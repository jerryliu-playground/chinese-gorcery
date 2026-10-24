using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;

namespace ChineseGrocery.Sim.Tests.Seafood;

/// <summary>
/// A stage 1 store with the seafood fixtures and the fishmonger unlocked and plenty of cash (data tweak), so tests need
/// no upgrade.
/// Free floor used by the tests: tanks at (7,3) and (8,3) worked from the row below, an ice table at (6,3), a seafood
/// counter at (3,3) worked from (3,2) with customers at (3,4), and cold storage at (9,4) worked from (9,6).
/// </summary>
public static class SeafoodScenario
{
    public const string Fish = "live_fish";
    public const string Shrimp = "live_shrimp";
    public const string Crab = "crab";
    public const string Lobster = "lobster";
    public const string ChilledFish = "chilled_fish";
    public const string ChilledLobster = "chilled_lobster";

    public static GameData Unlocked(GameData data) => data with
    {
        Economy = data.Economy with { StartingCashCents = 1_000_000 },
        Fixtures = data.Fixtures.Select(f => f.Kind is FixtureKind.FishTank or FixtureKind.IceTable or FixtureKind.SeafoodCounter or FixtureKind.ColdStorage ? f with { UnlockStage = 1 } : f).ToList(),
        Employees = data.Employees with { Roles = data.Employees.Roles.Select(r => r with { UnlockStage = 1 }).ToList() },
    };

    public static GameSim Store(GameData? data = null, ulong seed = 1) => SimTestKit.NewSim(seed, Unlocked(data ?? SimTestKit.Data));

    public static ProductDef Def(GameSim sim, string id) => sim.Data.Products.Single(p => p.Id == id);

    /// <summary>Places a fixture (failing the test if it is rejected) and assigns its slots.</summary>
    public static int Place(GameSim sim, string fixtureId, int x, int y, params string?[] products)
    {
        sim.DrainEvents();
        sim.Run(new PlaceFixture(fixtureId, new GridPoint(x, y)));
        Assert.Empty(sim.DrainEvents().OfType<CommandRejected>());
        var id = sim.Layout.Fixtures[^1].InstanceId;
        for (var slot = 0; slot < products.Length; slot++)
        {
            if (products[slot] is { } p) sim.Run(new AssignShelfSlot(id, slot, p));
        }

        return id;
    }

    public static int Tank(GameSim sim, int x, params string?[] products) => Place(sim, "fish_tank", x, 3, products);

    /// <summary>Puts animals that arrived today straight into a slot (test shortcut).</summary>
    public static void Stock(GameSim sim, int fixture, int slot, string productId, int units)
    {
        var p = Def(sim, productId);
        sim.Inventory.GetOrCreate(new StockLocation(fixture, slot)).Add(new StockLot(productId, units, sim.Clock.Day + p.ShelfLifeDays - 1, p.CostCents));
    }

    public static StockLot DeadLot(GameSim sim, string productId, int units) => new(productId, units, 999, Def(sim, productId).CostCents);

    public static int LiveUnits(GameSim sim, int tank) =>
        sim.Inventory.Locations.Where(p => p.Location.FixtureInstanceId == tank).Sum(p => p.Lots.Sum(l => l.Quantity));

    public static void TickFor(GameSim sim, int ticks)
    {
        for (var i = 0; i < ticks; i++) sim.Tick();
    }
}
