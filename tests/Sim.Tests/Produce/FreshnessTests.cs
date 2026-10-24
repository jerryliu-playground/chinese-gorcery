using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Customers;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Produce;
using ChineseGrocery.Sim.Tasks;
using static ChineseGrocery.Sim.Tests.Customers.CustomerScenario;

namespace ChineseGrocery.Sim.Tests.Produce;

/// <summary>Spec 11.2: batches age through tiers, are marked down, spoil at closing; customers pick batches.</summary>
public class FreshnessTests
{
    private const string Cabbage = "napa_cabbage"; // fresh 2, normal 2, ripe 1 (placeholders)
    private static ProductDef Def(string id) => SimTestKit.Data.Products.Single(p => p.Id == id);

    [Theory]
    [InlineData(0, FreshnessTier.Fresh)]
    [InlineData(1, FreshnessTier.Fresh)]
    [InlineData(2, FreshnessTier.Normal)]
    [InlineData(3, FreshnessTier.Normal)]
    [InlineData(4, FreshnessTier.Ripe)]
    [InlineData(5, FreshnessTier.Spoiled)]
    public void A_batch_moves_through_the_tiers_day_by_day(int daysSinceArrival, FreshnessTier expected)
    {
        var cabbage = Def(Cabbage);
        var arrivedDay = 10;
        var lot = new StockLot(Cabbage, 1, arrivedDay + cabbage.ShelfLifeDays - 1, cabbage.CostCents);

        Assert.Equal(expected, Freshness.TierOf(cabbage, lot, arrivedDay + daysSinceArrival));
    }

    [Fact]
    public void Non_produce_is_always_fresh()
    {
        var rice = Def("rice_jasmine");

        Assert.Equal(FreshnessTier.Fresh, Freshness.TierOf(rice, new StockLot(rice.Id, 1, 2, 900), 2));
    }

    [Theory]
    [InlineData(0, 1000)]
    [InlineData(2, 850)]
    [InlineData(4, 500)]
    public void Selling_price_is_the_set_price_times_the_tier_factor(int age, int permille)
    {
        var sim = SimTestKit.NewSim();
        sim.Run(new SetPrice(Cabbage, 300));
        var lot = new StockLot(Cabbage, 1, sim.Clock.Day + Def(Cabbage).ShelfLifeDays - 1 - age, 120);

        Assert.Equal(300 * permille / 1000, sim.EffectivePrice(lot));
    }

    [Fact]
    public void Spoiled_batches_are_thrown_out_at_closing_even_in_the_backroom_and_counted_as_produce_waste()
    {
        var sim = SimTestKit.NewSim(data: SimTestKit.Data with
        {
            Economy = SimTestKit.Data.Economy with { CustomerArrivals = [new ArrivalBucket(SimTestKit.Data.Economy.OpenMinuteOfDay, 0)] },
        });
        sim.Run(new PlaceOrder(Cabbage, 6));
        var lifetime = Def(Cabbage).ShelfLifeDays;

        for (var d = 0; d < lifetime; d++) sim.RunFullDay();
        Assert.Equal(6, sim.Inventory.Total(Cabbage));

        sim.OpenAndRunToClose();

        Assert.Equal(0, sim.Inventory.Total(Cabbage));
        Assert.Equal(6 * Def(Cabbage).CostCents, sim.Reports[^1].ProduceLossCents);
        Assert.Equal(sim.Reports[^1].LossCents, sim.Reports[^1].ProduceLossCents);
    }

    /// <summary>A produce stand at (7,3) holding one fresh and one ripe batch of cabbage, and a shopper who wants produce.</summary>
    private static (GameSim Sim, StockLocation Slot, int FreshDay, int RipeDay) StandWithTwoBatches(bool prefersDiscount)
    {
        var data = ScenarioData(perHour: 360, wants: Department.Produce);
        data = data with
        {
            CustomerTypes = data.CustomerTypes.Select((t, i) => i == 0 ? t with { PrefersDiscount = prefersDiscount, ProduceCategories = null } : t).ToList(),
            Fixtures = data.Fixtures.Select(f => f.Id == "produce_stand" ? f with { UnlockStage = 1 } : f).ToList(),
        };
        var sim = SimTestKit.NewSim(data: data);
        sim.Run(new PlaceFixture("produce_stand", new GridPoint(7, 3)));
        var slot = new StockLocation(sim.Layout.Fixtures[^1].InstanceId, 0);
        sim.Run(new AssignShelfSlot(slot.FixtureInstanceId, 0, Cabbage));
        var life = Def(Cabbage).ShelfLifeDays;
        var freshBest = sim.Clock.Day + life - 1;
        var ripeBest = sim.Clock.Day;
        sim.Inventory.GetOrCreate(slot).Add(new StockLot(Cabbage, 3, freshBest, 120));
        sim.Inventory.GetOrCreate(slot).Add(new StockLot(Cabbage, 3, ripeBest, 120));
        return (sim, slot, freshBest, ripeBest);
    }

    private static int TakenFrom(GameSim sim, int bestBefore)
    {
        Open(sim);
        TickUntil(sim, () => sim.Customers.Any(c => c.Basket.Count > 0));
        return sim.Customers.SelectMany(c => c.Basket).Count(b => b.Lot.BestBeforeDay == bestBefore);
    }

    [Fact]
    public void Customers_take_the_freshest_batch()
    {
        var (sim, _, fresh, _) = StandWithTwoBatches(prefersDiscount: false);

        Assert.True(TakenFrom(sim, fresh) > 0);
    }

    [Fact]
    public void Bargain_hunters_take_the_marked_down_batch_and_pay_less()
    {
        var (sim, _, _, ripe) = StandWithTwoBatches(prefersDiscount: true);

        Assert.True(TakenFrom(sim, ripe) > 0);
        var item = sim.Customers.SelectMany(c => c.Basket).First();
        Assert.Equal(sim.Prices.PriceOf(Cabbage) * SimTestKit.Data.Economy.Freshness!.PriceFactorsPermille.Ripe / 1000, item.PriceCents);
    }

    [Fact]
    public void Checkout_charges_the_marked_down_price()
    {
        var (sim, _, _, _) = StandWithTwoBatches(prefersDiscount: true);
        sim.WalkPlayerTo(CashierCell);
        Open(sim);

        TickUntil(sim, () => sim.Economy.Today.RevenueCents > 0);

        Assert.Equal(sim.Prices.PriceOf(Cabbage) * SimTestKit.Data.Economy.Freshness!.PriceFactorsPermille.Ripe / 1000, sim.Economy.Today.Sales.Single().RevenueCents);
    }
}
