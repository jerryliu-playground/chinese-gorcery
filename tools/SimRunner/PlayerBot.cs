using ChineseGrocery.Sim.Calendar;
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
/// menu each morning and keeps ducks and pork shoulder on hand. With <c>festivalStock</c> it keeps one extra shelf for
/// festival goods: before each festival (inside the order window) it gives the shelf's empty slots to that festival's
/// goods and orders them, and hangs the festival's decorations through its prep days. It takes clearance offers and
/// group orders it can fill, and rents a generator when the power is going out on chilled goods or tanks.
/// While open it works the highest-priority open task.
/// Runner policy only, not a game rule.
/// </summary>
public sealed class PlayerBot(int stockTarget, int primalsOnHand = 1, long cashReserveCents = 50_000, int produceStands = 4, bool runDeli = true, bool autoStaff = false, bool festivalStock = true, int cashiers = 0)
{
    /// <summary>Fills of the festival shelf's slots the bot orders of each festival good.</summary>
    private const int FestivalFills = 2;

    /// <summary>The bot stops ordering a festival's goods this many days before it.</summary>
    private const int FestivalLastOrderDays = 3;


    private int? _festivalShelf;
    private const int MaxTicksPerDay = 3 * 24 * 60 * 60;
    private const int MaxPreOpenTicks = 12_000;

    private static readonly FixtureKind[] MeatFixtures = [FixtureKind.ColdStorage, FixtureKind.MeatDisplay, FixtureKind.CuttingTable];

    /// <summary>The seafood corner, in the order the bot buys it.</summary>
    private static readonly FixtureKind[] SeafoodFixtures = [FixtureKind.SeafoodCounter, FixtureKind.FishTank, FixtureKind.ShellfishTank, FixtureKind.IceTable];

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
        StockForFestivals(sim);
        AssignEmptySlots(sim);
        AnswerEvents(sim);
        if (autoStaff)
        {
            StaffTheStore(sim);
            GiveRestDays(sim);
        }
        PlaceOrders(sim);
        if (!IsAutomated(sim, Department.Deli)) PlanMenu(sim);
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
        // Cashiers on request, so the player is not the only one at the till (a busy store loses half its shoppers in line).
        if (!autoStaff && sim.Workers.Count(w => w.RoleId == "cashier") < cashiers && Send(sim, new HireEmployee("cashier")) is null) _notes.Add("hired cashier");

        foreach (var role in sim.Data.Employees.Roles.Where(r => r.UnlockStage <= sim.Economy.Stage))
        {
            if (sim.Workers.Any(w => w.RoleId == role.Id)) continue;

            // Only the specialists, and only once there is somewhere to work.
            if (!RoleNeeds.TryGetValue(role.Id, out var needs)) continue;
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
            if (!DataValidator.IsDisplay(def.Kind) || fixture.InstanceId == FestivalShelf(sim)) continue;

            // Sell what can be bought or cut, but never a whole primal or a festival good; produce goes on produce stands and only there.
            var candidates = data.Products
                .Where(p => def.Storage.Contains(p.Storage) && !primals.Contains(p.Id) && (p.Orderable || producible.Contains(p.Id)) && !sim.IsFestivalGood(p.Id))
                .Where(p => (p.Department == Department.Produce) == (def.Kind == FixtureKind.ProduceStand))
                .Where(p => (p.Department == Department.Seafood) == (DataValidator.IsTank(def.Kind) || def.Kind == FixtureKind.IceTable))
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

    /// <summary>The shelf kept for festival goods: the one the bot bought, else one with a slot given to a festival good.</summary>
    private int? FestivalShelf(GameSim sim)
    {
        if (_festivalShelf is { } id && sim.Layout.Find(id) is not null) return id;

        _festivalShelf = sim.Shelves.Assigned.Where(kv => sim.IsFestivalGood(kv.Value)).Select(kv => (int?)kv.Key.FixtureInstanceId).Min();
        return _festivalShelf;
    }

    /// <summary>
    /// The next festival with goods, while its goods can be ordered: a shelf for them (bought once), its free slots
    /// given to the goods (not produce), the goods ordered up to two fills; and the festival's decorations hung from the
    /// first prep day to the day itself, stored again after. Without <c>festivalStock</c> the shelf is still bought (so
    /// both runs of a comparison have the same floor) but left empty, and nothing is decorated.
    /// </summary>
    private void StockForFestivals(GameSim sim)
    {
        if (sim.Data.Festivals is not { } festivals || sim.Data.Calendar is null) return;

        var upcoming = festivals.Festivals
            .Where(f => f.Goods.Count > 0 && sim.DaysUntil(f) is var d && d >= 1 && d <= festivals.OrderWindowDays)
            .OrderBy(sim.DaysUntil)
            .FirstOrDefault();
        if (upcoming is not null)
        {
            if (FestivalShelf(sim) is null) BuyFestivalShelf(sim);
            if (festivalStock && FestivalShelf(sim) is { } shelf)
            {
                var goods = upcoming.Goods.Where(id => sim.Data.Products.Single(p => p.Id == id).Department != Department.Produce).ToList();
                var def = Def(sim.Data, sim.Layout.Find(shelf)!);
                for (var slot = 0; slot < def.Capacity && goods.Count > 0; slot++)
                {
                    var location = new StockLocation(shelf, slot);
                    var current = sim.Shelves.ProductAt(location);
                    if (current is not null && (goods.Contains(current) || sim.Inventory.Count(location, current) > 0)) continue;

                    Send(sim, new AssignShelfSlot(shelf, slot, goods[slot % goods.Count]));
                }

                // The last orders go in three days ahead, so little is left over after the day.
                foreach (var id in goods.Where(_ => sim.DaysUntil(upcoming) >= FestivalLastOrderDays))
                {
                    var slots = sim.Shelves.Assigned.Count(kv => kv.Key.FixtureInstanceId == shelf && kv.Value == id);
                    if (slots > 0) TopUp(sim, id, FestivalFills * slots * sim.Data.Products.Single(p => p.Id == id).UnitsPerShelfSlot);
                }
            }
        }

        var around = sim.FestivalAround(sim.Clock.Day);
        foreach (var deco in festivalStock ? festivals.Decorations : [])

        {
            var wanted = around is { } a && a.Offset <= 0 && deco.Festivals.Contains(a.Festival.Id);
            var owned = sim.Decorations.FirstOrDefault(d => d.DecorationId == deco.Id);
            if (wanted && owned is null && sim.Economy.CashCents >= deco.CostCents + cashReserveCents && Send(sim, new BuyDecoration(deco.Id)) is null)
            {
                owned = new OwnedDecoration(deco.Id, null);
                _notes.Add($"bought {deco.Id}");
            }

            if (wanted && owned is { WallX: null }) Send(sim, new HangDecoration(deco.Id));
            if (!wanted && owned is { WallX: not null }) Send(sim, new StoreDecoration(deco.Id));
        }
    }

    private void BuyFestivalShelf(GameSim sim)
    {
        var def = sim.Data.Fixtures.First(f => f.Kind == FixtureKind.Shelf);
        if (sim.Economy.CashCents < def.PriceCents + cashReserveCents || FindSpot(sim, def) is not { } spot) return;
        if (Send(sim, new PlaceFixture(def.Id, spot.Origin, spot.Rotation)) is not null) return;

        _festivalShelf = sim.Layout.Fixtures[^1].InstanceId;
        _notes.Add($"placed festival shelf at {spot.Origin.X},{spot.Origin.Y}");
    }

    /// <summary>
    /// Before opening: a clearance offer is taken if the product is below its target and it fits the cash reserve; a
    /// group order if twice its units are on hand or on order; a generator if the power cut would hit chilled goods or tanks.
    /// </summary>
    private void AnswerEvents(GameSim sim)
    {
        foreach (var e in sim.StoreEvents.ToList())
        {
            var def = sim.EventDefOf(e);
            if (e.Status == StoreEventStatus.Offered && def.Kind == EventKind.ClearanceOffer)
            {
                var units = e.Goods![0].Quantity;
                var below = OnHand(sim, e.ProductId!) < (Targets(sim).TryGetValue(e.ProductId!, out var t) ? t : 0);
                var take = below && sim.Economy.CashCents >= units * e.UnitCostCents + cashReserveCents;
                if (Send(sim, new AnswerOffer(e.Id, take)) is null && take) _notes.Add($"took clearance {e.ProductId} x{units}");
            }
            else if (e.Status == StoreEventStatus.Offered && def.Kind == EventKind.GroupOrder)
            {
                var take = e.Goods!.All(g => OnHand(sim, g.ProductId) >= 2 * g.Quantity);
                if (Send(sim, new AnswerOffer(e.Id, take)) is null && take) _notes.Add($"took group order {string.Join("+", e.Goods!.Select(g => $"{g.ProductId}x{g.Quantity}"))}");
            }
            else if (def.PowerCut is { } cut && e.Status == StoreEventStatus.Announced && e.StartDay == sim.Clock.Day && !e.GeneratorRented
                     && sim.Economy.CashCents >= cut.GeneratorCostCents + cashReserveCents
                     && (sim.Inventory.Locations.SelectMany(l => l.Lots).Any(l => sim.Data.Products.Single(p => p.Id == l.ProductId).Storage == StorageType.Chilled)
                         || sim.Layout.Fixtures.Any(f => DataValidator.IsTank(Def(sim.Data, f).Kind))))
            {
                if (Send(sim, new RentGenerator(e.Id)) is null) _notes.Add("rented a generator");
            }
        }
    }

    private static int OnHand(GameSim sim, string productId) =>
        sim.Inventory.Total(productId) + sim.PendingOrders.Where(o => o.ProductId == productId).Sum(o => o.Quantity);

    /// <summary>The departments a manager runs, so the bot leaves their ordering and menu alone.</summary>
    private static bool IsAutomated(GameSim sim, Department department) =>
        sim.Automation.Any(a => a.Value.AutoOrder && sim.ManagerOf(a.Key) is not null && GameSim.AreaOf(a.Key).Contains(department));

    private static readonly Department[] ManagedAreas = [Department.DryGoods, Department.Produce, Department.Meat, Department.Seafood, Department.Deli];

    /// <summary>
    /// --auto: a cashier and a stocker from the start, then once managers unlock, one per area with automatic ordering
    /// and pricing, ordering up to the same targets the bot would use.
    /// </summary>
    private void StaffTheStore(GameSim sim)
    {
        foreach (var roleId in new[] { "cashier", "stocker" })
        {
            if (!sim.Workers.Any(w => w.RoleId == roleId) && Send(sim, new HireEmployee(roleId)) is null) _notes.Add($"hired {roleId}");
        }

        foreach (var area in ManagedAreas)
        {
            if (sim.ManagerOf(area) is null)
            {
                if (Send(sim, new HireEmployee("manager")) is not null) continue;
                var manager = sim.Workers.Last(w => w.RoleId == "manager");
                Send(sim, new AssignManager(manager.Id, area));
                Send(sim, new SetAutomation(area, AutoOrder: true, AutoPrice: true));
                _notes.Add($"manager for {area}");
            }

            foreach (var (productId, target) in Targets(sim).Where(t => GameSim.AreaOf(area).Contains(sim.Data.Products.Single(p => p.Id == t.Key).Department)))
            {
                if (sim.OrderTargets.GetValueOrDefault(productId) != target) Send(sim, new SetOrderTarget(productId, target));
            }
        }
    }

    /// <summary>--auto: every staff member gets one day off a week, spread over the week, so no one burns out.</summary>
    private void GiveRestDays(GameSim sim)
    {
        foreach (var worker in sim.Workers.Where(w => w.RoleId is not null && w.RestDays.Count == 0))
        {
            Send(sim, new SetRestDays(worker.Id, [worker.Id % 7]));
        }
    }

    /// <summary>What the bot keeps on hand of each product it orders.</summary>
    private Dictionary<string, int> Targets(GameSim sim)
    {
        var targets = new Dictionary<string, int>();
        foreach (var id in sim.Shelves.Assigned.Values.Distinct())
        {
            var product = sim.Data.Products.Single(p => p.Id == id);
            if (!product.Orderable || sim.IsFestivalGood(id)) continue;

            var fills
 = sim.Shelves.Assigned.Values.Count(x => x == id) * product.UnitsPerShelfSlot;
            targets[id] = product.Live is not null ? fills : product.FreshnessDays is not null ? 2 * fills : stockTarget;
        }

        if (sim.Layout.Fixtures.Any(f => Def(sim.Data, f).Kind == FixtureKind.CuttingTable))
        {
            foreach (var cut in sim.Data.MeatCuts) targets[cut.PrimalId] = primalsOnHand;
        }

        if (sim.Layout.Fixtures.Any(f => Def(sim.Data, f).Kind == FixtureKind.RoastStation))
        {
            foreach (var x in sim.Data.Recipes.Where(r => r.Station == FixtureKind.RoastStation).SelectMany(r => r.Ingredients)) targets[x.ProductId] = RoastStockTarget * x.Quantity;
        }

        return targets;
    }

    private void PlaceOrders(GameSim sim)
    {
        var orderable = sim.Data.Products.Where(p => p.Orderable).Select(p => p.Id).ToHashSet();
        // Fresh goods first, so dry goods do not crowd them out of the backroom. Produce and clams spoil, so only two
        // fills of their slots; live seafood dies if left in storage, so just what its tank slots hold.
        var products = sim.Shelves.Assigned.Values.Distinct().Where(id => orderable.Contains(id) && !sim.IsFestivalGood(id))
            .Select(id => sim.Data.Products.Single(p => p.Id == id))
            .OrderBy(p => p.Department is not (Department.Produce or Department.Seafood))
            .ThenBy(p => p.Id, StringComparer.Ordinal);
        foreach (var product in products.Where(p => !IsAutomated(sim, p.Department)))
        {
            var fills = sim.Shelves.Assigned.Values.Count(id => id == product.Id) * product.UnitsPerShelfSlot;
            var target = product.Live is not null ? fills : product.FreshnessDays is not null ? 2 * fills : stockTarget;
            TopUp(sim, product.Id, target);
        }

        if (sim.Layout.Fixtures.Any(f => Def(sim.Data, f).Kind == FixtureKind.RoastStation))
        {
            foreach (var recipe in sim.Data.Recipes.Where(r => r.Station == FixtureKind.RoastStation && !IsAutomated(sim, Department.Meat)))
            {
                foreach (var x in recipe.Ingredients) TopUp(sim, x.ProductId, RoastStockTarget * x.Quantity);
            }
        }

        if (sim.Layout.Fixtures.Any(f => Def(sim.Data, f).Kind == FixtureKind.CuttingTable))
        {
            foreach (var cut in sim.Data.MeatCuts)
            {
                var primal = sim.Data.Products.Single(p => p.Id == cut.PrimalId);
                if (StorageRouting.FindDestination(sim.Data, sim.Layout.Fixtures, primal) is not null && !IsAutomated(sim, Department.Meat)) TopUp(sim, cut.PrimalId, primalsOnHand);
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
