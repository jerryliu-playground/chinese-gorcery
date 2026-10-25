using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Tests.Seafood;
using static ChineseGrocery.Sim.Tests.Seafood.SeafoodScenario;

namespace ChineseGrocery.Sim.Tests.Deli;

/// <summary>
/// A stage 1 store with every later fixture and role unlocked and plenty of cash (data tweak). Tests use: a stove at
/// (7,3) worked from (7,4), a steam table at (8,5) worked from (8,4) with customers at (8,6), and a roast station at
/// (10,5) worked from (10,4) with customers at (10,6).
/// </summary>
public static class DeliScenario
{
    public static GameData Unlocked(GameData data) => SeafoodScenario.Unlocked(data) with
    {
        Fixtures = SeafoodScenario.Unlocked(data).Fixtures.Select(f => f with { UnlockStage = 1 }).ToList(),
    };

    public static GameSim DeliStore(GameData? data = null, ulong seed = 1) => SimTestKit.NewSim(seed, Unlocked(data ?? SimTestKit.Data));

    public static int Stove(GameSim sim) => Place(sim, "wok_stove", 7, 3);

    public static int SteamTable(GameSim sim) => Place(sim, "steam_table", 8, 5);

    public static int RoastStation(GameSim sim) => Place(sim, "roast_station", 10, 5);

    /// <summary>Puts a lot that arrived <paramref name="ageDays"/> ago straight into a location (test shortcut).</summary>
    public static StockLot Put(GameSim sim, StockLocation where, string productId, int units, int ageDays = 0)
    {
        var p = Def(sim, productId);
        var lot = new StockLot(productId, units, sim.Clock.Day + p.ShelfLifeDays - 1 - ageDays, p.CostCents > 0 ? p.CostCents : 100);
        sim.Inventory.GetOrCreate(where).Add(lot);
        return lot;
    }

    public static StockLocation Backroom(GameSim sim) => StockLocation.Storage(sim.InstanceOf("backroom"));
}
