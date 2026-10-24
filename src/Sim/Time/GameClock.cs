namespace ChineseGrocery.Sim.Time;

/// <summary>Current day, phase and in-game time of day (kept in seconds). Only moves while the store is open.</summary>
public sealed class GameClock
{
    internal GameClock(int openMinuteOfDay) => SecondOfDay = openMinuteOfDay * 60;

    public int Day { get; internal set; } = 1;
    public DayPhase Phase { get; internal set; } = DayPhase.PreOpen;
    public int SecondOfDay { get; internal set; }
    public int MinuteOfDay => SecondOfDay / 60;

    internal void Open() => Phase = DayPhase.Open;

    internal void Advance(int seconds) => SecondOfDay += seconds;

    internal void Close() => Phase = DayPhase.Closed;

    internal void StartNextDay(int openMinuteOfDay)
    {
        Day++;
        Phase = DayPhase.PreOpen;
        SecondOfDay = openMinuteOfDay * 60;
    }
}
