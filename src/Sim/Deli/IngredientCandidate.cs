using ChineseGrocery.Sim.Inventory;

namespace ChineseGrocery.Sim.Deli;

/// <summary>A batch the kitchen could cook with, and where it is.</summary>
public sealed record IngredientCandidate(StockLocation From, StockLot Lot);
