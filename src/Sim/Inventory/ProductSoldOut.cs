using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Inventory;

/// <summary>A customer took the last unit from a shelf / display slot.</summary>
public sealed record ProductSoldOut(StockLocation Slot, string ProductId) : SimEvent;
