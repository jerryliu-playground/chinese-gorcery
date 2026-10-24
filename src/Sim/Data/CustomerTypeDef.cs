using System.Text.Json.Serialization;

namespace ChineseGrocery.Sim.Data;

/// <summary>Row of customer_types.json.</summary>
/// <param name="PriceSensitivityPermille">0 = ignores price, 1000 = maximally sensitive.</param>
public sealed record CustomerTypeDef(
    string Id,
    string NameKey,
    IReadOnlyList<DepartmentWeight> Preferences,
    int PriceSensitivityPermille,
    int PatienceMinutes,
    IntRange ShoppingListSize,
    [property: JsonPropertyName("_tbd")] bool Tbd = false);

public sealed record DepartmentWeight(Department Department, int Weight);
