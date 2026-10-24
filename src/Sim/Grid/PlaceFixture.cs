using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Grid;

/// <summary>PreOpen only: buys a fixture and puts the top-left corner of its rotated footprint at <paramref name="Origin"/>.</summary>
public sealed record PlaceFixture(string FixtureId, GridPoint Origin, Rotation Rotation = Rotation.None) : SimCommand;
