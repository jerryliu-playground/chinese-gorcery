using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Grid;

public sealed record FixtureSold(int InstanceId, string FixtureId, long RefundCents) : SimEvent;
