using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Story;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Tests.Story;

/// <summary>Spec 18.6: weekly minimum payments from cash, extra payments, paying it off, and arrears that never end the game.</summary>
public class LoanTests
{
    private static GameData Terms(long principal, long minimum, int every = 7) =>
        StoryKit.WithSettings(s => s with { Tutorial = [], Loan = s.Loan with { PrincipalCents = principal, MinimumPaymentCents = minimum, PaymentEveryDays = every } });

    private static GameSim NewSim(GameData data)
    {
        var sim = StoryKit.NewSim(data);
        sim.SkipOpening();
        return sim;
    }

    [Fact]
    public void A_new_game_owes_the_whole_loan_and_pays_the_minimum_at_the_end_of_each_week()
    {
        var terms = StoryKit.Real.Story!.Settings.Loan;
        var sim = NewSim(StoryKit.WithSettings(s => s with { Tutorial = [] }));
        Assert.Equal(terms.PrincipalCents, sim.Loan!.BalanceCents);
        Assert.Equal(7, sim.NextLoanPaymentDay());

        for (var day = 0; day < 14; day++) sim.NextMorning();

        Assert.Equal([0, 0, 0, 0, 0, 0, terms.MinimumPaymentCents, 0, 0, 0, 0, 0, 0, terms.MinimumPaymentCents], sim.Reports.Select(r => r.LoanPaymentCents));
        Assert.Equal(terms.PrincipalCents - 2 * terms.MinimumPaymentCents, sim.Loan.BalanceCents);
        Assert.Equal(0, sim.Loan.ArrearsCents);
        Assert.Equal(21, sim.NextLoanPaymentDay());
        Assert.All(sim.Reports, r => Assert.Equal(r.ProfitCents, r.CashEndCents - r.CashStartCents));
    }

    [Fact]
    public void Extra_payments_come_out_of_cash_today_and_are_capped_at_what_is_left()
    {
        var sim = NewSim(Terms(principal: 50_000, minimum: 1_000));
        var cash = sim.Economy.CashCents;

        sim.Run(new PayLoan(20_000));
        Assert.Equal(30_000, sim.Loan!.BalanceCents);
        Assert.Equal(cash - 20_000, sim.Economy.CashCents);
        Assert.Equal(20_000, sim.Economy.Today.LoanPaymentCents);
        Assert.Contains(sim.DrainEvents(), e => e is LoanPaymentMade { PaidCents: 20_000, Scheduled: false });

        sim.Run(new PayLoan(0));
        Assert.Equal(RejectReason.InvalidQuantity, sim.Drain<CommandRejected>().Single().Reason);

        sim.Run(new PayLoan(1_000_000));
        Assert.True(sim.Loan.PaidOff);
        Assert.Equal(cash - 50_000, sim.Economy.CashCents);

        var big = NewSim(Terms(principal: 100_000_000, minimum: 1_000));
        big.Run(new PayLoan(big.Economy.CashCents + 1));
        Assert.Equal(RejectReason.InsufficientCash, big.Drain<CommandRejected>().Single().Reason);
        Assert.Equal(100_000_000, big.Loan!.BalanceCents);
    }

    [Fact]
    public void Paying_it_off_calls_home_and_ends_the_payments()
    {
        var sim = NewSim(Terms(principal: 10_000, minimum: 1_000));

        sim.Run(new PayLoan(10_000));

        Assert.Single(sim.DrainEvents().OfType<LoanPaidOff>());
        Assert.Equal("loan_paid_off", sim.Story.Current);
        Assert.Null(sim.NextLoanPaymentDay());
        sim.Run(new PayLoan(100));
        Assert.Equal(RejectReason.NothingToDo, sim.Drain<CommandRejected>().Single().Reason);

        sim.ReadAll();
        for (var day = 0; day < 8; day++) sim.NextMorning();
        Assert.Equal(10_000, sim.Reports[0].LoanPaymentCents);
        Assert.All(sim.Reports.Skip(1), r => Assert.Equal(0, r.LoanPaymentCents));
    }

    [Fact]
    public void The_last_scheduled_payment_can_pay_it_off()
    {
        var sim = NewSim(Terms(principal: 1_500, minimum: 1_000, every: 1));

        sim.NextMorning();
        sim.NextMorning();

        Assert.Equal([1_000, 500], sim.Reports.Select(r => r.LoanPaymentCents));
        Assert.True(sim.Loan!.PaidOff);
        Assert.True(sim.Story.HasFired("loan_paid_off"));
    }

    [Fact]
    public void Short_cash_pays_what_it_can_carries_the_rest_and_never_goes_negative()
    {
        var huge = 100_000_000L;
        var sim = NewSim(Terms(principal: 10 * huge, minimum: huge, every: 2));

        sim.NextMorning();
        sim.RunToClose();

        var day2 = sim.Reports[^1];
        Assert.Equal(day2.CashStartCents + day2.ProfitCents, day2.CashEndCents);
        Assert.True(day2.LoanPaymentCents > 0);
        Assert.Equal(0, sim.Economy.CashCents);
        Assert.Equal(huge - day2.LoanPaymentCents, sim.Loan!.ArrearsCents);
        Assert.Equal("loan_short", sim.Story.Pending[^1]);

        // Two days later the arrears are due again on top of the new minimum; the game just goes on.
        sim.ReadAll();
        sim.Run(new StartNextDay());
        sim.NextMorning();
        sim.RunToClose();
        Assert.Equal(4, sim.Clock.Day);
        Assert.True(sim.Economy.CashCents >= 0);
        Assert.Equal(2 * huge - sim.Reports[1].LoanPaymentCents - sim.Reports[3].LoanPaymentCents, sim.Loan.ArrearsCents);
        Assert.False(sim.Story.Pending.Contains("loan_short"), "the worried line has a 14-day cooldown");
    }

    [Fact]
    public void An_extra_payment_settles_arrears_first()
    {
        var sim = NewSim(Terms(principal: 100_000_000, minimum: 50_000_000, every: 1));
        sim.RunToClose();
        var arrears = sim.Loan!.ArrearsCents;
        Assert.True(arrears > 0);
        Assert.Equal(0, sim.Economy.CashCents);

        // Next day's takings, paid in during business hours.
        sim.Run(new StartNextDay());
        sim.WalkPlayerTo(Customers.CustomerScenario.CashierCell);
        sim.Run(new OpenStore());
        sim.TickUntil(() => sim.Economy.CashCents > 0);
        var cash = sim.Economy.CashCents;
        sim.Run(new PayLoan(cash));

        Assert.Equal(arrears - cash, sim.Loan.ArrearsCents);
        Assert.Equal(100_000_000 - sim.Reports[0].LoanPaymentCents - cash, sim.Loan.BalanceCents);
    }
}
