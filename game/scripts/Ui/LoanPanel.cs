using System.Globalization;
using ChineseGrocery.Sim.Story;
using Godot;

namespace ChineseGrocery.Game.Ui;

/// <summary>The student loan: what is left, the next scheduled payment, arrears, and buttons to pay extra from cash.</summary>
public partial class LoanPanel : PanelBase
{
    /// <summary>Extra payment buttons, in cents.</summary>
    private static readonly long[] Amounts = [5_000, 20_000, 100_000];

    protected override void Rebuild()
    {
        SetTitle(UiText.T("ui.loan.title"));
        ClearBody();
        if (Host.Sim.Loan is not { } loan || Host.Data.Story is not { } story)
        {
            Close();
            return;
        }

        var terms = story.Settings.Loan;
        Body.AddChild(Row(Text(UiText.T("ui.loan.balance"), 150), Text(UiText.Money(loan.BalanceCents))));
        if (loan.PaidOff)
        {
            Body.AddChild(Text(UiText.T("ui.loan.paid_off")));
            return;
        }

        Body.AddChild(Row(Text(UiText.T("ui.loan.minimum"), 150),
            Text(UiText.F("ui.loan.every_days", ("amount", UiText.Money(terms.MinimumPaymentCents)), ("days", terms.PaymentEveryDays)))));
        if (Host.Sim.NextLoanPaymentDay() is { } due)
        {
            Body.AddChild(Row(Text(UiText.T("ui.loan.next_payment"), 150), Text(UiText.F("ui.hud.day", ("day", due.ToString(CultureInfo.InvariantCulture))))));
        }

        if (loan.ArrearsCents > 0)
        {
            Body.AddChild(Row(Text(UiText.T("ui.loan.arrears"), 150), Text(UiText.Money(loan.ArrearsCents))));
            Body.AddChild(Text(UiText.T("ui.loan.arrears_note")));
        }

        Body.AddChild(Text(UiText.T("ui.loan.pay_extra")));
        var cash = Host.Sim.Economy.CashCents;
        var row = Row();
        foreach (var amount in Amounts)
        {
            var pay = Math.Min(amount, loan.BalanceCents);
            var b = MoneyButton(pay, cash >= pay);
            b.Pressed += () => Pay(pay);
            row.AddChild(b);
        }

        var all = Math.Min(loan.BalanceCents, Math.Max(0, cash));
        var everything = new Button { Text = "ui.loan.pay_all", Disabled = all <= 0 };
        everything.Pressed += () => Pay(all);
        row.AddChild(everything);
        Body.AddChild(row);
    }

    private static Button MoneyButton(long cents, bool enabled)
    {
        var b = new Button { AutoTranslateMode = AutoTranslateModeEnum.Disabled, Disabled = !enabled };
        b.Text = UiText.Money(cents);
        return b;
    }

    private void Pay(long cents)
    {
        if (cents <= 0) return;
        Host.SendNow(new PayLoan(cents));
        Rebuild();
    }
}
