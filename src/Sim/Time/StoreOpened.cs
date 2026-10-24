using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Time;

public sealed record StoreOpened(int Day) : SimEvent;
