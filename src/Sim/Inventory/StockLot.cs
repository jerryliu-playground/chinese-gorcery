namespace ChineseGrocery.Sim.Inventory;

/// <summary>Units of one product that share an expiry day and purchase cost.</summary>
/// <param name="BestBeforeDay">Last day the goods can be sold; discarded at that day's closing.</param>
public sealed record StockLot(string ProductId, int Quantity, int BestBeforeDay, long UnitCostCents);
