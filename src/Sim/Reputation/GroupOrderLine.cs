namespace ChineseGrocery.Sim.Reputation;

/// <summary>Units of one product a group order asks for, and what the buyer pays for each.</summary>
public sealed record GroupOrderLine(string ProductId, int Units, long UnitPriceCents);
