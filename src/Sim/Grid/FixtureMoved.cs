using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Grid;

public sealed record FixtureMoved(int InstanceId, GridPoint From, GridPoint To, Rotation Rotation) : SimEvent;
