using ChineseGrocery.Sim.Inventory;

namespace ChineseGrocery.Sim.Save;

public sealed record SlotSave(StockLocation Slot, string ProductId);
