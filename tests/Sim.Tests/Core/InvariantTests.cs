using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;

namespace ChineseGrocery.Sim.Tests.Core;

/// <summary>Spec section 7.3 invariants, checked over many days of pseudo-random play.</summary>
public class InvariantTests
{
    /// <summary>Drives a sim with orders chosen by a test-side RNG (separate from the sim's own).</summary>
    private static GameSim PlayRandomly(int seed, int days)
    {
        var sim = SimTestKit.NewSim((ulong)seed);
        var driver = new Random(seed);
        var products = SimTestKit.Data.Products.Select(p => p.Id).Append("not_a_product").ToList();

        for (var day = 0; day < days; day++)
        {
            for (var i = driver.Next(0, 6); i > 0; i--)
            {
                sim.Enqueue(new PlaceOrder(products[driver.Next(products.Count)], driver.Next(-2, 60)));
            }

            for (var i = driver.Next(0, 4); i > 0; i--)
            {
                sim.Enqueue(RandomLayoutCommand(sim, driver));
            }

            sim.RunFullDay();
            AssertNoNegativeStock(sim);
            AssertLayoutValid(sim);
        }

        return sim;
    }

    private static SimCommand RandomLayoutCommand(GameSim sim, Random driver)
    {
        var fixtures = SimTestKit.Data.Fixtures.Select(f => f.Id).ToList();
        var placed = sim.Layout.Fixtures;
        var spot = new GridPoint(driver.Next(-1, sim.Layout.Size.W), driver.Next(-1, sim.Layout.Size.H));
        return driver.Next(3) switch
        {
            0 => new PlaceFixture(fixtures[driver.Next(fixtures.Count)], spot, (Rotation)driver.Next(4)),
            1 => new MoveFixture(placed[driver.Next(placed.Count)].InstanceId, spot, (Rotation)driver.Next(4)),
            _ => new SellFixture(placed[driver.Next(placed.Count)].InstanceId),
        };
    }

    private static void AssertLayoutValid(GameSim sim)
    {
        Assert.Null(LayoutRules.Check(sim.Layout.Size, sim.Layout.Fixtures, id => SimTestKit.Data.Fixtures.Single(f => f.Id == id)));
        Assert.All(sim.Inventory.Locations, l => Assert.NotNull(sim.Layout.Find(l.Location.FixtureInstanceId)));
    }

    private static void AssertNoNegativeStock(GameSim sim)
    {
        Assert.All(sim.Inventory.Locations.SelectMany(l => l.Lots), lot => Assert.True(lot.Quantity > 0));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Cash_change_equals_revenue_minus_spending_every_day(int seed)
    {
        var sim = PlayRandomly(seed, days: 20);

        Assert.Equal(20, sim.Reports.Count);
        Assert.All(sim.Reports, r =>
        {
            Assert.Equal(r.RevenueCents - r.PurchaseCostCents - r.WagesCents - r.FixtureSpendCents, r.CashEndCents - r.CashStartCents);
            Assert.Equal(r.CashEndCents - r.CashStartCents, r.ProfitCents);
            Assert.True(r.CashEndCents >= 0);
            Assert.True(r.LossCents >= 0);
        });
        Assert.Equal(sim.Reports[^1].CashEndCents, sim.Economy.CashCents);
    }

    [Fact]
    public void Random_layout_play_actually_changes_the_layout()
    {
        var sim = PlayRandomly(seed: 6, days: 20);
        var events = sim.DrainEvents();

        Assert.NotEmpty(events.OfType<FixturePlaced>());
        Assert.NotEmpty(events.OfType<FixtureMoved>());
        Assert.NotEmpty(events.OfType<FixtureSold>());
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void Every_paid_unit_is_delivered_then_either_kept_or_written_off(int seed)
    {
        var sim = PlayRandomly(seed, days: 15);
        var events = sim.DrainEvents();

        var ordered = events.OfType<OrderPlaced>().Sum(e => e.Quantity);
        var delivered = events.OfType<OrderDelivered>().Sum(e => e.Quantity);
        var expired = events.OfType<ProductExpired>().Sum(e => e.Quantity);
        var onHand = sim.Inventory.Locations.SelectMany(l => l.Lots).Sum(l => l.Quantity);

        Assert.Equal(ordered, delivered + sim.PendingOrders.Sum(o => o.Quantity));
        Assert.Equal(delivered, onHand + expired);
        Assert.Equal(events.OfType<ProductExpired>().Sum(e => e.LossCents), sim.Reports.Sum(r => r.LossCents));
    }
}
