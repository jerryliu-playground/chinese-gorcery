using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Tasks;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.SimRunner;

/// <summary>
/// Stand-in player for headless runs. It only uses real commands: assigns empty shelf slots round-robin, tops each
/// shelved product up to a stock target before opening, shelves goods, then works the highest-priority open task
/// (checkout before restock) all day. Runner policy only, not a game rule.
/// </summary>
public sealed class PlayerBot(int stockTarget)
{
    private const int MaxTicksPerDay = 3 * 24 * 60;
    private const int MaxPreOpenTicks = 2000;

    public int RejectedCommands { get; private set; }
    public int OrdersCutForSpace { get; private set; }

    public void PlayDay(GameSim sim)
    {
        AssignEmptySlots(sim);
        PlaceOrders(sim);
        for (var i = 0; i < MaxPreOpenTicks && HasWork(sim); i++) Step(sim);

        Send(sim, new OpenStore());
        for (var guard = 0; sim.Clock.Phase != DayPhase.Closed; guard++)
        {
            if (guard == MaxTicksPerDay) throw new InvalidOperationException($"Day {sim.Clock.Day} never closed.");
            Step(sim);
        }

        Send(sim, new StartNextDay());
    }

    private static bool HasWork(GameSim sim) =>
        sim.Player.Step != WorkStep.Idle || sim.Player.Path.Count > 0 || sim.Tasks.OpenTasks.Any();

    private void AssignEmptySlots(GameSim sim)
    {
        var data = sim.Data;
        foreach (var fixture in sim.Layout.Fixtures)
        {
            var def = Def(data, fixture);
            if (!DataValidator.IsDisplay(def.Kind)) continue;

            var candidates = data.Products.Where(p => p.Orderable && def.Storage.Contains(p.Storage)).ToList();
            for (var slot = 0; slot < def.Capacity && candidates.Count > 0; slot++)
            {
                var location = new StockLocation(fixture.InstanceId, slot);
                if (sim.Shelves.ProductAt(location) is not null) continue;

                var used = sim.Shelves.Assigned.Values.ToList();
                var next = candidates.OrderBy(p => used.Count(u => u == p.Id)).ThenBy(p => candidates.IndexOf(p)).First();
                Send(sim, new AssignShelfSlot(fixture.InstanceId, slot, next.Id));
            }
        }
    }

    private void PlaceOrders(GameSim sim)
    {
        foreach (var productId in sim.Shelves.Assigned.Values.Distinct().Order(StringComparer.Ordinal))
        {
            var have = sim.Inventory.Total(productId)
                       + sim.PendingOrders.Where(o => o.ProductId == productId).Sum(o => o.Quantity)
                       + sim.Workers.Sum(w => w.Carrying.Where(l => l.ProductId == productId).Sum(l => l.Quantity));
            for (var want = stockTarget - have; want > 0; want /= 2)
            {
                if (Send(sim, new PlaceOrder(productId, want)) is not RejectReason.StorageFull) break;
                OrdersCutForSpace++;
            }
        }
    }

    /// <summary>One tick: walk to and work the player's current or best open task.</summary>
    private void Step(GameSim sim)
    {
        var player = sim.Player;
        if (player.IsWorking || player.Path.Count > 0)
        {
            sim.Tick();
            Drain(sim);
            return;
        }

        int? fixtureId = player.Step == WorkStep.Carrying
            ? ((RestockTask)sim.Tasks.Find(player.TaskId!.Value)!).Target.FixtureInstanceId
            : sim.Tasks.OpenTasks.FirstOrDefault() switch
            {
                CheckoutTask c => c.CheckoutInstanceId,
                RestockTask r => r.Source.FixtureInstanceId,
                _ => null,
            };
        if (fixtureId is null)
        {
            sim.Tick();
            Drain(sim);
            return;
        }

        var placed = sim.Layout.Find(fixtureId.Value)!;
        var cells = LayoutRules.InteractionCells(Def(sim.Data, placed), placed.Origin, placed.Rotation).ToList();
        Send(sim, cells.Contains(player.Position) ? new PlayerInteract(fixtureId) : new MovePlayer(cells[0]));
    }

    /// <summary>Runs one tick with the command and returns why it was rejected, if it was.</summary>
    private RejectReason? Send(GameSim sim, SimCommand command)
    {
        sim.Enqueue(command);
        sim.Tick();
        return Drain(sim).FirstOrDefault(r => r.Command == command)?.Reason;
    }

    private List<CommandRejected> Drain(GameSim sim)
    {
        var rejected = sim.DrainEvents().OfType<CommandRejected>().ToList();
        RejectedCommands += rejected.Count(r => r.Reason != RejectReason.StorageFull);
        return rejected;
    }

    private static FixtureDef Def(GameData data, PlacedFixture fixture) => data.Fixtures.Single(f => f.Id == fixture.FixtureId);
}
