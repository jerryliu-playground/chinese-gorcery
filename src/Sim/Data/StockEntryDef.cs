namespace ChineseGrocery.Sim.Data;

/// <summary>Goods already in the store on day 1 (free of charge).</summary>
public sealed record StockEntryDef(string ProductId, int Quantity);
