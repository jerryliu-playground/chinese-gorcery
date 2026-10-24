using ChineseGrocery.Sim.Inventory;

namespace ChineseGrocery.Sim.Save;

public sealed record StockSave(StockLocation Location, IReadOnlyList<StockLot> Lots);
