using System.Text.Json.Serialization;

namespace ChineseGrocery.Sim.Data;

/// <summary>Row of recipes.json: a dish the kitchen cooks from ingredients and sells hot at a deli station.</summary>
/// <param name="Portions">Portions one cooking makes.</param>
/// <param name="CookMinutes">Time at the stove for one cooking, at 1000 permille work speed and the lowest skill.</param>
/// <param name="Station">The kind of deli station it is sold from.</param>
/// <param name="UnitsPerSlot">Portions one station slot holds.</param>
public sealed record RecipeDef(
    string Id,
    string NameKey,
    IReadOnlyList<IngredientDef> Ingredients,
    int Portions,
    long SuggestedPriceCents,
    int CookMinutes,
    FixtureKind Station,
    int UnitsPerSlot,
    string? Sprite = null,
    [property: JsonPropertyName("_tbd")] bool Tbd = false)
{
    /// <summary>The dish as a product, so stock, prices, baskets and sales handle it like any other goods.</summary>
    public ProductDef ToProduct() =>
        new(Id, NameKey, Department.Deli, CostCents: 0, SuggestedPriceCents, ShelfLifeDays: 1, StorageType.Hot,
            UnitsPerSlot, UnitsPerStorageSlot: 1, Orderable: false, Sprite: Sprite, Tbd: Tbd);
}

public sealed record IngredientDef(string ProductId, int Quantity);
