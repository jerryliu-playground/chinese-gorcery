using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Tests.Time;

public class DayCycleTests
{
    private static readonly int Open = SimTestKit.Data.Economy.OpenMinuteOfDay;
    private static readonly int Close = SimTestKit.Data.Economy.CloseMinuteOfDay;

    [Fact]
    public void New_game_starts_on_day_one_before_opening()
    {
        var sim = SimTestKit.NewSim();

        Assert.Equal(1, sim.Clock.Day);
        Assert.Equal(DayPhase.PreOpen, sim.Clock.Phase);
        Assert.Equal(Open, sim.Clock.MinuteOfDay);
        Assert.Equal(SimTestKit.Data.Economy.StartingCashCents, sim.Economy.CashCents);
        Assert.Equal(1, sim.DrainEvents().Single<DayStarted>().Day);
    }

    [Fact]
    public void Time_does_not_move_before_opening()
    {
        var sim = SimTestKit.NewSim();

        for (var i = 0; i < 100; i++) sim.Tick();

        Assert.Equal(DayPhase.PreOpen, sim.Clock.Phase);
        Assert.Equal(Open, sim.Clock.MinuteOfDay);
    }

    [Fact]
    public void Commands_take_effect_only_on_tick()
    {
        var sim = SimTestKit.NewSim();

        sim.Enqueue(new OpenStore());
        Assert.Equal(DayPhase.PreOpen, sim.Clock.Phase);

        sim.Tick();
        Assert.Equal(DayPhase.Open, sim.Clock.Phase);
    }

    [Fact]
    public void Each_open_tick_advances_the_configured_seconds()
    {
        var sim = SimTestKit.NewSim();
        var step = SimTestKit.Data.Economy.SecondsPerTick;
        sim.Run(new OpenStore());
        var after = sim.Clock.SecondOfDay;

        sim.Tick();

        Assert.Equal(Open * 60 + step, after);
        Assert.Equal(after + step, sim.Clock.SecondOfDay);
        Assert.Equal(sim.Clock.SecondOfDay / 60, sim.Clock.MinuteOfDay);
    }

    [Fact]
    public void A_full_open_day_takes_the_open_hours_in_ticks()
    {
        var sim = SimTestKit.NewSim(data: SimTestKit.Data with
        {
            Economy = SimTestKit.Data.Economy with { CustomerArrivals = [new(SimTestKit.Data.Economy.OpenMinuteOfDay, 0)] },
        });
        sim.Run(new OpenStore());
        var ticks = 1;

        while (sim.Clock.Phase == DayPhase.Open)
        {
            sim.Tick();
            ticks++;
        }

        Assert.Equal((Close - Open) * 60 / SimTestKit.Data.Economy.SecondsPerTick, ticks);
    }

    [Fact]
    public void Store_closes_at_closing_time_and_publishes_one_report()
    {
        var sim = SimTestKit.NewSim();
        sim.DrainEvents();

        sim.OpenAndRunToClose();
        var events = sim.DrainEvents();

        Assert.Equal(DayPhase.Closed, sim.Clock.Phase);
        Assert.Equal(Close, sim.Clock.MinuteOfDay);
        Assert.Equal(1, events.Single<StoreClosed>().Day);
        Assert.Equal(1, events.Single<DailyReportReady>().Report.Day);
        Assert.Single(sim.Reports);
    }

    [Fact]
    public void Closed_day_waits_for_confirmation()
    {
        var sim = SimTestKit.NewSim();
        sim.OpenAndRunToClose();

        for (var i = 0; i < 50; i++) sim.Tick();

        Assert.Equal(DayPhase.Closed, sim.Clock.Phase);
        Assert.Equal(1, sim.Clock.Day);
    }

    [Fact]
    public void Starting_next_day_returns_to_pre_open()
    {
        var sim = SimTestKit.NewSim();
        sim.OpenAndRunToClose();
        sim.DrainEvents();

        sim.Run(new StartNextDay());

        Assert.Equal(2, sim.Clock.Day);
        Assert.Equal(DayPhase.PreOpen, sim.Clock.Phase);
        Assert.Equal(Open, sim.Clock.MinuteOfDay);
        Assert.Equal(2, sim.DrainEvents().Single<DayStarted>().Day);
    }

    [Fact]
    public void Commands_in_the_wrong_phase_are_rejected()
    {
        var sim = SimTestKit.NewSim();

        sim.Run(new StartNextDay());
        Assert.Equal(RejectReason.WrongPhase, sim.DrainEvents().Single<CommandRejected>().Reason);

        sim.Run(new OpenStore());
        sim.Run(new OpenStore());
        Assert.Equal(RejectReason.WrongPhase, sim.DrainEvents().Single<CommandRejected>().Reason);

        sim.OpenAndRunToClose();
        sim.DrainEvents();
        sim.Run(new OpenStore());
        Assert.Equal(RejectReason.WrongPhase, sim.DrainEvents().Single<CommandRejected>().Reason);
        Assert.Equal(DayPhase.Closed, sim.Clock.Phase);
    }

    [Fact]
    public void Tick_count_counts_every_tick()
    {
        var sim = SimTestKit.NewSim();

        sim.Tick();
        sim.Tick();

        Assert.Equal(2, sim.TickCount);
    }
}
