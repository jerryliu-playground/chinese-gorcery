namespace ChineseGrocery.Sim.Time;

/// <summary>Current day, phase and in-game minute. Only moves while the store is open.</summary>
public sealed class GameClock
{
    internal GameClock(int openMinuteOfDay) => MinuteOfDay = openMinuteOfDay;

    public int Day { get; private set; } = 1;
    public DayPhase Phase { get; private set; } = DayPhase.PreOpen;
    public int MinuteOfDay { get; private set; }

    internal void Open() => Phase = DayPhase.Open;

    internal void Advance(int minutes) => MinuteOfDay += minutes;

    internal void Close() => Phase = DayPhase.Closed;

    internal void StartNextDay(int openMinuteOfDay)
    {
        Day++;
        Phase = DayPhase.PreOpen;
        MinuteOfDay = openMinuteOfDay;
    }
}
