using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Time;

public sealed record StoreClosed(int Day) : SimEvent;
