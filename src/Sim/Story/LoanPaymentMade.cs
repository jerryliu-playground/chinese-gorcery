using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Story;

/// <summary>Money went to the student loan: a scheduled payment, or an extra one the player chose to make.</summary>
/// <param name="ArrearsCents">Still due and unpaid afterwards (only a scheduled payment can fall short).</param>
public sealed record LoanPaymentMade(long PaidCents, long ArrearsCents, bool Scheduled) : SimEvent;
