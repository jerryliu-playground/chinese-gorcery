using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Story;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Core;

/// <summary>
/// The student loan (spec 18.6): a minimum payment comes out of cash at closing every few days; the player can pay
/// extra at any time. Cash never goes negative for it: whatever cannot be paid stays due (arrears) and is never a
/// game over.
/// </summary>
public sealed partial class GameSim
{
    /// <summary>Null when the game runs without story data.</summary>
    public StudentLoan? Loan { get; private set; }

    /// <summary>The day whose closing takes the next scheduled payment, or null once the loan is paid off.</summary>
    public int? NextLoanPaymentDay()
    {
        if (Loan is not { PaidOff: false } || Data.Story is not { } story) return null;

        var every = story.Settings.Loan.PaymentEveryDays;
        var from = Clock.Phase == DayPhase.Closed ? Clock.Day + 1 : Clock.Day;
        return (from + every - 1) / every * every;
    }

    /// <summary>Called while closing the day, before the report is drawn up, so the payment shows on it.</summary>
    private void CollectLoanPayment()
    {
        if (Loan is not { PaidOff: false } loan || Data.Story is not { } story) return;

        var terms = story.Settings.Loan;
        if (Clock.Day % terms.PaymentEveryDays != 0) return;

        loan.FallDue(terms.MinimumPaymentCents);
        var paid = Math.Clamp(Economy.CashCents, 0, loan.ArrearsCents);
        PayTowardsLoan(loan, paid, scheduled: true);
    }

    private Rejection? HandlePayLoan(PayLoan pay)
    {
        if (Loan is not { PaidOff: false } loan) return Reject(RejectReason.NothingToDo);
        if (pay.AmountCents <= 0) return Reject(RejectReason.InvalidQuantity);

        var amount = Math.Min(pay.AmountCents, loan.BalanceCents);
        if (!Economy.CanAfford(amount)) return Reject(RejectReason.InsufficientCash);

        PayTowardsLoan(loan, amount, scheduled: false);
        return null;
    }

    private void PayTowardsLoan(StudentLoan loan, long cents, bool scheduled)
    {
        if (cents > 0) Economy.Spend(ExpenseCategory.Loan, cents);
        loan.Pay(cents);
        _events.Add(new LoanPaymentMade(cents, loan.ArrearsCents, scheduled));
        if (loan.PaidOff) _events.Add(new LoanPaidOff(Clock.Day));
    }
}
