using ChineseGrocery.Sim.Calendar;
using ChineseGrocery.Sim.Core;
using ChineseGrocery.SimRunner;

namespace ChineseGrocery.Sim.Tests.Calendar;

/// <summary>A whole year with seasons, festivals and random events, played by the SimRunner bot: the books still balance.</summary>
public class FullYearTests
{
    private static readonly Lazy<(GameSim Sim, List<SimEvent> Seen)> Year = new(() =>
    {
        var sim = new GameSim(SimTestKit.CalendarData, 1);
        var seen = new List<SimEvent>();
        var bot = new PlayerBot(stockTarget: 40) { EventsSeen = seen.AddRange };
        for (var day = 0; day < 112; day++) bot.PlayDay(sim);
        return (sim, seen);
    });

    [Fact]
    public void Every_day_balances_with_event_money_included()
    {
        var (sim, _) = Year.Value;

        Assert.All(sim.Reports, r =>
        {
            Assert.Equal(r.RevenueCents - r.PurchaseCostCents - r.WagesCents - r.FixtureSpendCents - r.LoanPaymentCents - r.EventCostCents, r.ProfitCents);
            Assert.Equal(r.ProfitCents, r.CashEndCents - r.CashStartCents);
            Assert.Equal(r.RevenueCents, r.Sales.Sum(s => s.RevenueCents));
        });
        Assert.Equal(sim.Reports.Sum(r => r.RevenueCents), sim.Economy.CumulativeRevenueCents);
        Assert.All(sim.Inventory.Locations.SelectMany(l => l.Lots), lot => Assert.True(lot.Quantity > 0));
    }

    [Fact]
    public void Every_unit_paid_for_is_still_accounted_for()
    {
        var (sim, _) = Year.Value;

        var paid = sim.Reports.Sum(r => r.PurchaseCostCents);
        var pending = sim.PendingOrders.Sum(o => o.Quantity * o.UnitCostCents);
        var onHand = sim.Inventory.Locations.SelectMany(l => l.Lots).Sum(l => l.Quantity * l.UnitCostCents)
                     + sim.Layout.Fixtures.SelectMany(f => sim.DeadIn(f.InstanceId)).Sum(l => l.Quantity * l.UnitCostCents);
        var carried = sim.Workers.SelectMany(w => w.Carrying).Sum(l => l.Quantity * l.UnitCostCents);

        Assert.Equal(paid, pending + onHand + carried + sim.Reports.Sum(r => r.CostOfGoodsSoldCents) + sim.Reports.Sum(r => r.LossCents));
    }

    [Fact]
    public void The_year_has_festivals_events_and_four_season_reports()
    {
        var (sim, seen) = Year.Value;

        Assert.Equal(4, sim.QuarterReports().Count);
        Assert.Equal(4, seen.OfType<QuarterReportReady>().Count());
        Assert.Equal(["dragon_boat", "mid_autumn", "thanksgiving", "christmas", "spring_festival"], seen.OfType<FestivalReminder>().Select(r => r.FestivalId));
        Assert.True(seen.OfType<StoreEventAnnounced>().Select(a => a.Event.EventId).Distinct().Count() >= 4);
        var sold = sim.Reports.SelectMany(r => r.Sales).Select(s => s.ProductId).ToHashSet();
        Assert.Contains("mooncake", sold);
        Assert.Contains("zongzi", sold);
        Assert.NotEmpty(sim.Decorations);
    }
}
