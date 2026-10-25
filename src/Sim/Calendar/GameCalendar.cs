using ChineseGrocery.Sim.Data;

namespace ChineseGrocery.Sim.Calendar;

/// <summary>
/// Calendar arithmetic: day numbers (day 1 = first day of the first season of year 1) to dates and back, and where a
/// day falls around a festival. Festivals sit on fixed dates, the same every year.
/// </summary>
public static class GameCalendar
{
    public static CalendarDate DateOf(CalendarDef calendar, int day)
    {
        var length = calendar.DaysPerYear;
        var sinceStart = day - 1;
        var year = sinceStart >= 0 ? sinceStart / length : -((-sinceStart + length - 1) / length);
        var inYear = sinceStart - year * length;
        var season = 0;
        while (inYear >= calendar.Seasons[season].Days)
        {
            inYear -= calendar.Seasons[season].Days;
            season++;
        }

        return new CalendarDate(year + 1, season, inYear + 1);
    }

    public static int DayOf(CalendarDef calendar, CalendarDate date) =>
        (date.Year - 1) * calendar.DaysPerYear + calendar.Seasons.Take(date.SeasonIndex).Sum(s => s.Days) + date.DayOfSeason;

    public static int SeasonIndexOf(CalendarDef calendar, string seasonId) =>
        calendar.Seasons.ToList().FindIndex(s => s.Id == seasonId);

    /// <summary>First and last day number of a season.</summary>
    public static (int First, int Last) SeasonSpan(CalendarDef calendar, int year, int seasonIndex)
    {
        var first = DayOf(calendar, new CalendarDate(year, seasonIndex, 1));
        return (first, first + calendar.Seasons[seasonIndex].Days - 1);
    }

    /// <summary>Day number of the festival in a given year.</summary>
    public static int FestivalDay(CalendarDef calendar, FestivalDef festival, int year) =>
        DayOf(calendar, new CalendarDate(year, SeasonIndexOf(calendar, festival.Season), festival.Day));

    public static int PrepDays(FestivalsDef festivals, FestivalDef festival) => festival.PrepDays ?? festivals.DefaultPrepDays;

    public static int AfterDays(FestivalsDef festivals) => festivals.Curve.AfterSharesPermille.Count;

    /// <summary>
    /// Days from the festival to <paramref name="day"/> (negative before it) if the day is in its window (prep days,
    /// the day, the days after); otherwise null.
    /// </summary>
    public static int? WindowOffset(CalendarDef calendar, FestivalsDef festivals, FestivalDef festival, int day)
    {
        var year = DateOf(calendar, day).Year;
        for (var y = year - 1; y <= year + 1; y++)
        {
            var offset = day - FestivalDay(calendar, festival, y);
            if (offset >= -PrepDays(festivals, festival) && offset <= AfterDays(festivals)) return offset;
        }

        return null;
    }

    /// <summary>Days until the festival's next occurrence; 0 on the day itself.</summary>
    public static int DaysUntil(CalendarDef calendar, FestivalDef festival, int day)
    {
        var year = DateOf(calendar, day).Year;
        var thisYear = FestivalDay(calendar, festival, year);
        return thisYear >= day ? thisYear - day : FestivalDay(calendar, festival, year + 1) - day;
    }

    /// <summary>How much of the festival's peak demand applies at this offset from it (permille; 0 outside the window).</summary>
    public static int SharePermille(FestivalsDef festivals, FestivalDef festival, int offset)
    {
        var curve = festivals.Curve;
        var prep = PrepDays(festivals, festival);
        if (offset == 0) return 1000;
        if (offset > 0) return offset <= curve.AfterSharesPermille.Count ? curve.AfterSharesPermille[offset - 1] : 0;
        if (offset < -prep) return 0;
        if (prep == 1) return curve.EveSharePermille;

        return curve.PrepStartSharePermille + (curve.EveSharePermille - curve.PrepStartSharePermille) * (offset + prep) / (prep - 1);
    }
}
