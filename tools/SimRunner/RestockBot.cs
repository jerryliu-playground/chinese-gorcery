using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.SimRunner;

/// <summary>
/// Stand-in player for headless runs: before opening, tops every orderable product up to a target
/// (stock on hand + pending), then plays the day through. Runner policy only, not a game rule.
/// </summary>
public sealed class RestockBot(int stockTarget)
{
    private const int MaxTicksPerDay = 24 * 60 + 1;

    public int RejectedCommands { get; private set; }

    public void PlayDay(GameSim sim)
    {
        foreach (var product in sim.Data.Products.Where(p => p.Orderable))
        {
            if (StorageRouting.FindDestination(sim.Data, product, sim.Economy.Stage) is null) continue;

            var have = sim.Inventory.Total(product.Id) + sim.PendingOrders.Where(o => o.ProductId == product.Id).Sum(o => o.Quantity);
            if (have < stockTarget)
            {
                sim.Enqueue(new PlaceOrder(product.Id, stockTarget - have));
            }
        }

        sim.Enqueue(new OpenStore());
        for (var guard = 0; sim.Clock.Phase != DayPhase.Closed; guard++)
        {
            if (guard == MaxTicksPerDay) throw new InvalidOperationException($"Day {sim.Clock.Day} never closed.");
            sim.Tick();
        }

        sim.Enqueue(new StartNextDay());
        sim.Tick();
        RejectedCommands += sim.DrainEvents().OfType<CommandRejected>().Count();
    }
}
