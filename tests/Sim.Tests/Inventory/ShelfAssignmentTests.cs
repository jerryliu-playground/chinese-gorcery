using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Tests.Inventory;

public class ShelfAssignmentTests
{
    private static (GameSim Sim, int Shelf) NewSimWithShelf()
    {
        var sim = SimTestKit.NewSim();
        sim.DrainEvents();
        return (sim, sim.InstanceOf("shelf"));
    }

    private static RejectReason RejectionFor(GameSim sim, SimCommand command)
    {
        sim.DrainEvents();
        sim.Run(command);
        return sim.DrainEvents().Single<CommandRejected>().Reason;
    }

    [Fact]
    public void Slots_start_unassigned()
    {
        var (sim, shelf) = NewSimWithShelf();

        Assert.Null(sim.Shelves.ProductAt(new StockLocation(shelf, 0)));
    }

    [Fact]
    public void Assigning_a_slot_sets_its_product_in_any_phase()
    {
        var (sim, shelf) = NewSimWithShelf();
        sim.Run(new OpenStore());
        sim.DrainEvents();

        sim.Run(new AssignShelfSlot(shelf, 2, "soy_milk"));

        Assert.Equal("soy_milk", sim.Shelves.ProductAt(new StockLocation(shelf, 2)));
        Assert.Equal(new ShelfSlotAssigned(new StockLocation(shelf, 2), "soy_milk"), sim.DrainEvents().Single<ShelfSlotAssigned>());
    }

    [Fact]
    public void Clearing_an_empty_slot_unassigns_it()
    {
        var (sim, shelf) = NewSimWithShelf();
        sim.Run(new AssignShelfSlot(shelf, 0, "soy_milk"));

        sim.Run(new AssignShelfSlot(shelf, 0, null));

        Assert.Null(sim.Shelves.ProductAt(new StockLocation(shelf, 0)));
    }

    [Fact]
    public void Invalid_assignments_are_rejected()
    {
        var (sim, shelf) = NewSimWithShelf();
        var slots = SimTestKit.Data.Fixtures.Single(f => f.Id == "shelf").Capacity;

        Assert.Equal(RejectReason.UnknownFixtureInstance, RejectionFor(sim, new AssignShelfSlot(999, 0, "soy_milk")));
        Assert.Equal(RejectReason.NotADisplay, RejectionFor(sim, new AssignShelfSlot(sim.InstanceOf("backroom"), 0, "soy_milk")));
        Assert.Equal(RejectReason.InvalidSlot, RejectionFor(sim, new AssignShelfSlot(shelf, slots, "soy_milk")));
        Assert.Equal(RejectReason.InvalidSlot, RejectionFor(sim, new AssignShelfSlot(shelf, -1, "soy_milk")));
        Assert.Equal(RejectReason.UnknownProduct, RejectionFor(sim, new AssignShelfSlot(shelf, 0, "seafood_crab")));
        Assert.Equal(RejectReason.WrongStorageType, RejectionFor(sim, new AssignShelfSlot(shelf, 0, "pork_belly")));
    }

    [Fact]
    public void Selling_a_shelf_drops_its_assignments()
    {
        var (sim, shelf) = NewSimWithShelf();
        sim.Run(new AssignShelfSlot(shelf, 0, "soy_milk"));

        sim.Run(new SellFixture(shelf));

        Assert.Null(sim.Shelves.ProductAt(new StockLocation(shelf, 0)));
        Assert.Empty(sim.Shelves.Assigned);
    }
}
