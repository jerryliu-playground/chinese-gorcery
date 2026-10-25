namespace ChineseGrocery.Sim.Imports;

/// <summary>An import order at sea, arriving at the store on <paramref name="ArrivalDay"/> (before opening).</summary>
public sealed record Shipment(int Id, string ProductId, int Quantity, long UnitCostCents, int OrderedDay, int ArrivalDay);
