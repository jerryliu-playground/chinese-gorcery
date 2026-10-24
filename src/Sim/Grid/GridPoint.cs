namespace ChineseGrocery.Sim.Grid;

/// <summary>A cell coordinate on the store grid (one cell = 32x32 px on screen).</summary>
public readonly record struct GridPoint(int X, int Y);
