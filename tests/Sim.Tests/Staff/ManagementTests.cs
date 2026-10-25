using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Save;
using ChineseGrocery.Sim.Staff;
using ChineseGrocery.Sim.Tasks;
using ChineseGrocery.Sim.Time;
using static ChineseGrocery.Sim.Tests.Customers.CustomerScenario;

namespace ChineseGrocery.Sim.Tests.Staff;

/// <summary>Spec 14: applicants, shifts and rest days, morale and quitting, priorities and task limits, managers.</summary>
public class ManagementTests
{
    private static readonly EmployeesDef Employees = SimTestKit.Data.Employees;

    private static RejectReason? Send(GameSim sim, SimCommand command)
    {
        sim.DrainEvents();
        sim.Run(command);
        return sim.DrainEvents().OfType<CommandRejected>().SingleOrDefault()?.Reason;
    }

    private static Worker HireRole(GameSim sim, string roleId)
    {
        sim.Run(new HireEmployee(roleId));
        return sim.Workers.Last(w => w.RoleId == roleId);
    }

    [Fact]
    public void Each_morning_brings_three_applicants_rolled_from_the_seed_within_the_role_ranges()
    {
        var a = SimTestKit.NewSim(seed: 5);
        var b = SimTestKit.NewSim(seed: 5);

        Assert.Equal(Employees.ApplicantsPerDay, a.Applicants.Count);
        Assert.Equal(a.Applicants, b.Applicants);
        foreach (var applicant in a.Applicants)
        {
            var role = Employees.Roles.Single(r => r.Id == applicant.RoleId);
            Assert.True(role.UnlockStage <= a.Economy.Stage);
            Assert.InRange(applicant.Skill, role.KnifeSkill.Min, role.KnifeSkill.Max);
            Assert.InRange(applicant.SpeedPermille, role.SpeedPermille.Min, role.SpeedPermille.Max);
            Assert.InRange(applicant.AskingWageCentsPerDay,
                role.MarketWageCentsPerDay * Employees.AskingWage!.Value.Min / 1000, role.MarketWageCentsPerDay * Employees.AskingWage!.Value.Max / 1000);
        }

        var yesterday = a.Applicants.ToList();
        a.RunFullDay();
        Assert.Equal(Employees.ApplicantsPerDay, a.Applicants.Count);
        Assert.NotEqual(yesterday, a.Applicants);
    }

    [Fact]
    public void Hiring_an_applicant_takes_their_attributes_and_asking_wage()
    {
        var sim = SimTestKit.NewSim();
        var applicant = sim.Applicants[0];

        Assert.Null(Send(sim, new HireApplicant(applicant.Id)));

        var worker = sim.Workers.Last();
        Assert.Equal((applicant.RoleId, applicant.Skill, applicant.SpeedPermille, applicant.AskingWageCentsPerDay),
            (worker.RoleId, worker.KnifeSkill, worker.WorkSpeedPermille, worker.WageCentsPerDay));
        Assert.DoesNotContain(sim.Applicants, x => x.Id == applicant.Id);
        Assert.Null(Send(sim, new RejectApplicant(sim.Applicants[0].Id)));
        Assert.Equal(Employees.ApplicantsPerDay - 2, sim.Applicants.Count);
    }

    [Fact]
    public void Firing_pays_severance_and_the_worker_leaves()
    {
        var sim = SimTestKit.NewSim();
        var cashier = HireRole(sim, "cashier");
        var cash = sim.Economy.CashCents;

        Assert.Null(Send(sim, new FireEmployee(cashier.Id)));

        Assert.DoesNotContain(sim.Workers, w => w.Id == cashier.Id);
        Assert.Equal(cash - Employees.SeveranceDays * cashier.WageCentsPerDay, sim.Economy.CashCents);
    }

    [Fact]
    public void A_cashier_serves_the_queue_without_the_player()
    {
        var sim = SimTestKit.NewSim(data: ScenarioData(perHour: 30));
        ShelveDirectly(sim, Rice, 20);
        HireRole(sim, "cashier");
        Open(sim);

        TickUntil(sim, () => sim.Economy.Today.Sales.Any());
    }

    [Theory]
    [InlineData(Shift.Morning, true, false)]
    [InlineData(Shift.Evening, false, true)]
    [InlineData(Shift.Full, true, true)]
    public void Staff_are_only_in_the_store_during_their_shift(Shift shift, bool morning, bool evening)
    {
        var sim = SimTestKit.NewSim(data: ScenarioData(perHour: 0));
        var cashier = HireRole(sim, "cashier");
        sim.Run(new SetShift(cashier.Id, shift));
        Open(sim);
        sim.Tick();
        Assert.Equal(morning, cashier.IsPresent);

        TickUntil(sim, () => sim.Clock.MinuteOfDay >= sim.Data.Economy.Shifts!.EveningStartMinuteOfDay + 1);
        Assert.Equal(evening, cashier.IsPresent);
    }

    [Fact]
    public void Absent_staff_take_no_tasks()
    {
        var sim = SimTestKit.NewSim(data: ScenarioData(perHour: 30));
        ShelveDirectly(sim, Rice, 20);
        var cashier = HireRole(sim, "cashier");
        sim.Run(new SetShift(cashier.Id, Shift.Evening));
        Open(sim);

        for (var i = 0; i < 600; i++)
        {
            sim.Tick();
            Assert.Null(cashier.TaskId);
        }
    }

    [Fact]
    public void Wages_follow_the_shift_and_rest_days_are_unpaid()
    {
        var sim = SimTestKit.NewSim();
        var full = HireRole(sim, "cashier");
        var half = HireRole(sim, "stocker");
        sim.Run(new SetShift(half.Id, Shift.Morning));
        sim.OpenAndRunToClose();
        var halfPay = half.WageCentsPerDay * sim.Data.Economy.Shifts!.HalfShiftWagePermille / 1000;
        Assert.Equal(full.WageCentsPerDay + halfPay, sim.Reports[^1].WagesCents);

        sim.Run(new StartNextDay());
        sim.Run(new SetRestDays(full.Id, [sim.Clock.Weekday]));
        sim.OpenAndRunToClose();
        Assert.Equal(halfPay, sim.Reports[^1].WagesCents);
    }

    [Fact]
    public void Working_many_days_in_a_row_wears_morale_down_and_a_rest_day_restores_it()
    {
        var sim = SimTestKit.NewSim();
        var cashier = HireRole(sim, "cashier");
        var start = cashier.MoralePermille;
        var limit = Employees.Morale!.MaxStreakDays;

        for (var day = 0; day < limit; day++) sim.RunFullDay();
        Assert.Equal(start, cashier.MoralePermille);
        sim.RunFullDay();
        var tired = cashier.MoralePermille;
        Assert.True(tired < start);

        sim.Run(new SetRestDays(cashier.Id, [sim.Clock.Weekday]));
        sim.RunFullDay();
        Assert.True(cashier.MoralePermille > tired);
        Assert.Equal(0, cashier.WorkStreakDays);
    }

    [Fact]
    public void Very_unhappy_staff_are_warned_about_then_may_quit()
    {
        var data = SimTestKit.Data with
        {
            Employees = Employees with { Morale = Employees.Morale! with { QuitChancePermille = 1000 } },
        };
        var sim = SimTestKit.NewSim(data: data);
        var cashier = HireRole(sim, "cashier");
        sim.Run(new SetWage(cashier.Id, 0));
        sim.DrainEvents();

        sim.OpenAndRunToClose();
        Assert.True(cashier.QuitWarning);
        Assert.Contains(sim.Workers, w => w.Id == cashier.Id);

        sim.Run(new StartNextDay());
        sim.OpenAndRunToClose();
        Assert.DoesNotContain(sim.Workers, w => w.Id == cashier.Id);
        Assert.Contains(sim.DrainEvents().OfType<EmployeeQuit>(), e => e.WorkerId == cashier.Id);
    }

    [Fact]
    public void A_raised_task_priority_comes_first()
    {
        var sim = SimTestKit.NewSim();
        sim.Run(new SetTaskPriority(TaskType.Tidy, 99));

        Assert.Equal(99, sim.PriorityOf(TaskType.Tidy));
        sim.Run(new SetTaskPriority(TaskType.Tidy, null));
        Assert.Equal(sim.Data.Economy.Tasks.PriorityOf(TaskType.Tidy), sim.PriorityOf(TaskType.Tidy));
    }

    [Fact]
    public void Open_tasks_are_re_ranked_when_a_priority_changes()
    {
        var sim = SimTestKit.NewSim(data: ScenarioData(perHour: 30));
        ShelveDirectly(sim, Rice, 1);
        Put(sim, Rice, 20);
        Open(sim);
        TickUntil(sim, () => sim.Tasks.OpenTasks.Any(t => t.Type == TaskType.Restock) && sim.Tasks.OpenTasks.Any(t => t.Type == TaskType.Checkout));
        Assert.Equal(TaskType.Checkout, sim.Tasks.OpenTasks.First().Type);

        sim.Run(new SetTaskPriority(TaskType.Restock, 50));

        Assert.Equal(TaskType.Restock, sim.Tasks.OpenTasks.First().Type);
    }

    private static void Put(GameSim sim, string productId, int units) =>
        sim.Inventory.GetOrCreate(StockLocation.Storage(sim.InstanceOf("backroom"))).Add(new StockLot(productId, units, 999, 100));

    [Fact]
    public void A_worker_limited_to_some_tasks_leaves_the_others()
    {
        var sim = SimTestKit.NewSim(data: ScenarioData(perHour: 0));
        var stocker = HireRole(sim, "stocker");
        sim.Run(new SetWorkerTasks(stocker.Id, [TaskType.Tidy]));
        ShelveDirectly(sim, Rice, 1);
        Put(sim, Rice, 20);

        for (var i = 0; i < 300; i++) sim.Tick();
        Assert.Null(stocker.TaskId);

        sim.Run(new SetWorkerTasks(stocker.Id, null));
        TickUntil(sim, () => stocker.TaskId is not null);
    }

    /// <summary>A stage 4 store (data shortcut: managers unlocked at stage 1) with a manager on dry goods.</summary>
    private static (GameSim Sim, Worker Manager) WithManager(long cash = 1_000_000)
    {
        var data = SimTestKit.Data with
        {
            Economy = SimTestKit.Data.Economy with { StartingCashCents = cash },
            Employees = Employees with { Roles = Employees.Roles.Select(r => r with { UnlockStage = 1 }).ToList() },
        };
        var sim = SimTestKit.NewSim(data: data);
        var manager = HireRole(sim, "manager");
        sim.Run(new AssignManager(manager.Id, Department.DryGoods));
        return (sim, manager);
    }

    [Fact]
    public void A_manager_tops_dry_goods_up_to_their_targets_every_morning()
    {
        var (sim, _) = WithManager();
        sim.Run(new SetAutomation(Department.DryGoods, AutoOrder: true, AutoPrice: false));
        sim.Run(new SetOrderTarget("rice_jasmine", 24));
        sim.Run(new SetOrderTarget("noodles_instant", 60));

        sim.RunFullDay();

        int Ordered(string id) => sim.PendingOrders.Where(o => o.ProductId == id).Sum(o => o.Quantity);
        var spread = sim.Data.Employees.ManagerErrorPerPointPermille * (sim.Data.Employees.Roles.Single(r => r.Id == "manager").KnifeSkill.Max - 1);
        Assert.InRange(Ordered("rice_jasmine"), 24 * (1000 - spread) / 1000, 24 * (1000 + spread) / 1000 + 1);
        Assert.InRange(Ordered("noodles_instant"), 60 * (1000 - spread) / 1000, 60 * (1000 + spread) / 1000 + 1);
    }

    [Fact]
    public void Short_of_cash_the_manager_orders_in_priority_order_and_warns()
    {
        var (sim, _) = WithManager();
        sim.Run(new SetAutomation(Department.DryGoods, AutoOrder: true, AutoPrice: false));
        sim.Run(new SetOrderTarget("rice_jasmine", 2));
        sim.Run(new SetOrderTarget("noodles_instant", 60));
        sim.OpenAndRunToClose();
        sim.Economy.CashCents = 2 * sim.Data.Products.Single(p => p.Id == "rice_jasmine").CostCents * 13 / 10;
        sim.DrainEvents();

        sim.Run(new StartNextDay());

        // Rice comes first in the product list, so it gets the money; noodles get what is left.
        Assert.Contains(sim.PendingOrders, o => o.ProductId == "rice_jasmine");
        Assert.True(sim.PendingOrders.Where(o => o.ProductId == "noodles_instant").Sum(o => o.Quantity) < 30);
        Assert.Contains(sim.DrainEvents().OfType<AutoOrderShort>(), e => e.ProductId == "noodles_instant");
    }

    [Fact]
    public void A_manager_with_auto_pricing_puts_prices_back_to_the_suggested_ones()
    {
        var (sim, _) = WithManager();
        sim.Run(new SetPrice("rice_jasmine", 5000));
        sim.Run(new SetAutomation(Department.DryGoods, AutoOrder: false, AutoPrice: true));

        sim.RunFullDay();

        Assert.Equal(sim.Prices.ReferenceOf("rice_jasmine"), sim.Prices.PriceOf("rice_jasmine"));
    }

    [Fact]
    public void Without_a_manager_nothing_is_automated()
    {
        var sim = SimTestKit.NewSim();
        sim.Run(new SetAutomation(Department.DryGoods, AutoOrder: true, AutoPrice: false));
        sim.Run(new SetOrderTarget("rice_jasmine", 24));

        sim.RunFullDay();

        Assert.Empty(sim.PendingOrders);
    }

    [Fact]
    public void The_player_works_any_time_and_is_never_paid()
    {
        var sim = SimTestKit.NewSim();
        sim.OpenAndRunToClose();

        Assert.True(sim.Player.IsPresent);
        Assert.Equal(0, sim.Reports[^1].WagesCents);
    }

    [Fact]
    public void Staff_shifts_automation_priorities_and_applicants_survive_save_and_load()
    {
        GameSim Setup()
        {
            var (sim, manager) = WithManager();
            var cashier = HireRole(sim, "cashier");
            sim.Run(new SetShift(cashier.Id, Shift.Evening));
            sim.Run(new SetRestDays(cashier.Id, [3, 5]));
            sim.Run(new SetWorkerTasks(cashier.Id, [TaskType.Checkout]));
            sim.Run(new SetTaskPriority(TaskType.Tidy, 30));
            sim.Run(new SetAutomation(Department.DryGoods, AutoOrder: true, AutoPrice: true));
            sim.Run(new SetOrderTarget("rice_jasmine", 24));
            sim.RunFullDay();
            return sim;
        }

        var paused = Setup();
        var json = SaveGame.Write(paused);
        var resumed = SaveGame.Read(paused.Data, json);
        Assert.Equal(json, SaveGame.Write(resumed));

        var straight = Setup();
        for (var day = 0; day < 3; day++)
        {
            straight.RunFullDay();
            resumed.RunFullDay();
        }

        Assert.Equal(SaveGame.Write(straight), SaveGame.Write(resumed));
    }
}
