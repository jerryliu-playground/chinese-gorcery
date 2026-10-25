namespace ChineseGrocery.Sim.Data;

/// <summary>File names of the data tables under /data.</summary>
public static class DataFiles
{
    public const string Products = "products.json";
    public const string Fixtures = "fixtures.json";
    public const string CustomerTypes = "customer_types.json";
    public const string Employees = "employees.json";
    public const string MeatCuts = "meat_cuts.json";
    public const string Stages = "stages.json";
    public const string Economy = "economy.json";
    public const string Recipes = "recipes.json";
    public const string Reputation = "reputation.json";

    public static readonly IReadOnlyList<string> All =
        [Products, Fixtures, CustomerTypes, Employees, MeatCuts, Stages, Economy, Recipes, Reputation];
}
