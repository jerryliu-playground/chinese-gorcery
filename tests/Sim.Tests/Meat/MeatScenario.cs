using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Tasks;

namespace ChineseGrocery.Sim.Tests.Meat;

/// <summary>
/// A stage 2 store with meat fixtures: cold storage 2x2 at (14,0) worked from (14,2), meat display at (14,4) worked
/// from (14,5)/(15,5), cutting table at (14,7) worked from (14,8). Primals arrive by a normal order.
/// </summary>
public static class MeatScenario
{
    public const string Primal = "pork_primal";
    public static readonly GridPoint ColdStorageCell = new(14, 2);
    public static readonly GridPoint TableCell = new(14, 8);

    public static GameSim Stage2(int primals = 1, bool withTable = true, ulong seed = 1)
    {
        var sim = SimTestKit.NewSim(seed);
        sim.Economy.Earn("rice_jasmine", 1, SimTestKit.Data.Stages[1].RequiredCumulativeRevenueCents);
        sim.Run(new UpgradeStage());
        sim.Run(new PlaceFixture("cold_storage", new GridPoint(14, 0)));
        sim.Run(new PlaceFixture("meat_display", new GridPoint(14, 4)));
        if (withTable) sim.Run(new PlaceFixture("cutting_table", new GridPoint(14, 7)));
        if (primals > 0)
        {
            sim.Run(new PlaceOrder(Primal, primals));
            sim.RunFullDay();
        }

        sim.DrainEvents();
        return sim;
    }

    public static StockLocation ColdStorage(GameSim sim) => StockLocation.Storage(sim.InstanceOf("cold_storage"));

    /// <summary>Walks the player to a cell, ticking until they arrive.</summary>
    public static void WalkTo(GameSim sim, GridPoint cell)
    {
        sim.Run(new MovePlayer(cell));
        TickUntil(sim, () => sim.Player.Path.Count == 0);
    }

    public static void TickUntil(GameSim sim, Func<bool> done, int max = 20_000)
    {
        for (var i = 0; !done(); i++)
        {
            if (i == max) throw new InvalidOperationException("Condition never met.");
            sim.Tick();
        }
    }
}
