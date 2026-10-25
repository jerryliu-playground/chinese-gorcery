using ChineseGrocery.Sim.Data;

namespace ChineseGrocery.Sim.Tests.Data;

/// <summary>Spec 13.2: recipes load as dishes and are checked like the other tables.</summary>
public class DeliDataTests
{
    private static readonly GameData Valid = GameDataLoader.LoadFromDirectory(RepoPaths.Data);

    private static IReadOnlyList<string> ErrorsFor(GameData data) => DataValidator.Validate(data);

    private static GameData WithRecipe(string id, Func<RecipeDef, RecipeDef> change) =>
        Valid with { Recipes = Valid.Recipes.Select(r => r.Id == id ? change(r) : r).ToList() };

    [Fact]
    public void Every_recipe_is_also_a_dish_product_sold_hot_the_same_day()
    {
        Assert.Empty(ErrorsFor(Valid));
        Assert.Equal(10, Valid.Recipes.Count);
        foreach (var recipe in Valid.Recipes)
        {
            var dish = Valid.Products.Single(p => p.Id == recipe.Id);
            Assert.Equal((Department.Deli, StorageType.Hot, 1, false), (dish.Department, dish.Storage, dish.ShelfLifeDays, dish.Orderable));
            Assert.Equal(recipe.SuggestedPriceCents, dish.SuggestedPriceCents);
        }
    }

    [Fact]
    public void Duck_and_pork_shoulder_are_bought_for_the_kitchen_only()
    {
        Assert.All(new[] { "whole_duck", "pork_shoulder" }, id => Assert.True(Valid.Products.Single(p => p.Id == id) is { IngredientOnly: true, Orderable: true }));
    }

    [Fact]
    public void Unknown_ingredients_are_reported()
    {
        var data = WithRecipe("dumplings", r => r with { Ingredients = [new IngredientDef("dragon", 1)] });

        Assert.Contains(ErrorsFor(data), e => e.Contains("ingredient 'dragon' does not exist"));
    }

    [Fact]
    public void A_slot_must_hold_at_least_one_cookings_portions()
    {
        var data = WithRecipe("roast_duck", r => r with { UnitsPerSlot = r.Portions - 1 });

        Assert.Contains(ErrorsFor(data), e => e.Contains("'roast_duck'") && e.Contains("unitsPerSlot"));
    }

    [Fact]
    public void Dishes_are_sold_from_deli_stations_only()
    {
        var data = WithRecipe("char_siu", r => r with { Station = FixtureKind.Shelf });

        Assert.Contains(ErrorsFor(data), e => e.Contains("'char_siu'") && e.Contains("deli station"));
    }

    [Fact]
    public void Rush_customers_must_be_real_types()
    {
        var deli = Valid.Economy.Deli!;
        var rush = deli.RushArrivals.Select((b, i) => i == 1 ? b with { TypeWeights = [new TypeWeight("tourist", 1)] } : b).ToList();
        var data = Valid with { Economy = Valid.Economy with { Deli = deli with { RushArrivals = rush } } };

        Assert.Contains(ErrorsFor(data), e => e.Contains("'tourist' does not exist"));
    }
}
