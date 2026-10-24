using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Time;

public sealed record DayStarted(int Day) : SimEvent;
