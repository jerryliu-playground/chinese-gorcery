using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Staff;
using ChineseGrocery.Sim.Tasks;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.SimRunner;

/// <summary>
/// Stand-in player for headless runs. It only uses real commands. Before opening it upgrades the store once it can
/// afford to (keeping a cash reserve), places any missing meat fixtures, hires one of each unlocked role, assigns empty
/// display slots round-robin, tops each shelved product up to a stock target, keeps a primal on hand per cutting
/// recipe, and shelves goods. While open it works the highest-priority open task. Runner policy only, not a game rule.
/// </summary>
public sealed class PlayerBot(int stockTarget, int primalsOnHand = 1, long cashReserveCents = 100_000)
{
    private const int MaxTicksPerDay = 3 * 24 * 60 * 60;
    private const int MaxPreOpenTicks = 12_000;

    private static readonly FixtureKind[] MeatFixtures = [FixtureKind.ColdStorage, FixtureKind.MeatDisplay, FixtureKind.CuttingTable];

    private readonly List<string> _notes = [];

    private readonly SortedDictionary<string, int> _rejections = new(StringComparer.Ordinal);

    public int RejectedCommands => _rejections.Values.Sum();

    /// <summary>Unexpected rejections by command and reason, e.g. "PlayerInteract/NothingToDo".</summary>
    public IReadOnlyDictionary<string, int> Rejections => _rejections;
    public int OrdersCutForSpace { get; private set; }

    /// <summary>Called after every tick the bot runs (tests use it to check state mid-play).</summary>
    public Action? AfterTick { get; set; }

    /// <summary>Returns and clears notable things the bot did (upgrades, hires, placements).</summary>
    public IReadOnlyList<string> TakeNotes()
    {
        var notes = _notes.ToList();
        _notes.Clear();
        return notes;
    }

    public void PlayDay(GameSim sim)
    {
        TryUpgrade(sim);
        PlaceMissingMeatFixtures(sim);
        HireUnlockedRoles(sim);
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
        sim.Player.Step != WorkStep.Idle || sim.Player.Path.Count > 0 || sim.Tasks.OpenTasks.Any(t => t.Type != TaskType.Checkout);

    private void TryUpgrade(GameSim sim)
    {
        var stage = sim.Economy.Stage;
        if (stage >= sim.Data.Stages.Count) return;

        var next = sim.Data.Stages[stage];
        if (sim.Economy.CumulativeRevenueCents < next.RequiredCumulativeRevenueCents) return;
        if (sim.Economy.CashCents < next.UpgradeCostCents + cashReserveCents) return;

        if (Send(sim, new UpgradeStage()) is null) _notes.Add($"upgraded to stage {next.Id}");
    }

    private void PlaceMissingMeatFixtures(GameSim sim)
    {
        foreach (var kind in MeatFixtures)
        {
            if (sim.Layout.Fixtures.Any(f => Def(sim.Data, f).Kind == kind)) continue;
            if (sim.Data.Fixtures.FirstOrDefault(f => f.Kind == kind && f.UnlockStage <= sim.Economy.Stage) is not { } def) continue;
            if (sim.Economy.CashCents < def.PriceCents + cashReserveCents) continue;
            if (FindSpot(sim, def) is not { } spot) continue;

            if (Send(sim, new PlaceFixture(def.Id, spot.Origin, spot.Rotation)) is null) _notes.Add($"placed {def.Id} at {spot.Origin.X},{spot.Origin.Y}");
        }
    }

    /// <summary>The first legal spot, scanning from the right-hand side (where an upgrade adds floor).</summary>
    private static (GridPoint Origin, Rotation Rotation)? FindSpot(GameSim sim, FixtureDef def)
    {
        var size = sim.Layout.Size;
        FixtureDef Lookup(string id) => sim.Data.Fixtures.Single(f => f.Id == id);
        var workers = sim.Workers.Select(w => w.Position).ToHashSet();

        for (var x = size.W - 1; x >= 0; x--)
        for (var y = 0; y < size.H; y++)
        foreach (var rotation in Enum.GetValues<Rotation>())
        {
            var candidate = new PlacedFixture(sim.Layout.NextInstanceId, def.Id, new GridPoint(x, y), def.PriceCents, rotation);
            if (LayoutRules.Check(size, [.. sim.Layout.Fixtures, candidate], Lookup, candidate.InstanceId) is not null) continue;
            if (LayoutRules.Footprint(def, candidate.Origin, rotation).Any(workers.Contains)) continue;
            return (candidate.Origin, rotation);
        }

        return null;
    }

    private void HireUnlockedRoles(GameSim sim)
    {
        // Only hire once there is somewhere to work: every meat fixture placed.
        if (!MeatFixtures.All(kind => sim.Layout.Fixtures.Any(f => Def(sim.Data, f).Kind == kind))) return;

        foreach (var role in sim.Data.Employees.Roles.Where(r => r.UnlockStage <= sim.Economy.Stage))
        {
            if (sim.Workers.Any(w => w.RoleId == role.Id)) continue;
            if (Send(sim, new HireEmployee(role.Id)) is null) _notes.Add($"hired {role.Id}");
        }
    }

    private void AssignEmptySlots(GameSim sim)
    {
        var data = sim.Data;
        var primals = data.MeatCuts.Select(c => c.PrimalId).ToHashSet();
        var producible = data.MeatCuts.SelectMany(c => c.Cuts.Select(y => y.ProductId).Append(c.GroundProductId)).ToHashSet();

        foreach (var fixture in sim.Layout.Fixtures)
        {
            var def = Def(data, fixture);
            if (!DataValidator.IsDisplay(def.Kind)) continue;

            // Sell what can be bought or cut, but never a whole primal.
            var candidates = data.Products
                .Where(p => def.Storage.Contains(p.Storage) && !primals.Contains(p.Id) && (p.Orderable || producible.Contains(p.Id)))
                .ToList();
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
        var orderable = sim.Data.Products.Where(p => p.Orderable).Select(p => p.Id).ToHashSet();
        foreach (var productId in sim.Shelves.Assigned.Values.Distinct().Where(orderable.Contains).Order(StringComparer.Ordinal))
        {
            TopUp(sim, productId, stockTarget);
        }

        if (sim.Layout.Fixtures.Any(f => Def(sim.Data, f).Kind == FixtureKind.CuttingTable))
        {
            foreach (var cut in sim.Data.MeatCuts)
            {
                var primal = sim.Data.Products.Single(p => p.Id == cut.PrimalId);
                if (StorageRouting.FindDestination(sim.Data, sim.Layout.Fixtures, primal) is not null) TopUp(sim, cut.PrimalId, primalsOnHand);
            }
        }
    }

    private void TopUp(GameSim sim, string productId, int target)
    {
        var have = sim.Inventory.Total(productId)
                   + sim.PendingOrders.Where(o => o.ProductId == productId).Sum(o => o.Quantity)
                   + sim.Workers.Sum(w => w.Carrying.Where(l => l.ProductId == productId).Sum(l => l.Quantity));
        for (var want = target - have; want > 0; want /= 2)
        {
            if (Send(sim, new PlaceOrder(productId, want)) is not RejectReason.StorageFull) break;
            OrdersCutForSpace++;
        }
    }

    /// <summary>One tick: walk to and work the player's current or best open task.</summary>
    private void Step(GameSim sim)
    {
        var player = sim.Player;
        if (player.IsWorking || player.Path.Count > 0)
        {
            Tick(sim);
            return;
        }

        int? fixtureId = player.Step == WorkStep.Carrying
            ? sim.Tasks.Find(player.TaskId!.Value) switch
            {
                RestockTask r => r.Target.FixtureInstanceId,
                CutMeatTask c => c.CuttingTableInstanceId,
                _ => null,
            }
            : sim.Tasks.OpenTasks.FirstOrDefault() switch
            {
                CheckoutTask c => c.CheckoutInstanceId,
                RestockTask r => r.Source.FixtureInstanceId,
                CutMeatTask c => c.Source.FixtureInstanceId,
                _ => null,
            };
        if (fixtureId is null)
        {
            Tick(sim);
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
        return Tick(sim).FirstOrDefault(r => r.Command == command)?.Reason;
    }

    private List<CommandRejected> Tick(GameSim sim)
    {
        sim.Tick();
        var rejected = Drain(sim);
        AfterTick?.Invoke();
        return rejected;
    }

    private List<CommandRejected> Drain(GameSim sim)
    {
        var rejected = sim.DrainEvents().OfType<CommandRejected>().ToList();
        foreach (var r in rejected.Where(r => r.Reason != RejectReason.StorageFull))
        {
            var key = $"{r.Command.GetType().Name}/{r.Reason}";
            _rejections[key] = _rejections.GetValueOrDefault(key) + 1;
        }

        return rejected;
    }

    private static FixtureDef Def(GameData data, PlacedFixture fixture) => data.Fixtures.Single(f => f.Id == fixture.FixtureId);
}
