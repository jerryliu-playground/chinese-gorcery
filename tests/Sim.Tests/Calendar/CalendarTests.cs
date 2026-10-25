using ChineseGrocery.Sim.Calendar;
using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Time;
using ChineseGrocery.Sim.Data;
using static ChineseGrocery.Sim.Tests.Calendar.CalendarScenario;

namespace ChineseGrocery.Sim.Tests.Calendar;

/// <summary>Spec 15.1: a year of four 28-day seasons from spring day 1; dates, season changes and weekdays.</summary>
public class CalendarTests
{
    private static readonly CalendarDef Cal = SimTestKit.RealData.Calendar!;

    [Theory]
    [InlineData(1, 1, 0, 1)]
    [InlineData(28, 1, 0, 28)]
    [InlineData(29, 1, 1, 1)]
    [InlineData(57, 1, 2, 1)]
    [InlineData(112, 1, 3, 28)]
    [InlineData(113, 2, 0, 1)]
    [InlineData(224, 2, 3, 28)]
    public void Day_numbers_map_to_year_season_and_day(int day, int year, int season, int dayOfSeason)
    {
        Assert.Equal(new CalendarDate(year, season, dayOfSeason), GameCalendar.DateOf(Cal, day));
        Assert.Equal(day, GameCalendar.DayOf(Cal, new CalendarDate(year, season, dayOfSeason)));
    }

    [Fact]
    public void The_year_is_four_seasons_of_28_days_starting_in_spring()
    {
        Assert.Equal(["spring", "summer", "autumn", "winter"], Cal.Seasons.Select(s => s.Id));
        Assert.All(Cal.Seasons, s => Assert.Equal(28, s.Days));
        Assert.Equal(112, Cal.DaysPerYear);
        Assert.Equal("spring", SimTestKit.NewSim().Season!.Id);
    }

    [Fact]
    public void Every_day_round_trips_and_follows_the_one_before()
    {
        var before = GameCalendar.DateOf(Cal, 1);
        for (var day = 2; day <= 400; day++)
        {
            var date = GameCalendar.DateOf(Cal, day);
            Assert.Equal(day, GameCalendar.DayOf(Cal, date));
            var next = before.DayOfSeason < 28 ? before with { DayOfSeason = before.DayOfSeason + 1 }
                : before.SeasonIndex < 3 ? new CalendarDate(before.Year, before.SeasonIndex + 1, 1)
                : new CalendarDate(before.Year + 1, 0, 1);
            Assert.Equal(next, date);
            before = date;
        }
    }

    [Fact]
    public void Playing_through_a_season_change_moves_to_summer_and_the_weekday_keeps_counting()
    {
        var sim = SimTestKit.NewSim(data: Quiet(SimTestKit.Data));
        var weekdays = new List<int>();
        for (var day = 1; day < 30; day++)
        {
            weekdays.Add(sim.Clock.Weekday);
            sim.RunFullDay();
        }

        Assert.Equal(30, sim.Clock.Day);
        Assert.Equal(new CalendarDate(1, 1, 2), sim.Date);
        Assert.Equal("summer", sim.Season!.Id);
        Assert.Equal(Enumerable.Range(0, 29).Select(i => i % 7), weekdays);
        Assert.Equal(1, sim.Clock.Weekday);
    }

    [Fact]
    public void Season_report_comes_at_the_close_of_the_last_day_of_the_season()
    {
        var sim = SimTestKit.NewSim(data: Quiet(SimTestKit.Data));
        QuarterReportReady? ready = null;
        for (var day = 1; day <= 28; day++)
        {
            sim.DrainEvents();
            sim.OpenAndRunToClose();
            ready = sim.DrainEvents().OfType<QuarterReportReady>().SingleOrDefault();
            if (day < 28) Assert.Null(ready);
            sim.Run(new StartNextDay());
        }

        Assert.NotNull(ready);
        Assert.Equal((1, 0, 1, 28), (ready.Report.Year, ready.Report.SeasonIndex, ready.Report.FirstDay, ready.Report.LastDay));
        Assert.Equal(sim.Reports.Sum(r => r.ProfitCents), ready.Report.ProfitCents);
        Assert.Single(sim.QuarterReports());
    }
}
