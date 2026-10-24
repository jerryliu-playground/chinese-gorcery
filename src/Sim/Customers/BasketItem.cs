using ChineseGrocery.Sim.Inventory;

namespace ChineseGrocery.Sim.Customers;

/// <summary>Goods a customer picked up, and the slot they came from (where they go back if the customer gives up).</summary>
/// <param name="PriceCents">Unit price when picked (marked down for older produce); -1 in saves from before markdowns.</param>
public sealed record BasketItem(StockLot Lot, StockLocation From, long PriceCents = -1);
