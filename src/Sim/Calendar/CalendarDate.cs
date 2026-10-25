namespace ChineseGrocery.Sim.Calendar;

/// <summary>A day on the game calendar: year (from 1), season (index into calendar.json) and day of the season (from 1).</summary>
public readonly record struct CalendarDate(int Year, int SeasonIndex, int DayOfSeason);
