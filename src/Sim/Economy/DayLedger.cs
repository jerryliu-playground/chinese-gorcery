namespace ChineseGrocery.Sim.Economy;

/// <summary>Running totals for the current day.</summary>
public sealed class DayLedger(int day, long cashStartCents)
{
    private readonly SortedDictionary<string, ProductSales> _sales = new(StringComparer.Ordinal);

    public int Day { get; } = day;
    public long CashStartCents { get; } = cashStartCents;
    public long RevenueCents { get; private set; }
    public long CostOfGoodsSoldCents { get; private set; }
    public long PurchaseCostCents { get; private set; }
    public long WagesCents { get; private set; }
    public long FixtureSpendCents { get; private set; }
    public long LossCents { get; private set; }
    public int Customers { get; private set; }
    public int AngryLeaves { get; private set; }

    internal void AddExpense(ExpenseCategory category, long cents)
    {
        switch (category)
        {
            case ExpenseCategory.Purchases: PurchaseCostCents += cents; break;
            case ExpenseCategory.Wages: WagesCents += cents; break;
            case ExpenseCategory.Fixtures: FixtureSpendCents += cents; break;
            default: throw new ArgumentOutOfRangeException(nameof(category), category, null);
        }
    }

    public int ItemsNotFound { get; private set; }
    public int ItemsTooExpensive { get; private set; }
    public IReadOnlyList<ProductSales> Sales => _sales.Values.ToList();

    internal void AddLoss(long cents) => LossCents += cents;

    internal void AddSale(string productId, int units, long revenueCents, long costCents)
    {
        RevenueCents += revenueCents;
        CostOfGoodsSoldCents += costCents;
        var before = _sales.GetValueOrDefault(productId) ?? new ProductSales(productId, 0, 0);
        _sales[productId] = before with { Units = before.Units + units, RevenueCents = before.RevenueCents + revenueCents };
    }

    internal void CountCustomer() => Customers++;
    internal void CountAngryLeave() => AngryLeaves++;
    internal void CountItemNotFound() => ItemsNotFound++;
    internal void CountItemTooExpensive() => ItemsTooExpensive++;

    internal DailyReport ToReport(long cashEndCents) => new(
        Day,
        CashStartCents,
        cashEndCents,
        RevenueCents,
        CostOfGoodsSoldCents,
        PurchaseCostCents,
        WagesCents,
        FixtureSpendCents,
        LossCents,
        RevenueCents - PurchaseCostCents - WagesCents - FixtureSpendCents,
        Customers,
        AngryLeaves,
        ItemsNotFound,
        ItemsTooExpensive,
        _sales.Values.ToList());
}
