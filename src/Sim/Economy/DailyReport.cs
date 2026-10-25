namespace ChineseGrocery.Sim.Economy;

/// <summary>
/// End-of-day summary. Profit is cash-based: revenue minus all spending (equals the day's cash change); loss is informational.
/// EventCostCents is what events cost (fines, a generator) net of event rewards, so it can be negative. FestivalId is
/// the festival whose window the day was in (FestivalDayOffset days from it); EventIds are the random events known or
/// running that day.
/// FixtureSpendCents is net of sell refunds, so it can be negative. CostOfGoodsSoldCents is the purchase cost of what
/// was sold (cut meat carries its share of the primal's cost); informational, like loss.
/// </summary>
public sealed record DailyReport(
    int Day,
    long CashStartCents,
    long CashEndCents,
    long RevenueCents,
    long CostOfGoodsSoldCents,
    long PurchaseCostCents,
    long WagesCents,
    long FixtureSpendCents,
    long LossCents,
    long ProduceLossCents,
    long ProfitCents,
    int Customers,
    int AngryLeaves,
    int ItemsNotFound,
    int ItemsTooExpensive,
    IReadOnlyList<ProductSales> Sales,
    int SeafoodDeaths = 0,
    long SeafoodLossCents = 0,
    long MeatLossCents = 0,
    long DeliLossCents = 0,
    long KitchenRescuedCents = 0,
    long EventCostCents = 0,
    string? FestivalId = null,
    int? FestivalDayOffset = null,
    IReadOnlyList<string>? EventIds = null);
