using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Tasks;
using static ChineseGrocery.Sim.Tests.Meat.MeatScenario;

namespace ChineseGrocery.Sim.Tests.Tasks;

/// <summary>A slot never ends up over full: goods put back by shoppers while a restock was being carried leave less room.</summary>
public class ShelvingOverflowTests
{
    private const string Rice = "rice_jasmine";
    private static readonly GridPoint BackroomCell = new(10, 2);
    private static readonly GridPoint ShelfCell = new(1, 2);

    [Fact]
    public void What_no_longer_fits_goes_back_to_storage()
    {
        var sim = SimTestKit.NewSim();
        sim.Run(new PlaceOrder(Rice, 20));
        sim.RunFullDay();
        var slot = new StockLocation(sim.InstanceOf("shelf"), 0);
        sim.Run(new AssignShelfSlot(slot.FixtureInstanceId, 0, Rice));
        sim.WalkPlayerTo(BackroomCell);
        sim.Run(new PlayerInteract());
        TickUntil(sim, () => sim.Player.Step == WorkStep.Carrying);
        var carried = sim.Player.Carrying.Sum(l => l.Quantity);
        var full = sim.Data.Products.Single(p => p.Id == Rice).UnitsPerShelfSlot;
        Assert.Equal(full, carried);

        // A shopper put two back meanwhile.
        sim.Inventory.GetOrCreate(slot).Add(new StockLot(Rice, 2, 999, 900));
        sim.WalkPlayerTo(ShelfCell);
        sim.Run(new PlayerInteract(slot.FixtureInstanceId));
        TickUntil(sim, () => sim.Player.Step == WorkStep.Idle);

        Assert.Equal(full, sim.Inventory.Count(slot, Rice));
        Assert.Equal(20 - full + 2, sim.Inventory.Count(StockLocation.Storage(sim.InstanceOf("backroom")), Rice));
    }
}
