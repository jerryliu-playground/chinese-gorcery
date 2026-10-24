using ChineseGrocery.Sim.Customers;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Meat;
using ChineseGrocery.Sim.Tasks;

namespace ChineseGrocery.Sim.Core;

/// <summary>
/// Generating tasks and running workers through them. Every task starts at one fixture (storage area or checkout) and
/// some continue at a second one (the shelf to fill, the cutting table). The player and staff run the same steps; staff
/// just claim open tasks on their own.
/// </summary>
public sealed partial class GameSim
{
    private void RefreshTasks()
    {
        RefreshRestockTasks();
        RefreshCheckoutTasks();
        RefreshCutMeatTasks();
        RefreshTidyTasks();
        RefreshTankTasks();
    }

    private void RefreshRestockTasks()
    {
        var settings = Data.Economy.Tasks;
        Tasks.RemoveOpen(t => t is RestockTask r && !IsRestockStillUseful(r));

        foreach (var (slot, productId) in Shelves.Assigned)
        {
            if (Tasks.Tasks.Any(t => t is RestockTask r && r.Target == slot)) continue;

            var full = _products[productId].UnitsPerShelfSlot;
            if (Inventory.Count(slot, productId) * 1000L >= (long)full * settings.RestockBelowPermille) continue;
            if (FindRestockSource(productId) is not { } source) continue;

            Tasks.Add(id => new RestockTask(id, settings.PriorityOf(TaskType.Restock), source, slot, productId));
        }
    }

    private bool IsRestockStillUseful(RestockTask r) =>
        Shelves.ProductAt(r.Target) == r.ProductId
        && Inventory.Count(r.Source, r.ProductId) > 0
        && Inventory.Count(r.Target, r.ProductId) * 1000L < (long)_products[r.ProductId].UnitsPerShelfSlot * Data.Economy.Tasks.RestockBelowPermille;

    private StockLocation? FindRestockSource(string productId) =>
        Inventory.Locations
            .Where(p => IsStorageArea(p.Location.FixtureInstanceId) && p.Count(productId) > 0)
            .Select(p => (StockLocation?)p.Location)
            .FirstOrDefault();

    /// <summary>One task per checkout with someone waiting; finishing it serves one customer.</summary>
    private void RefreshCheckoutTasks()
    {
        Tasks.RemoveOpen(t => t is CheckoutTask c && !HasQueue(c.CheckoutInstanceId));

        foreach (var checkout in Layout.Fixtures.Where(f => _fixtures[f.FixtureId].Kind == FixtureKind.Checkout))
        {
            if (!HasQueue(checkout.InstanceId)) continue;
            if (Tasks.Tasks.Any(t => t is CheckoutTask c && c.CheckoutInstanceId == checkout.InstanceId)) continue;

            Tasks.Add(id => new CheckoutTask(id, Data.Economy.Tasks.PriorityOf(TaskType.Checkout), checkout.InstanceId));
        }
    }

    private bool HasQueue(int checkoutInstanceId) =>
        _customers.Any(c => c.State == CustomerState.Queued && c.CheckoutInstanceId == checkoutInstanceId);

    /// <summary>One task per primal waiting in storage, as long as there is a cutting table.</summary>
    private void RefreshCutMeatTasks()
    {
        var table = Layout.Fixtures.FirstOrDefault(f => _fixtures[f.FixtureId].Kind == FixtureKind.CuttingTable);
        if (table is null)
        {
            Tasks.RemoveOpen(t => t is CutMeatTask);
            return;
        }

        foreach (var cut in Data.MeatCuts)
        {
            foreach (var pile in Inventory.Locations.Where(p => IsStorageArea(p.Location.FixtureInstanceId)).ToList())
            {
                // Tasks whose primal is still in this pile: open ones, plus claimed ones not picked up yet.
                var waiting = pile.Count(cut.PrimalId);
                var tasks = Tasks.Tasks
                    .OfType<CutMeatTask>()
                    .Where(c => c.Source == pile.Location && c.PrimalId == cut.PrimalId && !IsPrimalPickedUp(c))
                    .ToList();

                // Too many (a primal expired or was cut elsewhere): drop the newest open ones.
                foreach (var extra in tasks.Where(t => Tasks.ClaimedBy(t.Id) is null).OrderByDescending(t => t.Id).Take(tasks.Count - waiting))
                {
                    Tasks.Remove(extra.Id);
                }

                for (var n = tasks.Count; n < waiting; n++)
                {
                    Tasks.Add(id => new CutMeatTask(id, Data.Economy.Tasks.PriorityOf(TaskType.CutMeat), pile.Location, cut.PrimalId, table.InstanceId));
                }
            }
        }
    }

    private bool IsPrimalPickedUp(CutMeatTask task) =>
        _workers.Any(w => w.TaskId == task.Id && w.Step is WorkStep.Carrying or WorkStep.Cutting);

    private bool IsStorageArea(int instanceId) =>
        Layout.Find(instanceId) is { } f && DataValidator.IsStorageArea(_fixtures[f.FixtureId].Kind);

    /// <summary>Whether any claimed task involves the fixture.</summary>
    private bool IsFixtureInUse(int instanceId) =>
        Tasks.Tasks.Any(t => Tasks.ClaimedBy(t.Id) is not null && Involves(t, instanceId));

    private static bool Involves(SimTask task, int instanceId) =>
        StartFixtureOf(task) == instanceId || SecondFixtureOf(task) == instanceId;

    /// <summary>Where a task begins: the storage area to take goods from, the checkout, or the stand to tidy.</summary>
    private static int StartFixtureOf(SimTask task) => task switch
    {
        RestockTask r => r.Source.FixtureInstanceId,
        CheckoutTask c => c.CheckoutInstanceId,
        CutMeatTask c => c.Source.FixtureInstanceId,
        TidyTask t => t.StandInstanceId,
        CleanTankTask c => c.TankInstanceId,
        RemoveDeadTask r => r.TankInstanceId,
        _ => throw new ArgumentOutOfRangeException(nameof(task)),
    };

    /// <summary>Where carried goods go next, if anywhere.</summary>
    private static int? SecondFixtureOf(SimTask task) => task switch
    {
        RestockTask r => r.Target.FixtureInstanceId,
        CutMeatTask c => c.CuttingTableInstanceId,
        _ => null,
    };

    /// <summary>Starts whatever the worker can do at this fixture. Returns false when there is nothing.</summary>
    private bool TryStartWorkAt(Worker worker, PlacedFixture placed)
    {
        if (worker.Step == WorkStep.Carrying)
        {
            var carrying = Tasks.Find(worker.TaskId!.Value)!;
            if (SecondFixtureOf(carrying) == placed.InstanceId)
            {
                StartSecondStep(worker, carrying);
                return true;
            }

            return TryPutBack(worker, placed);
        }

        var task = Tasks.OpenTasks.FirstOrDefault(t => StartFixtureOf(t) == placed.InstanceId && CanDo(worker, t));
        if (task is null || !Tasks.TryClaim(task.Id, worker.Id)) return false;

        worker.TaskId = task.Id;
        StartFirstStep(worker, task);
        return true;
    }

    /// <summary>
    /// A worker changing their mind: carried goods go into this storage area if it can keep them, and the task goes back
    /// on the board for anyone to take.
    /// </summary>
    private bool TryPutBack(Worker worker, PlacedFixture storage)
    {
        var def = _fixtures[storage.FixtureId];
        if (!DataValidator.IsStorageArea(def.Kind) || worker.Carrying.Any(l => !def.Storage.Contains(_products[l.ProductId].Storage))) return false;

        var location = StockLocation.Storage(storage.InstanceId);
        var pile = Inventory.GetOrCreate(location);
        foreach (var group in worker.DropAll().GroupBy(l => l.ProductId).ToList())
        {
            foreach (var lot in group) pile.Add(lot);
            _events.Add(new GoodsReturned(worker.Id, group.Key, group.Sum(l => l.Quantity), location));
        }

        Tasks.Release(worker.TaskId!.Value);
        worker.TaskId = null;
        worker.Step = WorkStep.Idle;
        return true;
    }

    /// <summary>The player can do every task type; staff only their role's.</summary>
    private bool CanDo(Worker worker, SimTask task) =>
        worker.IsPlayer || RoleOf(worker)?.TaskTypes.Contains(task.Type) == true;

    private void StartFirstStep(Worker worker, SimTask task)
    {
        var settings = Data.Economy.Tasks;
        switch (task)
        {
            case RestockTask:
                BeginStep(worker, WorkStep.Picking, settings.RestockPickupMinutes);
                break;

            case CutMeatTask:
                BeginStep(worker, WorkStep.Picking, settings.CutPickupMinutes);
                break;

            case TidyTask:
                BeginStep(worker, WorkStep.Tidying, settings.TidyMinutes);
                break;

            case CleanTankTask:
                BeginStep(worker, WorkStep.Cleaning, settings.CleanTankMinutes);
                break;

            case RemoveDeadTask:
                BeginStep(worker, WorkStep.RemovingDead, settings.RemoveDeadMinutes);
                break;

            case CheckoutTask c:
                var customer = _customers
                    .Where(x => x.State == CustomerState.Queued && x.CheckoutInstanceId == c.CheckoutInstanceId)
                    .MinBy(x => x.QueueNumber)!;
                customer.State = CustomerState.BeingServed;
                worker.ServingCustomerId = customer.Id;
                BeginStep(worker, WorkStep.Serving, settings.CheckoutBaseMinutes + settings.CheckoutMinutesPerItem * customer.Basket.Sum(i => i.Lot.Quantity));
                break;
        }

        _events.Add(new TaskStarted(task.Id, task.Type, worker.Id));
    }

    private void StartSecondStep(Worker worker, SimTask task)
    {
        switch (task)
        {
            case RestockTask:
                BeginStep(worker, WorkStep.Shelving, Data.Economy.Tasks.RestockShelveMinutes);
                break;

            case CutMeatTask:
                BeginStep(worker, WorkStep.Cutting, Data.Economy.Tasks.CutMeatMinutes);
                break;
        }
    }

    private void BeginStep(Worker worker, WorkStep step, int minutes)
    {
        worker.Step = step;
        worker.WorkTicksLeft = WorkTicks(minutes, EffectiveSpeed(worker));
    }

    /// <summary>Ticks a job of <paramref name="minutes"/> takes at the given speed, rounded up.</summary>
    private int WorkTicks(int minutes, int speedPermille)
    {
        var perTick = (long)speedPermille * Data.Economy.SecondsPerTick;
        return (int)((minutes * 60_000L + perTick - 1) / perTick);
    }

    /// <summary>
    /// How many whole cells to step this tick. Speed is cells per game minute; the remainder carries over in
    /// cell-seconds so slow walkers still move smoothly and nothing is lost to rounding. Resting walkers bank nothing.
    /// </summary>
    private int StepsThisTick(ref int progress, int cellsPerMinute, bool hasPath)
    {
        if (!hasPath)
        {
            progress = 0;
            return 0;
        }

        progress += cellsPerMinute * Data.Economy.SecondsPerTick;
        var steps = progress / 60;
        progress %= 60;
        return steps;
    }

    private void UpdateWorkers()
    {
        foreach (var worker in _workers)
        {
            if (worker.IsPlayer) ServeIfBehindRegister(worker);
            else DriveStaff(worker);

            MoveWorker(worker);
            if (!worker.IsWorking) continue;

            if (worker.WorkTicksLeft > 0) worker.WorkTicksLeft--;
            if (worker.WorkTicksLeft == 0) CompleteStep(worker);
        }
    }

    /// <summary>
    /// The one task the player takes without pressing anything: standing idle at a checkout's work cell serves its queue,
    /// customer after customer, with the same checkout task logic as staff.
    /// </summary>
    private void ServeIfBehindRegister(Worker player)
    {
        if (player.Step != WorkStep.Idle || player.Path.Count > 0) return;

        foreach (var task in Tasks.OpenTasks.OfType<CheckoutTask>().ToList())
        {
            if (!IsAtFixture(player, task.CheckoutInstanceId) || !Tasks.TryClaim(task.Id, player.Id)) continue;

            player.TaskId = task.Id;
            StartFirstStep(player, task);
            return;
        }
    }

    /// <summary>Staff pick their own work: claim the best open task they can do, walk to it, and carry it through.</summary>
    private void DriveStaff(Worker worker)
    {
        if (worker.IsWorking || worker.Path.Count > 0) return;

        if (worker.Step == WorkStep.Idle)
        {
            foreach (var task in Tasks.OpenTasks.Where(t => CanDo(worker, t)).ToList())
            {
                if (PathToFixture(worker, StartFixtureOf(task)) is not { } path || !Tasks.TryClaim(task.Id, worker.Id)) continue;

                worker.TaskId = task.Id;
                worker.Step = WorkStep.ToTask;
                worker.SetPath(path);
                break;
            }
        }

        if (worker.Step == WorkStep.ToTask && worker.Path.Count == 0)
        {
            var task = Tasks.Find(worker.TaskId!.Value)!;
            if (IsAtFixture(worker, StartFixtureOf(task))) StartFirstStep(worker, task);
            else worker.SetPath(PathToFixture(worker, StartFixtureOf(task)) ?? []);
        }
        else if (worker.Step == WorkStep.Carrying)
        {
            var target = SecondFixtureOf(Tasks.Find(worker.TaskId!.Value)!)!.Value;
            if (IsAtFixture(worker, target)) StartSecondStep(worker, Tasks.Find(worker.TaskId!.Value)!);
            else worker.SetPath(PathToFixture(worker, target) ?? []);
        }
    }

    private bool IsAtFixture(Worker worker, int instanceId) =>
        Layout.Find(instanceId) is { } f && LayoutRules.InteractionCells(_fixtures[f.FixtureId], f.Origin, f.Rotation).Contains(worker.Position);

    /// <summary>Shortest walk to any of the fixture's interaction cells, or null if none can be reached.</summary>
    private List<GridPoint>? PathToFixture(Worker worker, int instanceId)
    {
        if (Layout.Find(instanceId) is not { } f) return null;

        return LayoutRules.InteractionCells(_fixtures[f.FixtureId], f.Origin, f.Rotation)
            .Select(cell => GridPaths.FindPath(IsWalkable, worker.Position, cell))
            .Where(p => p is not null)
            .MinBy(p => p!.Count);
    }

    private void MoveWorker(Worker worker)
    {
        var progress = worker.MoveProgress;
        var steps = StepsThisTick(ref progress, worker.MoveCellsPerMinute, worker.Path.Count > 0);
        worker.MoveProgress = progress;
        for (var moved = 0; moved < steps && worker.Path.Count > 0;)
        {
            var next = worker.Path[0];
            if (!IsWalkable(next))
            {
                worker.SetPath(GridPaths.FindPath(IsWalkable, worker.Position, worker.Path[^1]) ?? []);
                continue;
            }

            worker.Position = next;
            worker.PopStep();
            moved++;
        }
    }

    private void CompleteStep(Worker worker)
    {
        var task = Tasks.Find(worker.TaskId!.Value)!;
        switch (worker.Step, task)
        {
            case (WorkStep.Picking, RestockTask r):
                var space = _products[r.ProductId].UnitsPerShelfSlot - Inventory.Count(r.Target, r.ProductId);
                var quantity = Math.Min(space, Inventory.Count(r.Source, r.ProductId));
                if (quantity <= 0)
                {
                    FinishTask(worker, completed: false);
                    return;
                }

                worker.Carry(Inventory.Get(r.Source).Take(r.ProductId, quantity));
                worker.Step = WorkStep.Carrying;
                break;

            case (WorkStep.Shelving, RestockTask r):
                var shelf = Inventory.GetOrCreate(r.Target);
                var wasEmpty = shelf.Count(r.ProductId) == 0;
                var shelved = worker.DropAll();
                foreach (var lot in shelved) shelf.Add(lot);
                _events.Add(new ItemsShelved(r.Target, r.ProductId, shelved.Sum(l => l.Quantity), wasEmpty));
                FinishTask(worker, completed: true);
                break;

            case (WorkStep.Serving, CheckoutTask checkout):
                var served = _customers.Single(c => c.Id == worker.ServingCustomerId);
                var revenue = CheckOut(served);
                worker.ServingCustomerId = null;
                _events.Add(new CheckoutCompleted(served.Id, revenue, worker.Id, checkout.CheckoutInstanceId));
                FinishTask(worker, completed: true);
                break;

            case (WorkStep.Picking, CutMeatTask c):
                if (Inventory.Count(c.Source, c.PrimalId) == 0)
                {
                    FinishTask(worker, completed: false);
                    return;
                }

                worker.Carry(Inventory.Get(c.Source).Take(c.PrimalId, 1));
                worker.Step = WorkStep.Carrying;
                break;

            case (WorkStep.Cutting, CutMeatTask c):
                CutPrimal(worker, c);
                FinishTask(worker, completed: true);
                break;

            case (WorkStep.Tidying, TidyTask t):
                Tidy(worker, t);
                FinishTask(worker, completed: true);
                break;

            case (WorkStep.Cleaning, CleanTankTask c):
                CleanTank(worker, c.TankInstanceId);
                FinishTask(worker, completed: true);
                break;

            case (WorkStep.RemovingDead, RemoveDeadTask r):
                TakeOutDead(worker, r.TankInstanceId);
                FinishTask(worker, completed: true);
                break;

            default:
                throw new InvalidOperationException($"Worker {worker.Id} cannot finish {worker.Step} on {task.GetType().Name}.");
        }
    }

    /// <summary>Turns the carried primal into boxes stored back where it came from (or wherever they fit); the rest is loss.</summary>
    private void CutPrimal(Worker worker, CutMeatTask task)
    {
        var primal = worker.DropAll().Single();
        var def = Data.MeatCuts.Single(m => m.PrimalId == task.PrimalId);
        var result = MeatCutting.Cut(def, id => _products[id], worker.KnifeSkill, primal.UnitCostCents);

        long lostCents = result.LossCents;
        foreach (var output in result.Outputs)
        {
            var product = _products[output.ProductId];
            var lot = new StockLot(output.ProductId, output.Packages, Clock.Day + product.ShelfLifeDays - 1, output.UnitCostCents);
            var destination = IsStorageArea(task.Source.FixtureInstanceId) && _fixtures[Layout.Find(task.Source.FixtureInstanceId)!.FixtureId].Storage.Contains(product.Storage)
                ? task.Source
                : StorageRouting.FindDestination(Data, Layout.Fixtures, product) is { } other ? StockLocation.Storage(other.InstanceId) : (StockLocation?)null;

            if (destination is { } to) Inventory.GetOrCreate(to).Add(lot);
            else lostCents += output.Packages * output.UnitCostCents;
        }

        Economy.RecordLoss(lostCents);
        _events.Add(new MeatCut(task.Id, worker.Id, task.CuttingTableInstanceId, task.PrimalId, result.Outputs, lostCents));
    }

    private void FinishTask(Worker worker, bool completed)
    {
        var task = Tasks.Find(worker.TaskId!.Value)!;
        Tasks.Remove(task.Id);
        worker.TaskId = null;
        worker.Step = WorkStep.Idle;
        worker.WorkTicksLeft = 0;
        if (completed)
        {
            _events.Add(new TaskCompleted(task.Id, task.Type, worker.Id));
            GainExperience(worker);
        }
    }
}
