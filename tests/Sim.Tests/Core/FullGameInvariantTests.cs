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
        var sim = Play(seed: 3, days: 30);
        Assert.Contains(sim.Reports.SelectMany(r => r.Sales), s => s.ProductId == "ground_pork");

        // Valued at purchase cost, so units of different products (and primals cut into boxes) can be summed.
        var paid = sim.Reports.Sum(r => r.PurchaseCostCents);
        var pending = sim.PendingOrders.Sum(o => o.Quantity * o.UnitCostCents);
        var onHand = sim.Inventory.Locations.SelectMany(l => l.Lots).Sum(l => l.Quantity * l.UnitCostCents);
        var carried = sim.Workers.SelectMany(w => w.Carrying).Sum(l => l.Quantity * l.UnitCostCents);
        var sold = sim.Reports.Sum(r => r.CostOfGoodsSoldCents);
        var writtenOff = sim.Reports.Sum(r => r.LossCents);

        Assert.Equal(paid, pending + onHand + carried + sold + writtenOff);
    }

    [Fact]
    public void Bot_upgrades_hires_a_butcher_and_sells_cut_meat()
    {
        var sim = Play(seed: 1, days: 30);

        Assert.True(sim.Economy.Stage >= 2);
        var butcher = Assert.Single(sim.Workers, w => w.RoleId == "butcher");
        Assert.True(sim.Reports[^1].WagesCents == butcher.WageCentsPerDay);
        var sold = sim.Reports.SelectMany(r => r.Sales).Select(s => s.ProductId).ToHashSet();
        Assert.Superset(new HashSet<string> { "pork_belly", "pork_ribs", "pork_loin", "ground_pork" }, sold);
        Assert.DoesNotContain("pork_primal", sold);
        Assert.True(sim.Reports.Sum(r => r.LossCents) > 0, "cutting leaves trimmings as loss");
    }

    [Fact]
    public void A_task_is_never_held_by_two_workers_at_any_tick()
    {
        var sim = SimTestKit.NewSim(2);
        var bot = new PlayerBot(stockTarget: 40);
        var ticksChecked = 0;
        bot.AfterTick = () =>
        {
            var held = sim.Workers.Where(w => w.TaskId is not null).ToList();
            Assert.Equal(held.Count, held.Select(w => w.TaskId).Distinct().Count());
            Assert.All(held, w => Assert.Equal(w.Id, sim.Tasks.ClaimedBy(w.TaskId!.Value)));
            Assert.All(sim.Tasks.Claims, c => Assert.Equal(c.Key, sim.Workers.Single(w => w.Id == c.Value).TaskId));
            ticksChecked++;
        };

        for (var day = 0; day < 24; day++) bot.PlayDay(sim);

        Assert.Contains(sim.Workers, w => w.RoleId == "butcher");
        Assert.True(ticksChecked > 50_000);
    }

    [Fact]
    public void Same_seed_plays_out_identically_and_different_seeds_differ()
    {
        Assert.Equal(SimSnapshot.Describe(Play(11, days: 5)), SimSnapshot.Describe(Play(11, days: 5)));
        Assert.NotEqual(SimSnapshot.Describe(Play(11, days: 5)), SimSnapshot.Describe(Play(12, days: 5)));
    }
}
