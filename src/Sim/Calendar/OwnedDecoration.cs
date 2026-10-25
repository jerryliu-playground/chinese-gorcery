namespace ChineseGrocery.Sim.Calendar;

/// <summary>A decoration the store owns: hung at a back-wall cell, or stored when <paramref name="WallX"/> is null.</summary>
public sealed record OwnedDecoration(string DecorationId, int? WallX);
