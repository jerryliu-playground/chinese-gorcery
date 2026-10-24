namespace ChineseGrocery.Sim.Inventory;

/// <summary>A paid order waiting for delivery at the start of the next day.</summary>
public sealed record PendingOrder(string ProductId, int Quantity, long UnitCostCents, int OrderedOnDay);
