using ChineseGrocery.Sim.Customers;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Seafood;
using ChineseGrocery.Sim.Tasks;

namespace ChineseGrocery.Sim.Core;

/// <summary>
/// The seafood counter: customers who want a live fish or lobster wait there while a worker takes one from a tank and
/// kills it at the counter; then they carry on shopping. One kill task per counter, for the customer first in line.
/// </summary>
public sealed partial class GameSim
{
    private void RefreshKillTasks()
    {
        Tasks.RemoveOpen(t => t is KillFishTask k && WaitingAtCounter(k.CustomerId) is null);

        foreach (var counter in Layout.Fixtures.Where(f => _fixtures[f.FixtureId].Kind == FixtureKind.SeafoodCounter))
        {
            if (Tasks.Tasks.Any(t => t is KillFishTask k && k.CounterInstanceId == counter.InstanceId)) continue;

            var front = _customers
                .Where(c => c.State == CustomerState.AtCounter && c.CheckoutInstanceId == counter.InstanceId)
                .MinBy(c => c.QueueNumber);
            if (front is null) continue;

            var productId = front.WaitingFor!;
            var source = Tanks().SelectMany(TankSlots).Where(p => p.Count(productId) > 0).Select(p => (StockLocation?)p.Location).Min();
            if (source is null)
            {
                // Sold out while they queued: they move on to the rest of their list.
                NoteNotFound(front, productId);
                StopWaiting(front);
                continue;
            }

            Tasks.Add(id => new KillFishTask(id, PriorityOf(TaskType.KillFish), counter.InstanceId, front.Id, productId, source.Value));
        }
    }

    private Customer? WaitingAtCounter(int customerId) =>
        _customers.Find(c => c.Id == customerId && c.State == CustomerState.AtCounter);

    private static void StopWaiting(Customer customer)
    {
        customer.State = CustomerState.Shopping;
        customer.CheckoutInstanceId = null;
        customer.WaitingFor = null;
    }

    /// <summary>At the tank: the customer is now being served (no more waiting out their patience) while the fish is fetched.</summary>
    private bool StartKill(Worker worker, KillFishTask task)
    {
        if (WaitingAtCounter(task.CustomerId) is not { } customer) return false;

        customer.State = CustomerState.BeingServed;
        BeginStep(worker, WorkStep.Picking, Data.Economy.Tasks.KillPickupMinutes);
        return true;
    }

    private void PickUpForKill(Worker worker, KillFishTask task)
    {
        if (Inventory.Count(task.Source, task.ProductId) == 0)
        {
            // Gone from this slot: back in line, and the next refresh looks for another tank.
            if (_customers.Find(c => c.Id == task.CustomerId) is { State: CustomerState.BeingServed } customer) customer.State = CustomerState.AtCounter;
            FinishTask(worker, completed: false);
            return;
        }

        worker.Carry(Inventory.Get(task.Source).Take(task.ProductId, 1));
        worker.Step = WorkStep.Carrying;
    }

    private void StartKilling(Worker worker)
    {
        worker.Step = WorkStep.Killing;
        worker.WorkTicksLeft = KillTicks(worker);
    }

    /// <summary>Ticks to kill and prepare one animal: the data time, faster with work speed and with each knife skill point.</summary>
    internal int KillTicks(Worker worker)
    {
        var settings = Data.Economy.Tasks;
        var skillPermille = 1000L + (long)Math.Max(0, worker.KnifeSkill - 1) * settings.KillSkillSpeedPermille;
        return WorkTicks(settings.KillFishMinutes, (int)(EffectiveSpeed(worker) * skillPermille / 1000));
    }

    /// <summary>Hands the prepared animal to the customer, who goes back to shopping; if they are gone, it is kept chilled.</summary>
    private void FinishKill(Worker worker, KillFishTask task)
    {
        var lot = worker.DropAll().Single();
        if (_customers.Find(c => c.Id == task.CustomerId) is not { State: CustomerState.BeingServed } customer)
        {
            KeepAsChilled(lot);
            return;
        }

        customer.AddToBasket(new BasketItem(lot, new StockLocation(task.CounterInstanceId, 0), Prices.PriceOf(lot.ProductId)));
        StopWaiting(customer);
        _events.Add(new FishKilled(task.CounterInstanceId, customer.Id, lot.ProductId, worker.Id));
    }
}
