using ChineseGrocery.Sim.Economy;

namespace ChineseGrocery.Sim.Save;

/// <summary>The current day's running totals.</summary>
public sealed record LedgerSave(
    int Day,
    long CashStartCents,
    long RevenueCents,
    long CostOfGoodsSoldCents,
    long PurchaseCostCents,
    long WagesCents,
    long FixtureSpendCents,
    long LossCents,
    int Customers,
    int AngryLeaves,
    int ItemsNotFound,
    int ItemsTooExpensive,
    IReadOnlyList<ProductSales> Sales,
    long ProduceLossCents = 0,
    long SeafoodLossCents = 0,
    int SeafoodDeaths = 0,
    long MeatLossCents = 0,
    long DeliLossCents = 0,
    long KitchenRescuedCents = 0);
