using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Deli;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Staff;
using ChineseGrocery.Sim.Tasks;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.SimRunner;

/// <summary>
/// Stand-in player for headless runs. It only uses real commands. Before opening it upgrades the store once it can
/// afford to (keeping a cash reserve, and only once the current stage's departments are set up), places any missing
/// meat and seafood fixtures and a row of produce stands, hires each unlocked role once it has somewhere to work,
/// assigns empty display slots round-robin (produce only on produce stands, seafood only on tanks and ice tables), tops
/// each shelved product up to a stock target (fresh goods first: produce and clams to two fills of their slots, live
/// seafood to one since it dies in storage), keeps a primal on hand per cutting recipe, fills the shelves in one go, and
/// does any work left. With <c>runDeli</c> it also builds a deli corner once the seafood corner is done, plans the
/// menu each morning and keeps ducks and pork shoulder on hand. While open it works the highest-priority open task.
/// Runner policy only, not a game rule.
/// </summary>
public sealed class PlayerBot(int stockTarget, int primalsOnHand = 1, long cashReserveCents = 50_000, int produceStands = 4, bool runDeli = true)
{
    private const int MaxTicksPerDay = 3 * 24 * 60 * 60;
    private const int MaxPreOpenTicks = 12_000;

    private static readonly FixtureKind[] MeatFixtures = [FixtureKind.ColdStorage, FixtureKind.MeatDisplay, FixtureKind.CuttingTable];

    /// <summary>The seafood corner, in the order the bot buys it.</summary>
    private static readonly FixtureKind[] SeafoodFixtures = [FixtureKind.SeafoodCounter, FixtureKind.FishTank, FixtureKind.FishTank, FixtureKind.IceTable];

    /// <summary>The deli corner, in the order the bot buys it.</summary>
    private static readonly FixtureKind[] DeliFixtures = [FixtureKind.WokStove, FixtureKind.SteamTable, FixtureKind.RoastStation, FixtureKind.WokStove];

    /// <summary>Kitchen-only goods kept on hand for the roast station.</summary>
    private const int RoastStockTarget = 2;

    /// <summary>Which fixtures a role needs before the bot hires it.</summary>
    private static readonly Dictionary<string, FixtureKind[]> RoleNeeds = new()
    {
        ["butcher"] = MeatFixtures,
        ["fishmonger"] = [FixtureKind.FishTank, FixtureKind.SeafoodCounter],
        ["chef"] = [FixtureKind.WokStove, FixtureKind.SteamTable],
    };

    private readonly List<string> _notes = [];

    private readonly SortedDictionary<string, int> _rejections = new(StringComparer.Ordinal);

    public int RejectedCommands => _rejections.Values.Sum();

    /// <summary>Unexpected rejections by command and reason, e.g. "PlayerInteract/NothingToDo".</summary>
    public IReadOnlyDictionary<string, int> Rejections => _rejections;
    public int OrdersCutForSpace { get; private set; }

    /// <summary>Called after every tick the bot runs (tests use it to check state mid-play).</summary>
    public Action? AfterTick { get; set; }

    /// <summary>Receives the events of every tick the bot runs (the bot drains them).</summary>
    public Action<IReadOnlyList<SimEvent>>? EventsSeen { get; set; }

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
        PlaceSeafoodCorner(sim);
        if (runDeli && IsComplete(sim, SeafoodFixtures)) PlaceCorner(sim, DeliFixtures, besideKind: FixtureKind.WokStove);
        PlaceProduceStands(sim);
        HireUnlockedRoles(sim);
        AssignEmptySlots(sim);
        PlaceOrders(sim);
        PlanMenu(sim);
        Send(sim, new RestockAll());
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
        if (!IsBuiltOut(sim)) return;
        if (sim.Economy.CumulativeRevenueCents < next.RequiredCumulativeRevenueCents) return;
        if (sim.Economy.CashCents < next.UpgradeCostCents + cashReserveCents) return;

        if (Send(sim, new UpgradeStage()) is null) _notes.Add($"upgraded to stage {next.Id}");
    }

    /// <summary>Whether the meat corner is set up (once unlocked), so the next upgrade does not starve it of cash.</summary>
    private bool IsBuiltOut(GameSim sim) =>
        MeatFixtures.All(kind => !sim.Data.Fixtures.Any(f => f.Kind == kind && f.UnlockStage <= sim.Economy.Stage)
                                 || sim.Layout.Fixtures.Any(f => Def(sim.Data, f).Kind == kind));

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

    /// <summary>One produce stand a day, once the meat corner (and any unlocked seafood corner) is done, until there is a row of them.</summary>
    private void PlaceProduceStands(GameSim sim)
    {
        if (!MeatFixtures.All(kind => sim.Layout.Fixtures.Any(f => Def(sim.Data, f).Kind == kind))) return;
        if (!IsComplete(sim, SeafoodFixtures) || (runDeli && !IsComplete(sim, DeliFixtures))) return;
        if (sim.Layout.Fixtures.Count(f => Def(sim.Data, f).Kind == FixtureKind.ProduceStand) >= produceStands) return;
        if (sim.Data.Fixtures.FirstOrDefault(f => f.Kind == FixtureKind.ProduceStand && f.UnlockStage <= sim.Economy.Stage) is not { } def) return;
        if (sim.Economy.CashCents < def.PriceCents + cashReserveCents) return;
        if (FindSpot(sim, def, besideKind: FixtureKind.ProduceStand) is not { } spot) return;

        if (Send(sim, new PlaceFixture(def.Id, spot.Origin, spot.Rotation)) is null) _notes.Add($"placed {def.Id} at {spot.Origin.X},{spot.Origin.Y}");
    }

    /// <summary>
    /// The seafood corner one fixture a day, tanks next to each other. A second backroom comes first (it is free), so
    /// live deliveries are not crowded out by dry goods.
    /// </summary>
    private void PlaceSeafoodCorner(GameSim sim)
    {
        if (!sim.Data.Fixtures.Any(f => f.Kind == FixtureKind.FishTank && f.UnlockStage <= sim.Economy.Stage)) return;
        if (sim.Layout.Fixtures.Count(f => Def(sim.Data, f).Kind == FixtureKind.Backroom) < 2
            && sim.Data.Fixtures.First(f => f.Kind == FixtureKind.Backroom) is var backroom
            && FindSpot(sim, backroom) is { } room)
        {
            if (Send(sim, new PlaceFixture(backroom.Id, room.Origin, room.Rotation)) is null) _notes.Add($"placed {backroom.Id} at {room.Origin.X},{room.Origin.Y}");
        }

        PlaceCorner(sim, SeafoodFixtures, besideKind: FixtureKind.FishTank);
    }

    /// <summary>Whether every unlocked fixture of a corner is placed (as many of each kind as the list names).</summary>
    private static bool IsComplete(GameSim sim, FixtureKind[] corner) =>
        corner.GroupBy(k => k).All(g => !sim.Data.Fixtures.Any(f => f.Kind == g.Key && f.UnlockStage <= sim.Economy.Stage)
                                        || sim.Layout.Fixtures.Count(f => Def(sim.Data, f).Kind == g.Key) >= g.Count());

    /// <summary>Buys the next missing fixture of a corner (one a day), next to the corner's anchor kind if possible.</summary>
    private void PlaceCorner(GameSim sim, FixtureKind[] corner, FixtureKind besideKind)
    {
        var missing = corner
            .Where((kind, i) => sim.Layout.Fixtures.Count(f => Def(sim.Data, f).Kind == kind) < corner.Take(i + 1).Count(k => k == kind))
            .Select(kind => (FixtureKind?)kind)
            .FirstOrDefault();
        if (missing is not { } next) return;
        if (sim.Data.Fixtures.FirstOrDefault(f => f.Kind == next && f.UnlockStage <= sim.Economy.Stage) is not { } def) return;
        if (sim.Economy.CashCents < def.PriceCents + cashReserveCents) return;
        if (FindSpot(sim, def, besideKind) is not { } spot) return;

        if (Send(sim, new PlaceFixture(def.Id, spot.Origin, spot.Rotation)) is null) _notes.Add($"placed {def.Id} at {spot.Origin.X},{spot.Origin.Y}");
    }

    /// <summary>
    /// Each morning: every steam table slot gets a different dish, preferring the one that would cook the most
    /// near-expiry stock (then the one with the most ingredients); the roast station roasts duck, or char siu without
    /// ducks. Targets are two cookings.
    /// </summary>
    private void PlanMenu(GameSim sim)
    {
        var chosen = new HashSet<string>();
        foreach (var station in sim.Layout.Fixtures.Where(f => DataValidator.IsDeliStation(Def(sim.Data, f).Kind)).ToList())
        {
            var def = Def(sim.Data, station);
            for (var slot = 0; slot < def.Capacity; slot++)
            {
                var recipe = sim.Data.Recipes
                    .Where(r => r.Station == def.Kind && !chosen.Contains(r.Id) && sim.PortionsPossible(r.Id) > 0)
                    .OrderByDescending(r => r.Ingredients.Sum(x => sim.IngredientCandidates(x.ProductId).Where(c => sim.IsNearExpiry(c.Lot)).Sum(c => c.Lot.Quantity * c.Lot.UnitCostCents)))
                    .ThenByDescending(r => sim.PortionsPossible(r.Id))
                    .ThenBy(r => r.Id, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (recipe is null) continue;

                chosen.Add(recipe.Id);
                if (sim.Menu.GetValueOrDefault(new StockLocation(station.InstanceId, slot))?.DishId != recipe.Id)
                {
                    Send(sim, new SetMenuItem(station.InstanceId, slot, recipe.Id, 2 * recipe.Portions));
                }
            }
        }
    }

    /// <summary>
    /// The first legal spot, scanning from the right-hand side (where an upgrade adds floor); with
    /// <paramref name="besideKind"/>, a spot touching a fixture of that kind wins if there is one.
    /// </summary>
    private static (GridPoint Origin, Rotation Rotation)? FindSpot(GameSim sim, FixtureDef def, FixtureKind? besideKind = null)
    {
        var size = sim.Layout.Size;
        FixtureDef Lookup(string id) => sim.Data.Fixtures.Single(f => f.Id == id);
        var workers = sim.Workers.Select(w => w.Position).ToHashSet();
        var beside = sim.Layout.Fixtures
            .Where(f => besideKind is { } kind && Def(sim.Data, f).Kind == kind)
            .SelectMany(f => LayoutRules.Footprint(Def(sim.Data, f), f.Origin, f.Rotation))
            .ToHashSet();
        (GridPoint, Rotation)? first = null;

        for (var x = size.W - 1; x >= 0; x--)
        for (var y = 0; y < size.H; y++)
        foreach (var rotation in Enum.GetValues<Rotation>())
        {
            var candidate = new PlacedFixture(sim.Layout.NextInstanceId, def.Id, new GridPoint(x, y), def.PriceCents, rotation);
            if (LayoutRules.Check(size, [.. sim.Layout.Fixtures, candidate], Lookup, candidate.InstanceId) is not null) continue;
            var footprint = LayoutRules.Footprint(def, candidate.Origin, rotation).ToList();
            if (footprint.Any(workers.Contains)) continue;
            if (footprint.Any(c => beside.Any(b => Math.Abs(b.X - c.X) + Math.Abs(b.Y - c.Y) == 1))) return (candidate.Origin, rotation);
            first ??= (candidate.Origin, rotation);
            if (beside.Count == 0) return first;
        }

        return first;
    }

    private void HireUnlockedRoles(GameSim sim)
    {
        foreach (var role in sim.Data.Employees.Roles.Where(r => r.UnlockStage <= sim.Economy.Stage))
        {
            if (sim.Workers.Any(w => w.RoleId == role.Id)) continue;

            // Only hire once there is somewhere to work.
            var needs = RoleNeeds.GetValueOrDefault(role.Id, []);
            if (!needs.All(kind => sim.Layout.Fixtures.Any(f => Def(sim.Data, f).Kind == kind))) continue;
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

            // Sell what can be bought or cut, but never a whole primal; produce goes on produce stands and only there.
            var candidates = data.Products
                .Where(p => def.Storage.Contains(p.Storage) && !primals.Contains(p.Id) && (p.Orderable || producible.Contains(p.Id)))
                .Where(p => (p.Department == Department.Produce) == (def.Kind == FixtureKind.ProduceStand))
                .Where(p => (p.Department == Department.Seafood) == (def.Kind is FixtureKind.FishTank or FixtureKind.IceTable))
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
        // Fresh goods first, so dry goods do not crowd them out of the backroom. Produce and clams spoil, so only two
        // fills of their slots; live seafood dies if left in storage, so just what its tank slots hold.
        var products = sim.Shelves.Assigned.Values.Distinct().Where(orderable.Contains)
            .Select(id => sim.Data.Products.Single(p => p.Id == id))
            .OrderBy(p => p.Department is not (Department.Produce or Department.Seafood))
            .ThenBy(p => p.Id, StringComparer.Ordinal);
        foreach (var product in products)
        {
            var fills = sim.Shelves.Assigned.Values.Count(id => id == product.Id) * product.UnitsPerShelfSlot;
            var target = product.Live is not null ? fills : product.FreshnessDays is not null ? 2 * fills : stockTarget;
            TopUp(sim, product.Id, target);
        }

        if (sim.Layout.Fixtures.Any(f => Def(sim.Data, f).Kind == FixtureKind.RoastStation))
        {
            foreach (var recipe in sim.Data.Recipes.Where(r => r.Station == FixtureKind.RoastStation))
            {
                foreach (var x in recipe.Ingredients) TopUp(sim, x.ProductId, RoastStockTarget * x.Quantity);
            }
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
                KillFishTask k => k.CounterInstanceId,
                CookTask c => c.Target.FixtureInstanceId,
                _ => null,
            }
            : sim.Tasks.OpenTasks.FirstOrDefault() switch
            {
                CheckoutTask c => c.CheckoutInstanceId,
                RestockTask r => r.Source.FixtureInstanceId,
                CutMeatTask c => c.Source.FixtureInstanceId,
                TidyTask t => t.StandInstanceId,
                CleanTankTask c => c.TankInstanceId,
                RemoveDeadTask r => r.TankInstanceId,
                KillFishTask k => k.Source.FixtureInstanceId,
                CookTask c => c.StoveInstanceId,
                ServeDeliTask s => s.StationInstanceId,
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
        var events = sim.DrainEvents();
        EventsSeen?.Invoke(events);
        var rejected = events.OfType<CommandRejected>().ToList();
        // Expected outcomes, not problems: an order halved to fit storage, or shelves already full.
        foreach (var r in rejected.Where(r => r.Reason != RejectReason.StorageFull && r.Command is not RestockAll))
        {
            var key = $"{r.Command.GetType().Name}/{r.Reason}";
            _rejections[key] = _rejections.GetValueOrDefault(key) + 1;
        }

        return rejected;
    }

    private static FixtureDef Def(GameData data, PlacedFixture fixture) => data.Fixtures.Single(f => f.Id == fixture.FixtureId);
}
