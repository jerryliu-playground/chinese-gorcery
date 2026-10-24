using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Meat;
using ChineseGrocery.Sim.Tasks;
using static ChineseGrocery.Sim.Tests.Meat.MeatScenario;

namespace ChineseGrocery.Sim.Tests.Meat;

public class CutMeatTaskTests
{
    private static void PlayerCutsOne(GameSim sim)
    {
        WalkTo(sim, ColdStorageCell);
        sim.Run(new PlayerInteract());
        TickUntil(sim, () => sim.Player.Step == WorkStep.Carrying);
        WalkTo(sim, TableCell);
        sim.Run(new PlayerInteract());
        TickUntil(sim, () => sim.Player.Step == WorkStep.Idle);
    }

    [Fact]
    public void Each_primal_in_cold_storage_gets_a_cut_task()
    {
        var sim = Stage2(primals: 2);

        sim.Tick();

        var tasks = sim.Tasks.Tasks.OfType<CutMeatTask>().ToList();
        Assert.Equal(2, tasks.Count);
        Assert.All(tasks, t =>
        {
            Assert.Equal(ColdStorage(sim), t.Source);
            Assert.Equal(sim.InstanceOf("cutting_table"), t.CuttingTableInstanceId);
            Assert.Equal(SimTestKit.Data.Economy.Tasks.PriorityOf(TaskType.CutMeat), t.Priority);
        });
    }

    [Fact]
    public void No_cut_task_without_a_cutting_table()
    {
        var sim = Stage2(withTable: false);

        sim.Tick();

        Assert.Empty(sim.Tasks.Tasks.OfType<CutMeatTask>());
    }

    [Fact]
    public void Player_cuts_a_primal_into_boxes_back_in_cold_storage()
    {
        var sim = Stage2(primals: 1);
        sim.Tick();
        var primalCost = SimTestKit.Data.Products.Single(p => p.Id == Primal).CostCents;
        var expected = MeatCutting.Cut(
            SimTestKit.Data.MeatCuts.Single(), id => SimTestKit.Data.Products.Single(p => p.Id == id),
            SimTestKit.Data.Employees.Player.KnifeSkill, primalCost);
        var lossBefore = sim.Economy.Today.LossCents;

        PlayerCutsOne(sim);

        var cold = sim.Inventory.Get(ColdStorage(sim));
        Assert.Equal(0, cold.Count(Primal));
        Assert.All(expected.Outputs, o => Assert.Equal(o.Packages, cold.Count(o.ProductId)));
        Assert.All(cold.Lots, l => Assert.Equal(expected.Outputs.Single(o => o.ProductId == l.ProductId).UnitCostCents, l.UnitCostCents));
        Assert.Equal(lossBefore + expected.LossCents, sim.Economy.Today.LossCents);
        Assert.Empty(sim.Tasks.Tasks.OfType<CutMeatTask>());
        var cut = sim.DrainEvents().Single<MeatCut>();
        Assert.Equal((Primal, sim.Player.Id), (cut.PrimalId, cut.WorkerId));
    }

    [Fact]
    public void Cut_meat_keeps_its_own_shelf_life_from_the_cutting_day()
    {
        var sim = Stage2();
        sim.Tick();

        PlayerCutsOne(sim);

        var belly = sim.Inventory.Get(ColdStorage(sim)).Lots.First(l => l.ProductId == "pork_belly");
        Assert.Equal(sim.Clock.Day + SimTestKit.Data.Products.Single(p => p.Id == "pork_belly").ShelfLifeDays - 1, belly.BestBeforeDay);
    }

    [Fact]
    public void Carrying_a_primal_the_player_can_only_cut_it_or_put_it_back()
    {
        var sim = Stage2();
        sim.Tick();
        WalkTo(sim, ColdStorageCell);
        sim.Run(new PlayerInteract());
        TickUntil(sim, () => sim.Player.Step == WorkStep.Carrying);
        WalkTo(sim, new GridPoint(1, 2));
        sim.DrainEvents();

        // The front of a shelf: neither the cutting table nor a storage area.
        sim.Run(new PlayerInteract());

        Assert.Equal(RejectReason.NothingToDo, sim.DrainEvents().Single<CommandRejected>().Reason);
        Assert.Equal(Primal, Assert.Single(sim.Player.Carrying).ProductId);
    }

    [Fact]
    public void Picked_up_primal_is_not_counted_again()
    {
        var sim = Stage2(primals: 2);
        sim.Tick();
        WalkTo(sim, ColdStorageCell);
        sim.Run(new PlayerInteract());
        TickUntil(sim, () => sim.Player.Step == WorkStep.Carrying);

        sim.Tick();

        Assert.Equal(2, sim.Tasks.Tasks.OfType<CutMeatTask>().Count());
    }

    [Fact]
    public void Cutting_table_in_use_cannot_be_sold()
    {
        var sim = Stage2();
        sim.Tick();
        WalkTo(sim, ColdStorageCell);
        sim.Run(new PlayerInteract());
        TickUntil(sim, () => sim.Player.Step == WorkStep.Carrying);
        sim.DrainEvents();

        sim.Run(new SellFixture(sim.InstanceOf("cutting_table")));

        Assert.Equal(RejectReason.FixtureInUse, sim.DrainEvents().Single<CommandRejected>().Reason);
    }
}
