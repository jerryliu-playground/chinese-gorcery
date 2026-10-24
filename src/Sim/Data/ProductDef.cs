using System.Text.Json.Serialization;

namespace ChineseGrocery.Sim.Data;

/// <summary>Row of products.json.</summary>
/// <param name="Orderable">False for goods that only come from in-store processing (e.g. meat cuts).</param>
public sealed record ProductDef(
    string Id,
    string NameKey,
    Department Department,
    long CostCents,
    long SuggestedPriceCents,
    int ShelfLifeDays,
    StorageType Storage,
    int UnitsPerSlot,
    bool Orderable,
    [property: JsonPropertyName("_tbd")] bool Tbd = false);
