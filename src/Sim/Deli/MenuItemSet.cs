using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Inventory;

namespace ChineseGrocery.Sim.Deli;

public sealed record MenuItemSet(StockLocation Slot, MenuItem? Item) : SimEvent;
