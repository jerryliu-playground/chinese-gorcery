namespace ChineseGrocery.Sim.Data;

/// <summary>
/// All static data tables, loaded once at startup and validated. <see cref="Products"/> also holds every dish from
/// <see cref="Recipes"/> (see <see cref="RecipeDef.ToProduct"/>). <see cref="Story"/> is null when a game runs
/// without the story (tests that build data by hand); then there are no dialogues, tutorial, loan or competition. Without <see cref="Calendar"/>, <see cref="Festivals"/> or
/// <see cref="Events"/> there are no seasons, festivals or random events (the loader always provides all three).
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
    StoryData? Story = null,
    ReputationDef? Reputation = null,
    CalendarDef? Calendar = null,
    FestivalsDef? Festivals = null,
    EventsDef? Events = null);
