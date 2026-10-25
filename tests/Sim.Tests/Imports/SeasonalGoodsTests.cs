using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Inventory;

namespace ChineseGrocery.Sim.Tests.Imports;

/// <summary>Spec 17.5: some produce can only be bought in its season.</summary>
public class SeasonalGoodsTests
{
    private static RejectReason? Order(GameSim sim, string productId)
    {
        sim.DrainEvents();
        sim.Run(new PlaceOrder(productId, 1));
        return sim.DrainEvents().OfType<CommandRejected>().SingleOrDefault()?.Reason;
    }

    private static GameSim OnDay(int day)
    {
        var sim = SimTestKit.NewSim(data: SimTestKit.Data with { Economy = SimTestKit.Data.Economy with { StartingCashCents = 10_000_000 } });
        while (sim.Clock.Day < day) sim.RunFullDay();
        return sim;
    }

    [Fact]
    public void Yangmei_is_a_summer_fruit_and_persimmon_an_autumn_one()
    {
        var spring = OnDay(1);
        Assert.Equal(0, spring.SeasonIndexOf(spring.Clock.Day));
        Assert.Equal(RejectReason.OutOfSeason, Order(spring, "yangmei"));
        Assert.Equal(RejectReason.OutOfSeason, Order(spring, "persimmon"));
        Assert.Null(Order(spring, "gai_lan"));

        var summer = OnDay(spring.Data.Calendar!.Seasons[0].Days + 1);
        Assert.Equal(1, summer.SeasonIndexOf(summer.Clock.Day));
        Assert.Null(Order(summer, "yangmei"));
        Assert.Equal(RejectReason.OutOfSeason, Order(summer, "persimmon"));
    }

    [Fact]
    public void Only_some_products_are_seasonal()
    {
        var seasonal = SimTestKit.Data.Products.Where(p => p.Seasons is not null).Select(p => p.Id).ToHashSet();

        Assert.Superset(new HashSet<string> { "yangmei", "persimmon", "mango", "longan" }, seasonal);
        Assert.DoesNotContain("rice_jasmine", seasonal);
    }
}
