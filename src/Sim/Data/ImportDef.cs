namespace ChineseGrocery.Sim.Data;

/// <summary>products.json "import": what the overseas importer charges for one unit and the smallest order it takes.</summary>
public sealed record ImportDef(long CostCents, int MinUnits);
