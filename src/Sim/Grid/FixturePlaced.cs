using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Grid;

public sealed record FixturePlaced(int InstanceId, string FixtureId, GridPoint Origin, long CostCents) : SimEvent;
