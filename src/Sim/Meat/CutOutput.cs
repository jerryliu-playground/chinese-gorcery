namespace ChineseGrocery.Sim.Meat;

/// <summary>Boxes of one product produced by cutting a primal, each carrying its share of the primal's cost.</summary>
public sealed record CutOutput(string ProductId, int Packages, long UnitCostCents);
