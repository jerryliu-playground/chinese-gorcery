using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Calendar;

/// <summary>Hangs an owned decoration on the back wall, at a wall cell or the first free one.</summary>
public sealed record HangDecoration(string DecorationId, int? WallX = null) : SimCommand;
