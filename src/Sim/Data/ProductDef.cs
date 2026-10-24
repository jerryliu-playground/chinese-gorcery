using System.Text.Json.Serialization;

namespace ChineseGrocery.Sim.Data;

/// <summary>Row of products.json.</summary>
public sealed record ProductDef(
    string Id,
    string NameKey,
    Department Department,
    long CostCents,
    long SuggestedPriceCents,
    int ShelfLifeDays,
    StorageType Storage,
    int UnitsPerSlot,
    [property: JsonPropertyName("_tbd")] bool Tbd = false);
