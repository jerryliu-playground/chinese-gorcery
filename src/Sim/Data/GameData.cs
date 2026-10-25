namespace ChineseGrocery.Sim.Data;

/// <summary>
/// All static data tables, loaded once at startup and validated. <see cref="Products"/> also holds every dish from
/// <see cref="Recipes"/> (see <see cref="RecipeDef.ToProduct"/>).
/// </summary>
public sealed record GameData(
    IReadOnlyList<ProductDef> Products,
    IReadOnlyList<FixtureDef> Fixtures,
    IReadOnlyList<CustomerTypeDef> CustomerTypes,
    EmployeesDef Employees,
    IReadOnlyList<MeatCutDef> MeatCuts,
    IReadOnlyList<StageDef> Stages,
    EconomyDef Economy,
    IReadOnlyList<RecipeDef> Recipes,
    ReputationDef? Reputation = null);
