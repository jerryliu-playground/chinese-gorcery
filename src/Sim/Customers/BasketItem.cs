using ChineseGrocery.Sim.Inventory;

namespace ChineseGrocery.Sim.Customers;

/// <summary>Goods a customer picked up, and the slot they came from (where they go back if the customer gives up).</summary>
public sealed record BasketItem(StockLot Lot, StockLocation From);
