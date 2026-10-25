namespace ChineseGrocery.Sim.Story;

/// <summary>What is left of the protagonist's student loan, and how much of it is due and unpaid (arrears).</summary>
public sealed class StudentLoan(long balanceCents)
{
    public long BalanceCents { get; private set; } = balanceCents;

    /// <summary>Scheduled payments not yet made. Never ends the game; it just carries over to the next payment.</summary>
    public long ArrearsCents { get; internal set; }

    public bool PaidOff => BalanceCents == 0;

    /// <summary>Adds a scheduled payment to what is due (never more than what is left).</summary>
    internal void FallDue(long cents) => ArrearsCents = Math.Min(BalanceCents, ArrearsCents + cents);

    /// <summary>Money paid off the loan; it settles arrears first.</summary>
    internal void Pay(long cents)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cents);
        if (cents > BalanceCents) throw new InvalidOperationException($"Paying {cents} on a balance of {BalanceCents}.");

        BalanceCents -= cents;
        ArrearsCents = Math.Max(0, ArrearsCents - cents);
    }

    internal static StudentLoan Restore(long balanceCents, long arrearsCents) => new(balanceCents) { ArrearsCents = arrearsCents };
}
