using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Seafood;
using static ChineseGrocery.Sim.Tests.Customers.CustomerScenario;
using static ChineseGrocery.Sim.Tests.Seafood.SeafoodScenario;

namespace ChineseGrocery.Sim.Tests.Seafood;

/// <summary>Lobster and crab keep in a tank of their own, where they live longer (Jerry 2026-10-25).</summary>
public class ShellfishTankTests
{
    private static RejectReason? Assign(GameSim sim, int fixture, string productId)
    {
        sim.DrainEvents();
        sim.Run(new AssignShelfSlot(fixture, 0, productId));
        return sim.DrainEvents().OfType<CommandRejected>().SingleOrDefault()?.Reason;
    }

    [Fact]
    public void Lobster_and_crab_go_in_the_shellfish_tank_and_fish_and_shrimp_in_the_fish_tank()
    {
        var sim = Store();
        var fishTank = Tank(sim, 7);
        var shellfish = ShellfishTank(sim, 8);

        Assert.Equal(RejectReason.WrongStorageType, Assign(sim, fishTank, Lobster));
        Assert.Equal(RejectReason.WrongStorageType, Assign(sim, fishTank, Crab));
        Assert.Equal(RejectReason.WrongStorageType, Assign(sim, shellfish, Fish));
        Assert.Null(Assign(sim, shellfish, Lobster));
        Assert.Null(Assign(sim, fishTank, Shrimp));
    }

    [Fact]
    public void A_shellfish_tank_gets_dirty_more_slowly_than_a_fish_tank()
    {
        var sim = Store();
        var fishTank = Tank(sim, 7, Shrimp);
        var shellfish = ShellfishTank(sim, 8, Crab);
        Stock(sim, fishTank, 0, Shrimp, 6);
        Stock(sim, shellfish, 0, Crab, 6);
        Open(sim);

        TickFor(sim, 720);

        Assert.True(sim.WaterQualityOf(shellfish) > sim.WaterQualityOf(fishTank), $"shellfish {sim.WaterQualityOf(shellfish)}, fish {sim.WaterQualityOf(fishTank)}");
        Assert.True(sim.WaterQualityOf(shellfish) < 100);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(40)]
    [InlineData(0)]
    public void Lobster_and_crab_die_less_often_than_fish_in_the_same_water(int quality)
    {
        var settings = SimTestKit.Data.Economy.Seafood!;
        int Chance(string id) => Mortality.ChancePermille(settings, SimTestKit.Data.Products.Single(p => p.Id == id), quality);

        Assert.True(Chance(Lobster) < Chance(Fish));
        Assert.True(Chance(Crab) < Chance(Fish));
    }

    [Fact]
    public void Live_shellfish_arrives_in_the_backroom_like_other_live_seafood()
    {
        var sim = Store();
        ShellfishTank(sim, 8, Lobster);
        sim.Run(new PlaceOrder(Lobster, 3));

        sim.RunFullDay();

        Assert.Equal(3, sim.Inventory.Count(StockLocation.Storage(sim.InstanceOf("backroom")), Lobster));
    }
}
