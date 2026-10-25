using ChineseGrocery.Sim.Customers;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Save;
using ChineseGrocery.Sim.Staff;
using ChineseGrocery.Sim.Tasks;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Core;

/// <summary>
/// Running the store through staff: daily applicants, firing, shifts and rest days, morale from work and quitting,
/// task priorities and per-worker task limits, and managers who order, price and plan the menu for a department.
/// Staff randomness (applicants, quitting, managers' estimates) comes from its own seeded stream, so it never shifts
/// the customers' rolls.
/// </summary>
public sealed partial class GameSim
{
    private const ulong StaffStreamSalt = 0x5A17_F00D_C0FF_EE11;

    private readonly List<Applicant> _applicants = [];
    private readonly SortedDictionary<TaskType, int> _priorityOverrides = new();
    private readonly SortedDictionary<Department, DepartmentAutomation> _automation = new();
    private readonly SortedDictionary<string, int> _orderTargets = new(StringComparer.Ordinal);
    private SimRng _staffRng = null!;
    private int _nextApplicantId = 1;

    public IReadOnlyList<Applicant> Applicants => _applicants;
    public IReadOnlyDictionary<Department, DepartmentAutomation> Automation => _automation;
    public IReadOnlyDictionary<string, int> OrderTargets => _orderTargets;

    /// <summary>How urgent a task type is now: the player's override, else the data default.</summary>
    public int PriorityOf(TaskType type) =>
        _priorityOverrides.TryGetValue(type, out var p) ? p : Data.Economy.Tasks.PriorityOf(type);

    /// <summary>How many of a role the current stage allows.</summary>
    public int RoleLimit(RoleDef role) =>
        Data.Stages[Economy.Stage - 1].RoleLimits?.FirstOrDefault(l => l.RoleId == role.Id)?.Max ?? role.MaxCount;

    public Worker? ManagerOf(Department department) => _workers.Find(w => w.ManagedDepartment == department);

    /// <summary>
    /// The departments one manager's area covers: the dry goods manager runs all the packaged aisles (dry goods,
    /// sauces, snacks, drinks); the others run their own department.
    /// </summary>
    public static IReadOnlyList<Department> AreaOf(Department department) => department == Department.DryGoods
        ? [Department.DryGoods, Department.Sauces, Department.Snacks, Department.Drinks]
        : [department];

    private void StartStaffStream(ulong seed) => _staffRng = new SimRng(seed ^ StaffStreamSalt);

    /// <summary>Yesterday's applicants leave; new ones come for roles unlocked and not full.</summary>
    private void RefreshApplicants()
    {
        _applicants.Clear();
        for (var i = 0; i < Data.Employees.ApplicantsPerDay; i++)
        {
            var open = Data.Employees.Roles.Where(r => r.UnlockStage <= Economy.Stage && _workers.Count(w => w.RoleId == r.Id) < RoleLimit(r)).ToList();
            if (open.Count == 0) return;

            var role = open[_staffRng.NextInt(0, open.Count)];
            // A well-reviewed store draws slightly better applicants: the low end of each roll rises with reputation.
            var skill = _staffRng.NextInt(ApplicantFloor(role.KnifeSkill), role.KnifeSkill.Max + 1);
            var speed = _staffRng.NextInt(ApplicantFloor(role.SpeedPermille), role.SpeedPermille.Max + 1);
            _applicants.Add(new Applicant(_nextApplicantId++, role.Id, skill, speed, AskingWage(role, skill, speed)));
        }
    }

    /// <summary>The market wage, scaled from the low to the high asking share by how good the applicant is (0-1000).</summary>
    private long AskingWage(RoleDef role, int skill, int speed)
    {
        static long Share(int value, IntRange range) => range.Max == range.Min ? 500 : (value - range.Min) * 1000L / (range.Max - range.Min);

        var quality = (Share(skill, role.KnifeSkill) + Share(speed, role.SpeedPermille)) / 2;
        var asking = Data.Employees.AskingWage ?? new IntRange(1000, 1000);
        return role.MarketWageCentsPerDay * (asking.Min + (asking.Max - asking.Min) * quality / 1000) / 1000;
    }

    private Rejection? HandleHireApplicant(HireApplicant hire)
    {
        if (Clock.Phase != DayPhase.PreOpen) return Reject(RejectReason.WrongPhase);
        if (_applicants.Find(a => a.Id == hire.ApplicantId) is not { } applicant) return Reject(RejectReason.UnknownApplicant);

        var role = Data.Employees.Roles.Single(r => r.Id == applicant.RoleId);
        if (role.UnlockStage > Economy.Stage) return Reject(RejectReason.RoleLocked);
        if (_workers.Count(w => w.RoleId == role.Id) >= RoleLimit(role)) return Reject(RejectReason.RoleFull);

        _applicants.Remove(applicant);
        AddStaff(role, applicant.Skill, applicant.SpeedPermille, applicant.AskingWageCentsPerDay);
        return null;
    }

    private Rejection? HandleRejectApplicant(RejectApplicant reject) =>
        _applicants.RemoveAll(a => a.Id == reject.ApplicantId) > 0 ? null : Reject(RejectReason.UnknownApplicant);

    private Rejection? HandleFireEmployee(FireEmployee fire)
    {
        if (StaffMember(fire.WorkerId) is not { } worker) return Reject(RejectReason.UnknownWorker);

        var severance = Data.Employees.SeveranceDays * worker.WageCentsPerDay;
        Economy.Pay(ExpenseCategory.Wages, severance);
        RemoveStaff(worker);
        _events.Add(new EmployeeFired(worker.Id, severance));
        return null;
    }

    private Worker? StaffMember(int workerId) => _workers.Find(w => w.Id == workerId && w.RoleId is not null);

    /// <summary>A worker leaves: their task goes back on the board, a customer they served goes back in line, and goods they carry go to storage (or are lost).</summary>
    private void RemoveStaff(Worker worker)
    {
        if (worker.TaskId is { } taskId)
        {
            foreach (var customer in _customers.Where(c => c.State == CustomerState.BeingServed && IsServedBy(c, worker)))
            {
                customer.State = Tasks.Find(taskId) is CheckoutTask ? CustomerState.Queued : CustomerState.AtCounter;
            }

            Tasks.Release(taskId);
        }

        foreach (var lot in worker.DropAll())
        {
            if (StorageRouting.FindDestination(Data, Layout.Fixtures, _products[lot.ProductId]) is { } storage)
            {
                Inventory.GetOrCreate(StockLocation.Storage(storage.InstanceId)).Add(lot);
            }
            else
            {
                Economy.RecordLoss(checked(lot.Quantity * lot.UnitCostCents), _products[lot.ProductId].Department);
            }
        }

        _workers.Remove(worker);
    }

    private bool IsServedBy(Customer customer, Worker worker) =>
        worker.ServingCustomerId == customer.Id
        || Tasks.Find(worker.TaskId!.Value) is KillFishTask k && k.CustomerId == customer.Id
        || Tasks.Find(worker.TaskId!.Value) is ServeDeliTask s && s.CustomerId == customer.Id;

    private Rejection? HandleSetShift(SetShift set)
    {
        if (StaffMember(set.WorkerId) is not { } worker) return Reject(RejectReason.UnknownWorker);

        worker.Shift = set.Shift;
        return null;
    }

    private Rejection? HandleSetRestDays(SetRestDays set)
    {
        if (StaffMember(set.WorkerId) is not { } worker) return Reject(RejectReason.UnknownWorker);
        if (set.Weekdays.Any(d => d is < 0 or > 6)) return Reject(RejectReason.InvalidQuantity);

        worker.RestDays = set.Weekdays.Distinct().Order().ToList();
        return null;
    }

    private Rejection? HandleSetWorkerTasks(SetWorkerTasks set)
    {
        if (StaffMember(set.WorkerId) is not { } worker) return Reject(RejectReason.UnknownWorker);

        worker.AllowedTasks = set.Only?.Distinct().Order().ToList();
        return null;
    }

    private Rejection? HandleSetTaskPriority(SetTaskPriority set)
    {
        if (set.Priority is { } p) _priorityOverrides[set.TaskType] = p;
        else _priorityOverrides.Remove(set.TaskType);

        Tasks.Reprioritize(set.TaskType, PriorityOf(set.TaskType));
        return null;
    }

    private Rejection? HandleAssignManager(AssignManager assign)
    {
        if (StaffMember(assign.WorkerId) is not { } worker) return Reject(RejectReason.UnknownWorker);
        if (RoleOf(worker) is not { Manager: true }) return Reject(RejectReason.NotAManager);

        if (assign.Department is { } department && ManagerOf(department) is { } current) current.ManagedDepartment = null;
        worker.ManagedDepartment = assign.Department;
        return null;
    }

    private Rejection? HandleSetAutomation(SetAutomation set)
    {
        _automation[set.Department] = new DepartmentAutomation(set.AutoOrder, set.AutoPrice);
        return null;
    }

    private Rejection? HandleSetOrderTarget(SetOrderTarget set)
    {
        if (!_products.TryGetValue(set.ProductId, out var product)) return Reject(RejectReason.UnknownProduct);
        if (!product.Orderable) return Reject(RejectReason.NotOrderable);
        if (set.Units < 0) return Reject(RejectReason.InvalidQuantity);

        if (set.Units == 0) _orderTargets.Remove(set.ProductId);
        else _orderTargets[set.ProductId] = set.Units;
        return null;
    }

    private bool IsRestDay(Worker worker) => worker.RestDays.Contains(Clock.Weekday);

    /// <summary>Whether the worker's shift covers the store's current time (the player is always in).</summary>
    private bool IsOnShift(Worker worker)
    {
        if (worker.IsPlayer) return true;
        if (IsRestDay(worker)) return false;
        if (Data.Economy.Shifts is not { } shifts || worker.Shift == Shift.Full) return true;

        return Clock.Phase switch
        {
            DayPhase.PreOpen => worker.Shift == Shift.Morning,
            DayPhase.Closed => worker.Shift == Shift.Evening,
            _ => worker.Shift == Shift.Morning ? Clock.MinuteOfDay < shifts.MorningEndMinuteOfDay : Clock.MinuteOfDay >= shifts.EveningStartMinuteOfDay,
        };
    }

    /// <summary>Staff come in at the door when their shift starts and go home when it ends (after finishing what they are doing).</summary>
    private void UpdatePresence(Worker worker)
    {
        var due = IsOnShift(worker);
        if (due && !worker.IsPresent)
        {
            worker.IsPresent = true;
            worker.Position = PlayerStartCell();
            worker.SetPath([]);
        }
        else if (!due && worker.IsPresent && worker.TaskId is null)
        {
            worker.IsPresent = false;
            worker.SetPath([]);
        }
    }

    /// <summary>A day's pay: full for a full shift, a share for a half shift, nothing on a rest day.</summary>
    private long WageFor(Worker worker)
    {
        if (IsRestDay(worker)) return 0;
        if (worker.Shift == Shift.Full || Data.Economy.Shifts is not { } shifts) return worker.WageCentsPerDay;

        return worker.WageCentsPerDay * shifts.HalfShiftWagePermille / 1000;
    }

    /// <summary>Morale is the wage-based level plus what the work has done to it.</summary>
    private void RecomputeMorale(Worker worker)
    {
        if (RoleOf(worker) is not { } role) return;

        worker.MoralePermille = Math.Clamp(Morale.FromWage(role, worker.WageCentsPerDay) + worker.MoraleAdjustPermille, 0, 1000);
    }

    /// <summary>
    /// At closing: long runs of work wear morale down and rest days restore it; a far heavier day than the others costs a
    /// little. Staff below the quitting line are warned; if still there the next closing, they may quit.
    /// </summary>
    private void CloseStaffDay()
    {
        if (Data.Employees.Morale is not { } rules) return;

        var staff = _workers.Where(w => w.RoleId is not null).ToList();
        var working = staff.Where(w => !IsRestDay(w)).ToList();
        var average = working.Count == 0 ? 0 : working.Sum(w => w.TasksToday) * 1000L / working.Count;
        foreach (var worker in staff)
        {
            if (IsRestDay(worker))
            {
                worker.WorkStreakDays = 0;
                worker.MoraleAdjustPermille = Math.Min(0, worker.MoraleAdjustPermille + rules.RestRecoveryPermille);
            }
            else
            {
                worker.WorkStreakDays++;
                if (worker.WorkStreakDays > rules.MaxStreakDays) worker.MoraleAdjustPermille -= rules.StreakPenaltyPermille;
                if (average > 0 && worker.TasksToday * 1000L * 1000 > average * rules.WorkloadFactorPermille) worker.MoraleAdjustPermille -= rules.WorkloadPenaltyPermille;
            }

            worker.MoraleAdjustPermille = Math.Clamp(worker.MoraleAdjustPermille, -1000, 1000);
            RecomputeMorale(worker);
        }

        foreach (var worker in staff.Where(w => w.MoralePermille < rules.QuitBelowPermille))
        {
            if (worker.QuitWarning && _staffRng.NextInt(0, 1000) < rules.QuitChancePermille)
            {
                RemoveStaff(worker);
                _events.Add(new EmployeeQuit(worker.Id, worker.RoleId!));
            }
            else if (!worker.QuitWarning)
            {
                worker.QuitWarning = true;
                _events.Add(new QuitWarned(worker.Id));
            }
        }

        foreach (var worker in staff.Where(w => w.MoralePermille >= rules.QuitBelowPermille)) worker.QuitWarning = false;
    }

    /// <summary>
    /// Each morning, every department with a manager (not on a rest day) and automation on: order up to the targets,
    /// a little over or under for a less able manager, in product-list order while cash and room allow; reset prices
    /// to the suggested ones; plan the deli menu.
    /// </summary>
    private void RunManagers()
    {
        foreach (var (department, automation) in _automation)
        {
            if (ManagerOf(department) is not { } manager || IsRestDay(manager)) continue;

            var area = AreaOf(department);
            var products = Data.Products.Where(p => area.Contains(p.Department)).ToList();
            if (automation.AutoOrder)
            {
                foreach (var product in products.Where(p => _orderTargets.ContainsKey(p.Id)))
                {
                    var have = Inventory.Total(product.Id) + _orders.Pending.Where(o => o.ProductId == product.Id).Sum(o => o.Quantity);
                    var need = _orderTargets[product.Id] - have;
                    if (need <= 0) continue;

                    var spread = (RoleOf(manager)!.KnifeSkill.Max - manager.KnifeSkill) * Data.Employees.ManagerErrorPerPointPermille;
                    var wanted = (int)Math.Max(1, need * (1000L + _staffRng.NextInt(-spread, spread + 1)) / 1000);
                    var affordable = product.CostCents == 0 ? wanted : (int)Math.Min(wanted, Math.Max(0, Economy.CashCents) / product.CostCents);
                    var quantity = Math.Min(affordable, RoomFor(product.Id));
                    if (quantity > 0) HandlePlaceOrder(new PlaceOrder(product.Id, quantity));
                    if (quantity < wanted) _events.Add(new AutoOrderShort(product.Id, wanted, quantity));
                }
            }

            if (automation.AutoPrice)
            {
                foreach (var product in products.Where(p => Prices.PriceOf(p.Id) != p.SuggestedPriceCents)) Prices.Set(product.Id, product.SuggestedPriceCents);
            }

            if (department == Department.Deli && automation.AutoOrder) PlanMenuAutomatically();
        }
    }

    /// <summary>
    /// The deli manager's menu: every station slot gets a different dish, the one that would cook the most near-expiry
    /// stock first; the target is what sold over the last days, at least two cookings.
    /// </summary>
    private void PlanMenuAutomatically()
    {
        var chosen = new HashSet<string>();
        foreach (var station in Layout.Fixtures.Where(f => IsDeliStation(f.InstanceId)).ToList())
        {
            var def = _fixtures[station.FixtureId];
            for (var slot = 0; slot < def.Capacity; slot++)
            {
                var recipe = Data.Recipes
                    .Where(r => r.Station == def.Kind && !chosen.Contains(r.Id) && PortionsPossible(r.Id) > 0)
                    .OrderByDescending(r => r.Ingredients.Sum(x => IngredientCandidates(x.ProductId).Where(c => IsNearExpiry(c.Lot)).Sum(c => c.Lot.Quantity * c.Lot.UnitCostCents)))
                    .ThenByDescending(r => PortionsPossible(r.Id))
                    .ThenBy(r => r.Id, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (recipe is null) continue;

                chosen.Add(recipe.Id);
                var sold = _reports.TakeLast(3).Sum(r => r.Sales.FirstOrDefault(s => s.ProductId == recipe.Id)?.Units ?? 0) / Math.Max(1, Math.Min(3, _reports.Count));
                HandleSetMenuItem(new Deli.SetMenuItem(station.InstanceId, slot, recipe.Id, Math.Max(2 * recipe.Portions, sold)));
            }
        }
    }

    private StaffSave StaffSave() => new(
        _staffRng.State, _applicants.ToList(), _nextApplicantId,
        new SortedDictionary<TaskType, int>(_priorityOverrides), new SortedDictionary<Department, DepartmentAutomation>(_automation),
        new SortedDictionary<string, int>(_orderTargets, StringComparer.Ordinal));

    private void RestoreStaff(StaffSave? save, ulong fallbackSeed)
    {
        if (save is null)
        {
            StartStaffStream(fallbackSeed);
            return;
        }

        _staffRng = SimRng.FromState(save.Rng);
        _applicants.AddRange(save.Applicants);
        _nextApplicantId = save.NextApplicantId;
        foreach (var (k, v) in save.PriorityOverrides) _priorityOverrides[k] = v;
        foreach (var (k, v) in save.Automation) _automation[k] = v;
        foreach (var (k, v) in save.OrderTargets) _orderTargets[k] = v;
    }
}
