using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Tests.Inventory;

/// <summary>Ordering after closing arrives the next morning; the day's books close at closing time (playtest, 2026-10-24).</summary>
public class OrderAfterClosingTests
{
    private const string Rice = "rice_jasmine";
    private static readonly long RiceCost = SimTestKit.Data.Products.Single(p => p.Id == Rice).CostCents;

    [Fact]
    public void Order_placed_after_closing_arrives_next_morning()
    {
        var sim = SimTestKit.NewSim();
        sim.OpenAndRunToClose();
        sim.DrainEvents();

        sim.Run(new PlaceOrder(Rice, 10));
        Assert.Single(sim.DrainEvents().OfType<OrderPlaced>());

        sim.Run(new StartNextDay());
        Assert.Equal(10, sim.Inventory.Get(StockLocation.Storage(sim.InstanceOf("backroom"))).Count(Rice));
    }

    [Fact]
    public void Orders_are_still_refused_while_open()
    {
        var sim = SimTestKit.NewSim();
        sim.Run(new OpenStore());
        sim.DrainEvents();

        sim.Run(new PlaceOrder(Rice, 10));

        Assert.Equal(RejectReason.WrongPhase, sim.DrainEvents().Single<CommandRejected>().Reason);
    }

    [Fact]
    public void Spending_after_closing_shows_in_the_next_days_report_and_cash_still_reconciles()
    {
        var sim = SimTestKit.NewSim();
        sim.OpenAndRunToClose();
        var dayOne = sim.Reports[^1];

        sim.Run(new PlaceOrder(Rice, 10));
        sim.Run(new StartNextDay());
        sim.OpenAndRunToClose();
        var dayTwo = sim.Reports[^1];

        Assert.Equal(dayOne.CashEndCents, dayTwo.CashStartCents);
        Assert.Equal(10 * RiceCost, dayTwo.PurchaseCostCents);
        Assert.Equal(dayTwo.RevenueCents - dayTwo.PurchaseCostCents - dayTwo.WagesCents - dayTwo.FixtureSpendCents, dayTwo.CashEndCents - dayTwo.CashStartCents);
        Assert.Equal(DayPhase.Closed, sim.Clock.Phase);
    }
}
