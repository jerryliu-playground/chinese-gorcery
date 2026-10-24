using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Grid;

/// <summary>PreOpen only: moves and/or turns a placed fixture (and anything stored in it) for free.</summary>
public sealed record MoveFixture(int InstanceId, GridPoint Origin, Rotation Rotation) : SimCommand;
