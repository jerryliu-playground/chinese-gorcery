using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Inventory;

namespace ChineseGrocery.Sim.Tests.Economy;

public class DailyReportTests
{
    [Fact]
    public void Report_shows_purchases_and_cash_based_profit()
    {
        var sim = SimTestKit.NewSim();
        var start = sim.Economy.CashCents;
        sim.Run(new PlaceOrder("rice_jasmine", 10), new PlaceOrder("soy_milk", 24));
        var spent = start - sim.Economy.CashCents;

        sim.OpenAndRunToClose();
        var report = sim.Reports.Single();

        Assert.Equal(1, report.Day);
        Assert.Equal(start, report.CashStartCents);
        Assert.Equal(start - spent, report.CashEndCents);
        Assert.Equal(spent, report.PurchaseCostCents);
        Assert.Equal(0, report.RevenueCents);
        Assert.Equal(0, report.WagesCents);
        Assert.Equal(0, report.FixtureSpendCents);
        Assert.Equal(-spent, report.ProfitCents);
        Assert.Equal(0, report.Customers);
        Assert.Empty(report.Sales);
    }

    [Fact]
    public void Each_day_starts_a_fresh_ledger()
    {
        var sim = SimTestKit.NewSim();
        sim.Run(new PlaceOrder("rice_jasmine", 10));
        sim.RunFullDay();

        sim.OpenAndRunToClose();

        Assert.Equal(0, sim.Reports[1].PurchaseCostCents);
        Assert.Equal(sim.Reports[0].CashEndCents, sim.Reports[1].CashStartCents);
    }
}
