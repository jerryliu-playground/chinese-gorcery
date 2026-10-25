namespace ChineseGrocery.Sim.Data;

/// <summary>
/// All static data tables, loaded once at startup and validated. <see cref="Products"/> also holds every dish from
/// <see cref="Recipes"/> (see <see cref="RecipeDef.ToProduct"/>). <see cref="Story"/> is null when a game runs
/// without the story (tests that build data by hand); then there are no dialogues, tutorial, loan or competition.
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
    StoryData? Story = null);
