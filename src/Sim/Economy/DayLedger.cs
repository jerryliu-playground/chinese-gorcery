namespace ChineseGrocery.Sim.Economy;

/// <summary>Running totals for the current day.</summary>
public sealed class DayLedger(int day, long cashStartCents)
{
    private readonly SortedDictionary<string, ProductSales> _sales = new(StringComparer.Ordinal);

    public int Day { get; } = day;
    public long CashStartCents { get; } = cashStartCents;
    public long RevenueCents { get; internal set; }
    public long CostOfGoodsSoldCents { get; internal set; }
    public long PurchaseCostCents { get; internal set; }
    public long WagesCents { get; internal set; }
    public long FixtureSpendCents { get; internal set; }
    public long LossCents { get; internal set; }
    public long ProduceLossCents { get; internal set; }
    public int Customers { get; internal set; }
    public int AngryLeaves { get; internal set; }

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

    public int ItemsNotFound { get; internal set; }
    public int ItemsTooExpensive { get; internal set; }
    public IReadOnlyList<ProductSales> Sales => _sales.Values.ToList();

    internal void AddLoss(long cents, bool produce)
    {
        LossCents += cents;
        if (produce) ProduceLossCents += cents;
    }

    internal void AddSale(string productId, int units, long revenueCents, long costCents)
    {
        RevenueCents += revenueCents;
        CostOfGoodsSoldCents += costCents;
        var before = _sales.GetValueOrDefault(productId) ?? new ProductSales(productId, 0, 0);
        _sales[productId] = before with { Units = before.Units + units, RevenueCents = before.RevenueCents + revenueCents };
    }

    /// <summary>Restores per-product sales when loading a save.</summary>
    internal void RestoreSales(IEnumerable<ProductSales> sales)
    {
        _sales.Clear();
        foreach (var s in sales) _sales[s.ProductId] = s;
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
        ProduceLossCents,
        RevenueCents - PurchaseCostCents - WagesCents - FixtureSpendCents,
        Customers,
        AngryLeaves,
        ItemsNotFound,
        ItemsTooExpensive,
        _sales.Values.ToList());
}
