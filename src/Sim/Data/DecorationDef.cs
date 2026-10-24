using ChineseGrocery.Sim.Grid;

namespace ChineseGrocery.Sim.Data;

/// <summary>Display-only ornament at a cell; y = -1 is the wall above the top row. Takes no floor space.</summary>
public sealed record DecorationDef(string Sprite, GridPoint Cell);
