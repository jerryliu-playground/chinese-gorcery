namespace ChineseGrocery.Sim.Data;

/// <summary>All static data tables, loaded once at startup and validated.</summary>
public sealed record GameData(
    IReadOnlyList<ProductDef> Products,
    IReadOnlyList<FixtureDef> Fixtures,
    IReadOnlyList<CustomerTypeDef> CustomerTypes,
    EmployeesDef Employees,
    IReadOnlyList<MeatCutDef> MeatCuts,
    IReadOnlyList<StageDef> Stages,
    EconomyDef Economy);
