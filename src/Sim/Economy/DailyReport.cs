namespace ChineseGrocery.Sim.Economy;

/// <summary>
/// End-of-day summary. Profit is cash-based: revenue minus all spending (equals the day's cash change); loss is informational.
/// FixtureSpendCents is net of sell refunds, so it can be negative.
/// </summary>
public sealed record DailyReport(
    int Day,
    long CashStartCents,
    long CashEndCents,
    long RevenueCents,
    long PurchaseCostCents,
    long WagesCents,
    long FixtureSpendCents,
    long LossCents,
    long ProfitCents,
    int Customers,
    int AngryLeaves,
    IReadOnlyList<ProductSales> Sales);
