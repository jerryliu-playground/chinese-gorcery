using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Inventory;

/// <summary>Display-only: goods went onto a shelf / display slot.</summary>
public sealed record ItemsShelved(StockLocation Slot, string ProductId, int Quantity, bool WasEmpty) : SimEvent;
