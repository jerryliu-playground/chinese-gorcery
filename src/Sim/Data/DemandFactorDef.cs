namespace ChineseGrocery.Sim.Data;

/// <summary>
/// A demand multiplier for the products it matches: one product, a whole department, or a produce category (set
/// exactly one). 1000 = normal demand.
/// </summary>
public sealed record DemandFactorDef(int Permille, string? ProductId = null, Department? Department = null, ProduceCategory? Category = null)
{
    public bool Matches(ProductDef product) =>
        ProductId is { } id ? product.Id == id
        : Department is { } department ? product.Department == department
        : Category is { } category && product.Category == category;
}
