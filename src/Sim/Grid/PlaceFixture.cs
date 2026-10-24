using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Grid;

/// <summary>PreOpen only: buys a fixture and puts its top-left corner at <paramref name="Origin"/>.</summary>
public sealed record PlaceFixture(string FixtureId, GridPoint Origin) : SimCommand;
