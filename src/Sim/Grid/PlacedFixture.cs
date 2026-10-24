namespace ChineseGrocery.Sim.Grid;

/// <summary>A fixture standing in the store.</summary>
/// <param name="Origin">Top-left cell of the (rotated) footprint.</param>
/// <param name="PaidCents">What was paid to place it; sell refunds are a share of this.</param>
public sealed record PlacedFixture(int InstanceId, string FixtureId, GridPoint Origin, long PaidCents, Rotation Rotation = Rotation.None);
