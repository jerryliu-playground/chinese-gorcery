namespace ChineseGrocery.Sim.Inventory;

/// <summary>A paid order (or the part of one counted against one storage area) waiting for delivery the next morning.</summary>
/// <param name="DestinationInstanceId">The storage area this part was counted against; null in saves from before splitting.</param>
public sealed record PendingOrder(string ProductId, int Quantity, long UnitCostCents, int OrderedOnDay, int? DestinationInstanceId = null);
