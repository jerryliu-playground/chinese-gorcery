using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChineseGrocery.Sim.Data;

/// <summary>Reads the JSON data tables and validates them. Any problem throws <see cref="DataValidationException"/>.</summary>
public static class GameDataLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false) },
    };

    public static GameData LoadFromDirectory(string directory)
    {
        var missing = DataFiles.All.Where(f => !File.Exists(Path.Combine(directory, f))).ToList();
        if (missing.Count > 0)
        {
            throw new DataValidationException(missing.Select(f => $"{f}: file not found in '{directory}'").ToList());
        }

        return Load(file => File.ReadAllText(Path.Combine(directory, file)));
    }

    /// <summary>Loads all tables through <paramref name="readFile"/>, which maps a file name to its JSON text.</summary>
    public static GameData Load(Func<string, string> readFile)
    {
        var errors = new List<string>();

        T? Parse<T>(string file) where T : class
        {
            try
            {
                return Deserialize<T>(file, readFile(file));
            }
            catch (DataValidationException ex)
            {
                errors.AddRange(ex.Errors);
                return null;
            }
        }

        var products = Parse<List<ProductDef>>(DataFiles.Products);
        var fixtures = Parse<List<FixtureDef>>(DataFiles.Fixtures);
        var customerTypes = Parse<List<CustomerTypeDef>>(DataFiles.CustomerTypes);
        var employees = Parse<EmployeesDef>(DataFiles.Employees);
        var meatCuts = Parse<List<MeatCutDef>>(DataFiles.MeatCuts);
        var stages = Parse<List<StageDef>>(DataFiles.Stages);
        var economy = Parse<EconomyDef>(DataFiles.Economy);
        var recipes = Parse<List<RecipeDef>>(DataFiles.Recipes);
        var reputation = Parse<ReputationDef>(DataFiles.Reputation);

        if (errors.Count > 0)
        {
            throw new DataValidationException(errors);
        }

        var data = new GameData([.. products!, .. recipes!.Select(r => r.ToProduct())], fixtures!, customerTypes!, employees!, meatCuts!, stages!, economy!, recipes!, reputation);
        DataValidator.ThrowIfInvalid(data);
        return data;
    }

    /// <summary>Parses one file's JSON strictly: missing fields, unknown fields, nulls and bad enum values are errors.</summary>
    public static T Deserialize<T>(string fileName, string json) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Options)
                ?? throw new DataValidationException([$"{fileName}: document is null"]);
        }
        catch (JsonException ex)
        {
            throw new DataValidationException([$"{fileName}: {ex.Message}"]);
        }
    }
}
