using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Tests.Economy;

public class PricingTests
{
    private static readonly long Suggested = SimTestKit.Data.Products.Single(p => p.Id == "soy_milk").SuggestedPriceCents;

    [Fact]
    public void Price_starts_at_the_suggested_price()
    {
        Assert.Equal(Suggested, SimTestKit.NewSim().Prices.PriceOf("soy_milk"));
    }

    [Fact]
    public void Price_can_be_set_before_opening()
    {
        var sim = SimTestKit.NewSim();
        sim.DrainEvents();

        sim.Run(new SetPrice("soy_milk", 250));

        Assert.Equal(250, sim.Prices.PriceOf("soy_milk"));
        Assert.Equal(new PriceChanged("soy_milk", 250), sim.DrainEvents().Single<PriceChanged>());
    }

    [Theory]
    [InlineData("soy_milk", 0, RejectReason.InvalidPrice)]
    [InlineData("soy_milk", -5, RejectReason.InvalidPrice)]
    [InlineData("seafood_crab", 100, RejectReason.UnknownProduct)]
    public void Invalid_prices_are_rejected(string productId, long price, RejectReason reason)
    {
        var sim = SimTestKit.NewSim();
        sim.DrainEvents();

        sim.Run(new SetPrice(productId, price));

        Assert.Equal(reason, sim.DrainEvents().Single<CommandRejected>().Reason);
        Assert.Equal(Suggested, sim.Prices.PriceOf("soy_milk"));
    }

    [Fact]
    public void Price_cannot_change_while_open()
    {
        var sim = SimTestKit.NewSim();
        sim.Run(new OpenStore());
        sim.DrainEvents();

        sim.Run(new SetPrice("soy_milk", 250));

        Assert.Equal(RejectReason.WrongPhase, sim.DrainEvents().Single<CommandRejected>().Reason);
    }

    public static TheoryData<int, int> BuyChances => new()
    {
        // comfort 1000, ceiling 1250, min chance 250 -> linear between.
        { 800, 1000 },
        { 1000, 1000 },
        { 1125, 625 },
        { 1250, 250 },
        { 1251, 0 },
    };

    [Theory]
    [MemberData(nameof(BuyChances))]
    public void Buy_chance_is_full_up_to_comfort_falls_linearly_to_the_ceiling_then_zero(int ratioPermille, int expected)
    {
        var type = SimTestKit.Data.CustomerTypes[0] with
        {
            PriceComfortRatioPermille = 1000, PriceCeilingRatioPermille = 1250, MinBuyChancePermille = 250,
        };

        Assert.Equal(expected, PriceAcceptance.BuyChancePermille(type, priceCents: ratioPermille, referenceCents: 1000));
    }
}
