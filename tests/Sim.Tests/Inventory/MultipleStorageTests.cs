using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;

namespace ChineseGrocery.Sim.Tests.Inventory;

/// <summary>A second backroom adds room for orders (bug found in playtest, 2026-10-24).</summary>
public class MultipleStorageTests
{
    private const string Rice = "rice_jasmine";
    private static readonly int RicePerSlot = SimTestKit.Data.Products.Single(p => p.Id == Rice).UnitsPerStorageSlot;
    private static readonly int BackroomSlots = SimTestKit.Data.Fixtures.Single(f => f.Id == "backroom").Capacity;
    private static int Full => BackroomSlots * RicePerSlot;

    /// <summary>A rich store with a second backroom at (6,3), used from (7,5).</summary>
    private static (GameSim Sim, int First, int Second) TwoBackrooms()
    {
        var sim = SimTestKit.NewSim(data: SimTestKit.Data with { Economy = SimTestKit.Data.Economy with { StartingCashCents = 100_000_000 } });
        sim.Run(new PlaceFixture("backroom", new GridPoint(6, 3)));
        var second = sim.Layout.Fixtures[^1].InstanceId;
        sim.DrainEvents();
        return (sim, sim.InstanceOf("backroom"), second);
    }

    [Fact]
    public void A_new_backroom_takes_what_the_first_cannot()
    {
        var (sim, first, second) = TwoBackrooms();
        sim.Run(new PlaceOrder(Rice, Full));

        sim.Run(new PlaceOrder(Rice, 1));
        sim.RunFullDay();

        Assert.Empty(sim.DrainEvents().OfType<CommandRejected>());
        Assert.Equal(Full, sim.Inventory.Count(StockLocation.Storage(first), Rice));
        Assert.Equal(1, sim.Inventory.Count(StockLocation.Storage(second), Rice));
    }

    [Fact]
    public void One_order_splits_across_backrooms()
    {
        var (sim, first, second) = TwoBackrooms();

        sim.Run(new PlaceOrder(Rice, Full + 6));
        var placed = sim.DrainEvents().Single<OrderPlaced>();
        sim.RunFullDay();

        Assert.Equal(Full + 6, placed.Quantity);
        Assert.Equal(Full, sim.Inventory.Count(StockLocation.Storage(first), Rice));
        Assert.Equal(6, sim.Inventory.Count(StockLocation.Storage(second), Rice));
    }

    [Fact]
    public void Still_refused_when_every_backroom_is_full()
    {
        var (sim, _, _) = TwoBackrooms();
        sim.Run(new PlaceOrder(Rice, 2 * Full));

        sim.Run(new PlaceOrder(Rice, 1));

        Assert.Equal(RejectReason.StorageFull, sim.DrainEvents().Single<CommandRejected>().Reason);
    }

    [Fact]
    public void Delivery_falls_back_if_its_backroom_was_sold()
    {
        var (sim, first, second) = TwoBackrooms();
        sim.Run(new PlaceOrder(Rice, Full + 6));

        sim.Run(new SellFixture(second));
        sim.RunFullDay();

        Assert.Null(sim.Layout.Find(second));
        Assert.Equal(Full + 6, sim.Inventory.Count(StockLocation.Storage(first), Rice));
    }
}
