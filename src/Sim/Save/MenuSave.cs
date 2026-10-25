using ChineseGrocery.Sim.Deli;
using ChineseGrocery.Sim.Inventory;

namespace ChineseGrocery.Sim.Save;

/// <summary>A deli station slot's menu item and the portions already cooked for it today.</summary>
public sealed record MenuSave(StockLocation Slot, MenuItem Item, int CookedToday);
