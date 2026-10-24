using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Inventory;

public sealed record ShelfSlotAssigned(StockLocation Slot, string? ProductId) : SimEvent;
