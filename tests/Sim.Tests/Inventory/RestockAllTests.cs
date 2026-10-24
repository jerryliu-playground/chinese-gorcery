using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Tasks;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Tests.Inventory;

/// <summary>One-click shelving before opening (playtest feedback, 2026-10-24).</summary>
public class RestockAllTests
{
    private const string Rice = "rice_jasmine";
    private const string Noodles = "noodles_instant";
    private static int PerSlot(string id) => SimTestKit.Data.Products.Single(p => p.Id == id).UnitsPerShelfSlot;

    /// <summary>Day 2 before opening: rice and noodles in the backroom, first shelf's slots 0/1 assigned to them.</summary>
    private static (GameSim Sim, StockLocation RiceSlot, StockLocation NoodleSlot, StockLocation Backroom) Stocked(int rice = 20, int noodles = 30)
    {
        var sim = SimTestKit.NewSim();
        if (rice > 0) sim.Run(new PlaceOrder(Rice, rice));
        if (noodles > 0) sim.Run(new PlaceOrder(Noodles, noodles));
        sim.RunFullDay();
        var shelf = sim.InstanceOf("shelf");
        sim.Run(new AssignShelfSlot(shelf, 0, Rice), new AssignShelfSlot(shelf, 1, Noodles));
        sim.DrainEvents();
        return (sim, new StockLocation(shelf, 0), new StockLocation(shelf, 1), StockLocation.Storage(sim.InstanceOf("backroom")));
    }

    [Fact]
    public void Fills_every_assigned_slot_from_storage()
    {
        var (sim, riceSlot, noodleSlot, backroom) = Stocked();

        sim.Run(new RestockAll());

        Assert.Equal(PerSlot(Rice), sim.Inventory.Count(riceSlot, Rice));
        Assert.Equal(PerSlot(Noodles), sim.Inventory.Count(noodleSlot, Noodles));
        Assert.Equal(20 - PerSlot(Rice), sim.Inventory.Count(backroom, Rice));
        Assert.Equal(30 - PerSlot(Noodles), sim.Inventory.Count(backroom, Noodles));
        Assert.Equal(new ShelvesRestocked(PerSlot(Rice) + PerSlot(Noodles)), sim.DrainEvents().Single<ShelvesRestocked>());
    }

    [Fact]
    public void Takes_what_there_is_when_storage_runs_short()
    {
        var (sim, riceSlot, _, backroom) = Stocked(rice: 2);

        sim.Run(new RestockAll());

        Assert.Equal(2, sim.Inventory.Count(riceSlot, Rice));
        Assert.Equal(0, sim.Inventory.Count(backroom, Rice));
    }

    [Fact]
    public void Leaves_no_restock_task_behind()
    {
        var (sim, _, _, _) = Stocked();
        sim.Tick();
        Assert.NotEmpty(sim.Tasks.Tasks.OfType<RestockTask>());

        sim.Run(new RestockAll());

        Assert.Empty(sim.Tasks.Tasks.OfType<RestockTask>());
    }

    [Fact]
    public void Only_before_opening()
    {
        var (sim, _, _, _) = Stocked();
        sim.Run(new OpenStore());
        sim.DrainEvents();

        sim.Run(new RestockAll());

        Assert.Equal(RejectReason.WrongPhase, sim.DrainEvents().Single<CommandRejected>().Reason);
    }

    [Fact]
    public void Nothing_to_move_is_rejected()
    {
        var (sim, _, _, _) = Stocked();
        sim.Run(new RestockAll());
        sim.DrainEvents();

        sim.Run(new RestockAll());

        Assert.Equal(RejectReason.NothingToDo, sim.DrainEvents().Single<CommandRejected>().Reason);
    }

    [Fact]
    public void Slot_someone_is_already_restocking_is_left_to_them()
    {
        var (sim, riceSlot, noodleSlot, _) = Stocked();
        sim.Tick();
        sim.WalkPlayerTo(new GridPoint(10, 2));
        sim.Run(new PlayerInteract());
        var carryingFor = ((RestockTask)sim.Tasks.Find(sim.Player.TaskId!.Value)!).Target;

        sim.Run(new RestockAll());

        var other = carryingFor == riceSlot ? noodleSlot : riceSlot;
        Assert.Equal(0, sim.Inventory.Count(carryingFor, sim.Shelves.ProductAt(carryingFor)!));
        Assert.True(sim.Inventory.Count(other, sim.Shelves.ProductAt(other)!) > 0);
    }

    [Fact]
    public void Oldest_stock_goes_out_first()
    {
        var sim = SimTestKit.NewSim();
        sim.Run(new PlaceOrder(Rice, 2));
        sim.RunFullDay();
        sim.Run(new PlaceOrder(Rice, 10));
        sim.RunFullDay();
        var slot = new StockLocation(sim.InstanceOf("shelf"), 0);
        sim.Run(new AssignShelfSlot(slot.FixtureInstanceId, 0, Rice));

        sim.Run(new RestockAll());

        var lots = sim.Inventory.Get(slot).Lots;
        Assert.Equal(2, lots.Single(l => l.BestBeforeDay == lots.Min(x => x.BestBeforeDay)).Quantity);
    }
}
