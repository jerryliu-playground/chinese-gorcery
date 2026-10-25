using System.Text.Json.Serialization;

namespace ChineseGrocery.Sim.Data;

/// <summary>Row of products.json.</summary>
/// <param name="UnitsPerShelfSlot">Units one shelf / display slot holds.</param>
/// <param name="UnitsPerStorageSlot">Units one backroom / cold storage slot holds (bulky goods hold fewer).</param>
/// <param name="WeightGrams">Weight of one unit (a whole primal, or one packaged box of cut meat); required for meat.</param>
/// <param name="FreshnessDays">Produce: days spent in each freshness tier from arrival; they add up to shelfLifeDays.</param>
/// <param name="Live">Live seafood: how it dies and how it is sold.</param>
/// <param name="IngredientOnly">Bought only for the kitchen: never put out for sale or wanted by shoppers.</param>
/// <param name="Import">Bought from the overseas importer too (cheaper, slower), at this cost and minimum order.</param>
/// <param name="Orderable">False for goods that only come from in-store processing (e.g. meat cuts).</param>
public sealed record ProductDef(
    string Id,
    string NameKey,
    Department Department,
    long CostCents,
    long SuggestedPriceCents,
    int ShelfLifeDays,
    StorageType Storage,
    int UnitsPerShelfSlot,
    int UnitsPerStorageSlot,
    bool Orderable,
    int? WeightGrams = null,
    string? Sprite = null,
    TierValues? FreshnessDays = null,
    ProduceCategory? Category = null,
    ProduceColor? Color = null,
    LiveSeafoodDef? Live = null,
    bool IngredientOnly = false,
    ImportDef? Import = null,
    [property: JsonPropertyName("_tbd")] bool Tbd = false);
