using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Tasks;

namespace ChineseGrocery.Sim.Core;

/// <summary>Generating tasks and running workers through them. Shared by the player and staff.</summary>
public sealed partial class GameSim
{
    private void RefreshTasks() => RefreshRestockTasks();

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
        Shelves.ProductAt(r.Target) == r.ProductId && Inventory.Count(r.Source, r.ProductId) > 0;

    private StockLocation? FindRestockSource(string productId) =>
        Inventory.Locations
            .Where(p => IsStorageArea(p.Location.FixtureInstanceId) && p.Count(productId) > 0)
            .Select(p => (StockLocation?)p.Location)
            .FirstOrDefault();

    private bool IsStorageArea(int instanceId) =>
        Layout.Find(instanceId) is { } f && DataValidator.IsStorageArea(_fixtures[f.FixtureId].Kind);

    /// <summary>Whether any claimed task (or goods being carried for one) involves the fixture.</summary>
    private bool IsFixtureInUse(int instanceId) =>
        Tasks.Tasks.Any(t => Tasks.ClaimedBy(t.Id) is not null && Involves(t, instanceId));

    private static bool Involves(SimTask task, int instanceId) => task switch
    {
        RestockTask r => r.Source.FixtureInstanceId == instanceId || r.Target.FixtureInstanceId == instanceId,
        _ => false,
    };

    /// <summary>Starts whatever the worker can do at this fixture. Returns false when there is nothing.</summary>
    private bool TryStartWorkAt(Worker worker, PlacedFixture placed)
    {
        if (worker.Step == WorkStep.Carrying)
        {
            if (Tasks.Find(worker.TaskId!.Value) is not RestockTask carrying || carrying.Target.FixtureInstanceId != placed.InstanceId) return false;

            BeginStep(worker, WorkStep.Shelving, Data.Economy.Tasks.RestockShelveMinutes);
            return true;
        }

        if (DataValidator.IsStorageArea(_fixtures[placed.FixtureId].Kind))
        {
            var task = Tasks.OpenTasks.OfType<RestockTask>().FirstOrDefault(t => t.Source.FixtureInstanceId == placed.InstanceId);
            if (task is null || !Tasks.TryClaim(task.Id, worker.Id)) return false;

            worker.TaskId = task.Id;
            BeginStep(worker, WorkStep.Picking, Data.Economy.Tasks.RestockPickupMinutes);
            _events.Add(new TaskStarted(task.Id, task.Type, worker.Id));
            return true;
        }

        return false;
    }

    private void BeginStep(Worker worker, WorkStep step, int minutes)
    {
        worker.Step = step;
        worker.WorkTicksLeft = WorkTicks(minutes, worker.WorkSpeedPermille);
    }

    /// <summary>Ticks a job of <paramref name="minutes"/> takes at the given speed, rounded up.</summary>
    private int WorkTicks(int minutes, int speedPermille)
    {
        var perTick = (long)speedPermille * Data.Economy.MinutesPerTick;
        return (int)((minutes * 1000L + perTick - 1) / perTick);
    }

    private void UpdateWorkers()
    {
        foreach (var worker in _workers)
        {
            MoveWorker(worker);
            if (!worker.IsWorking) continue;

            if (worker.WorkTicksLeft > 0) worker.WorkTicksLeft--;
            if (worker.WorkTicksLeft == 0) CompleteStep(worker);
        }
    }

    private void MoveWorker(Worker worker)
    {
        for (var moved = 0; moved < worker.MoveCellsPerTick && worker.Path.Count > 0;)
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
                foreach (var lot in worker.DropAll()) shelf.Add(lot);
                FinishTask(worker, completed: true);
                break;

            default:
                throw new InvalidOperationException($"Worker {worker.Id} cannot finish {worker.Step} on {task.GetType().Name}.");
        }
    }

    private void FinishTask(Worker worker, bool completed)
    {
        var task = Tasks.Find(worker.TaskId!.Value)!;
        Tasks.Remove(task.Id);
        worker.TaskId = null;
        worker.Step = WorkStep.Idle;
        worker.WorkTicksLeft = 0;
        if (completed) _events.Add(new TaskCompleted(task.Id, task.Type, worker.Id));
    }
}
