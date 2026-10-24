using ChineseGrocery.Sim.Core;
using ChineseGrocery.SimRunner;

namespace ChineseGrocery.Sim.Tests.Core;

/// <summary>Spec 7.3 invariants over whole days of play with customers, driven by the SimRunner player bot.</summary>
public class FullGameInvariantTests
{
    private static GameSim Play(ulong seed, int days)
    {
        var sim = SimTestKit.NewSim(seed);
        var bot = new PlayerBot(stockTarget: 40);
        for (var d = 0; d < days; d++) bot.PlayDay(sim);
        return sim;
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    public void Every_day_balances_and_nothing_goes_negative(ulong seed)
    {
        var sim = Play(seed, days: 10);

        Assert.Equal(10, sim.Reports.Count);
        Assert.All(sim.Reports, r =>
        {
            Assert.Equal(r.RevenueCents - r.PurchaseCostCents - r.WagesCents - r.FixtureSpendCents, r.CashEndCents - r.CashStartCents);
            Assert.Equal(r.RevenueCents, r.Sales.Sum(s => s.RevenueCents));
            Assert.True(r.CashEndCents >= 0);
            Assert.True(r.AngryLeaves <= r.Customers);
        });
        Assert.True(sim.Reports.Sum(r => r.RevenueCents) > 0, "the bot should actually sell things");
        Assert.Equal(sim.Reports.Sum(r => r.RevenueCents), sim.Economy.CumulativeRevenueCents);
        Assert.All(sim.Inventory.Locations.SelectMany(l => l.Lots), lot => Assert.True(lot.Quantity > 0));
        Assert.Empty(sim.Customers);
    }

    [Fact]
    public void Every_unit_paid_for_is_pending_on_hand_carried_sold_or_written_off()
    {
        var sim = Play(seed: 3, days: 8);

        // Valued at purchase cost (fixed per product in data), so units of different products can be summed.
        long CostOf(string productId) => sim.Data.Products.Single(p => p.Id == productId).CostCents;
        var paid = sim.Reports.Sum(r => r.PurchaseCostCents);
        var pending = sim.PendingOrders.Sum(o => o.Quantity * o.UnitCostCents);
        var onHand = sim.Inventory.Locations.SelectMany(l => l.Lots).Sum(l => l.Quantity * l.UnitCostCents);
        var carried = sim.Workers.SelectMany(w => w.Carrying).Sum(l => l.Quantity * l.UnitCostCents);
        var sold = sim.Reports.SelectMany(r => r.Sales).Sum(s => s.Units * CostOf(s.ProductId));
        var writtenOff = sim.Reports.Sum(r => r.LossCents);

        Assert.Equal(paid, pending + onHand + carried + sold + writtenOff);
    }

    [Fact]
    public void Same_seed_plays_out_identically_and_different_seeds_differ()
    {
        Assert.Equal(SimSnapshot.Describe(Play(11, days: 5)), SimSnapshot.Describe(Play(11, days: 5)));
        Assert.NotEqual(SimSnapshot.Describe(Play(11, days: 5)), SimSnapshot.Describe(Play(12, days: 5)));
    }
}
