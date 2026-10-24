using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Tasks;
using static ChineseGrocery.Sim.Tests.Meat.MeatScenario;

namespace ChineseGrocery.Sim.Tests.Tasks;

/// <summary>Changing your mind while carrying: put the goods back in storage (playtest feedback, 2026-10-24).</summary>
public class PutBackTests
{
    private const string Rice = "rice_jasmine";
    private static readonly GridPoint BackroomCell = new(10, 2);

    [Fact]
    public void Carried_goods_go_back_and_the_task_is_open_again()
    {
        var sim = SimTestKit.NewSim();
        sim.Run(new PlaceOrder(Rice, 20));
        sim.RunFullDay();
        sim.Run(new AssignShelfSlot(sim.InstanceOf("shelf"), 0, Rice));
        sim.WalkPlayerTo(BackroomCell);
        sim.Run(new PlayerInteract());
        TickUntil(sim, () => sim.Player.Step == WorkStep.Carrying);
        var taskId = sim.Player.TaskId!.Value;
        var carried = sim.Player.Carrying.Sum(l => l.Quantity);
        sim.DrainEvents();

        sim.Run(new PlayerInteract());

        var backroom = StockLocation.Storage(sim.InstanceOf("backroom"));
        Assert.Equal(20, sim.Inventory.Count(backroom, Rice));
        Assert.Empty(sim.Player.Carrying);
        Assert.Equal(WorkStep.Idle, sim.Player.Step);
        Assert.Null(sim.Tasks.ClaimedBy(taskId));
        Assert.Equal(new GoodsReturned(sim.Player.Id, Rice, carried, backroom), sim.DrainEvents().Single<GoodsReturned>());
    }

    [Fact]
    public void A_primal_goes_back_to_cold_storage()
    {
        var sim = Stage2(primals: 1);
        sim.Tick();
        WalkTo(sim, ColdStorageCell);
        sim.Run(new PlayerInteract());
        TickUntil(sim, () => sim.Player.Step == WorkStep.Carrying);

        sim.Run(new PlayerInteract());
        sim.Tick();

        Assert.Equal(1, sim.Inventory.Count(ColdStorage(sim), Primal));
        Assert.Single(sim.Tasks.Tasks.OfType<CutMeatTask>(), t => sim.Tasks.ClaimedBy(t.Id) is null);
    }

    [Fact]
    public void Goods_only_go_back_where_they_can_be_kept()
    {
        var sim = Stage2(primals: 1);
        sim.Tick();
        WalkTo(sim, ColdStorageCell);
        sim.Run(new PlayerInteract());
        TickUntil(sim, () => sim.Player.Step == WorkStep.Carrying);
        WalkTo(sim, BackroomCell);
        sim.DrainEvents();

        sim.Run(new PlayerInteract());

        Assert.Equal(RejectReason.NothingToDo, sim.DrainEvents().Single<CommandRejected>().Reason);
        Assert.Equal(Primal, Assert.Single(sim.Player.Carrying).ProductId);
    }
}
