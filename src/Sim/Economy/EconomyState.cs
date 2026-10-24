namespace ChineseGrocery.Sim.Economy;

/// <summary>Cash, lifetime revenue and store stage. All money moves through here so the daily ledger always balances.</summary>
public sealed class EconomyState
{
    internal EconomyState(long startingCashCents)
    {
        CashCents = startingCashCents;
        Today = new DayLedger(1, startingCashCents);
    }

    public long CashCents { get; internal set; }
    public long CumulativeRevenueCents { get; internal set; }
    public int Stage { get; internal set; } = 1;
    public DayLedger Today { get; internal set; }

    internal bool CanAfford(long cents) => cents <= CashCents;

    /// <summary>Spends cash; callers must check <see cref="CanAfford"/> first.</summary>
    internal void Spend(ExpenseCategory category, long cents)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cents);
        if (!CanAfford(cents)) throw new InvalidOperationException($"Cannot spend {cents} with {CashCents} cash.");

        CashCents -= cents;
        Today.AddExpense(category, cents);
    }

    /// <summary>Pays an obligation even if it takes cash below zero (wages); while negative, nothing can be bought.</summary>
    internal void Pay(ExpenseCategory category, long cents)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cents);
        CashCents -= cents;
        Today.AddExpense(category, cents);
    }

    /// <summary>Money coming back for something bought earlier (e.g. selling a fixture); reduces that category's spending.</summary>
    internal void Refund(ExpenseCategory category, long cents)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cents);
        CashCents += cents;
        Today.AddExpense(category, -cents);
    }

    /// <summary>Takes payment for <paramref name="units"/> of a product that cost <paramref name="costCents"/> in total to buy.</summary>
    internal void Earn(string productId, int units, long revenueCents, long costCents = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(revenueCents);
        CashCents += revenueCents;
        CumulativeRevenueCents += revenueCents;
        Today.AddSale(productId, units, revenueCents, costCents);
    }

    internal void RecordLoss(long cents)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cents);
        Today.AddLoss(cents);
    }

    internal void AdvanceStage() => Stage++;

    internal DailyReport CloseDay() => Today.ToReport(CashCents);

    internal void BeginDay(int day) => Today = new DayLedger(day, CashCents);
}
