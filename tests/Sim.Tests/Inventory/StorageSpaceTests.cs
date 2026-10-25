using ChineseGrocery.Sim.Inventory;

namespace ChineseGrocery.Sim.Tests.Inventory;

/// <summary>What the order screen shows about storage: slots taken (stock and goods on order) and room left per product.</summary>
public class StorageSpaceTests
{
    [Fact]
    public void Slots_used_counts_stock_and_goods_on_order_by_whole_slots()
    {
        var sim = SimTestKit.NewSim();
        var backroom = sim.InstanceOf("backroom");
        var noodles = sim.Data.Products.Single(p => p.Id == "noodles_instant");
        sim.Inventory.GetOrCreate(StockLocation.Storage(backroom)).Add(new StockLot(noodles.Id, noodles.UnitsPerStorageSlot + 1, 99, 80));
        sim.Run(new PlaceOrder("rice_jasmine", 1));

        Assert.Equal(3, sim.SlotsUsed(backroom));
    }

    [Fact]
    public void Room_left_for_a_product_includes_the_unused_part_of_its_own_slot()
    {
        var sim = SimTestKit.NewSim();
        var backroom = sim.InstanceOf("backroom");
        var capacity = sim.Data.Fixtures.Single(f => f.Id == "backroom").Capacity;
        var noodles = sim.Data.Products.Single(p => p.Id == "noodles_instant");
        sim.Inventory.GetOrCreate(StockLocation.Storage(backroom)).Add(new StockLot(noodles.Id, 5, 99, 80));

        Assert.Equal(capacity * noodles.UnitsPerStorageSlot - 5, sim.RoomFor(noodles.Id));

        sim.Run(new PlaceOrder(noodles.Id, sim.RoomFor(noodles.Id)));
        Assert.Equal(0, sim.RoomFor(noodles.Id));
        Assert.Equal(0, sim.RoomFor("pork_primal"));
    }
}
