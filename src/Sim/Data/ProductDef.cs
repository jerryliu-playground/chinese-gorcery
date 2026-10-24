using System.Text.Json.Serialization;

namespace ChineseGrocery.Sim.Data;

/// <summary>Row of products.json.</summary>
/// <param name="UnitsPerShelfSlot">Units one shelf / display slot holds.</param>
/// <param name="UnitsPerStorageSlot">Units one backroom / cold storage slot holds (bulky goods hold fewer).</param>
/// <param name="WeightGrams">Weight of one unit (a whole primal, or one packaged box of cut meat); required for meat.</param>
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
    [property: JsonPropertyName("_tbd")] bool Tbd = false);
