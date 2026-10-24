using ChineseGrocery.Sim.Data;

namespace ChineseGrocery.Sim.Tests.Data;

public class GameDataLoaderTests
{
    [Fact]
    public void Real_data_directory_loads_and_validates()
    {
        var data = GameDataLoader.LoadFromDirectory(RepoPaths.Data);

        Assert.NotEmpty(data.Products);
        Assert.NotEmpty(data.Fixtures);
        Assert.Equal(3, data.CustomerTypes.Count);
        Assert.NotEmpty(data.Stages);
        Assert.Empty(DataValidator.Validate(data));
    }

    [Fact]
    public void Missing_file_is_reported()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var ex = Assert.Throws<DataValidationException>(() => GameDataLoader.LoadFromDirectory(dir.FullName));
            Assert.Equal(DataFiles.All.Count, ex.Errors.Count);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Missing_required_field_is_rejected()
    {
        const string json = """[{ "id": "x", "nameKey": "product.x.name", "department": "snacks" }]""";

        var ex = Assert.Throws<DataValidationException>(() => GameDataLoader.Deserialize<List<ProductDef>>(DataFiles.Products, json));
        Assert.StartsWith(DataFiles.Products, ex.Errors.Single());
    }

    [Fact]
    public void Unknown_field_is_rejected()
    {
        var json = ProductJson("x", extra: """, "colour": "red" """);

        var ex = Assert.Throws<DataValidationException>(() => GameDataLoader.Deserialize<List<ProductDef>>(DataFiles.Products, json));
        Assert.Contains("colour", ex.Errors.Single());
    }

    [Fact]
    public void Unknown_enum_value_is_rejected()
    {
        var json = ProductJson("x").Replace("\"snacks\"", "\"seafood\"");

        Assert.Throws<DataValidationException>(() => GameDataLoader.Deserialize<List<ProductDef>>(DataFiles.Products, json));
    }

    [Fact]
    public void Null_for_required_string_is_rejected()
    {
        var json = ProductJson("x").Replace("\"product.x.name\"", "null");

        Assert.Throws<DataValidationException>(() => GameDataLoader.Deserialize<List<ProductDef>>(DataFiles.Products, json));
    }

    [Fact]
    public void Tbd_marker_is_read_and_defaults_to_false()
    {
        var withTbd = GameDataLoader.Deserialize<List<ProductDef>>(DataFiles.Products, ProductJson("x", extra: """, "_tbd": true """));
        var withoutTbd = GameDataLoader.Deserialize<List<ProductDef>>(DataFiles.Products, ProductJson("x"));

        Assert.True(withTbd.Single().Tbd);
        Assert.False(withoutTbd.Single().Tbd);
    }

    [Fact]
    public void Validation_errors_from_files_are_aggregated()
    {
        var ex = Assert.Throws<DataValidationException>(() => GameDataLoader.Load(file =>
            file == DataFiles.Products
                ? ProductJson("x").Replace("\"suggestedPriceCents\": 199", "\"suggestedPriceCents\": -1")
                : File.ReadAllText(Path.Combine(RepoPaths.Data, file))));

        Assert.Contains(ex.Errors, e => e.Contains("suggestedPriceCents"));
        // The meat cuts table references products that this replacement removed.
        Assert.Contains(ex.Errors, e => e.StartsWith(DataFiles.MeatCuts));
    }

    private static string ProductJson(string id, string extra = "") => $$"""
        [{
          "id": "{{id}}", "nameKey": "product.{{id}}.name", "department": "snacks",
          "costCents": 100, "suggestedPriceCents": 199, "shelfLifeDays": 30,
          "storage": "ambient", "unitsPerSlot": 10 {{extra}}
        }]
        """;
}
