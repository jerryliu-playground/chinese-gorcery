using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Grid;

/// <summary>PreOpen only: moves a placed fixture (and anything stored in it) for free.</summary>
public sealed record MoveFixture(int InstanceId, GridPoint Origin) : SimCommand;
