using System.Text.Json.Serialization;

namespace ChineseGrocery.Sim.Data;

/// <summary>Row of customer_types.json.</summary>
/// <param name="ArrivalWeight">Relative share of arriving customers that are this type.</param>
/// <param name="PriceComfortRatioPermille">At or below this price / reference ratio the customer always buys.</param>
/// <param name="PriceCeilingRatioPermille">Above this ratio the customer never buys; in between, the buy chance falls linearly.</param>
/// <param name="MinBuyChancePermille">Buy chance right at the ceiling.</param>
public sealed record CustomerTypeDef(
    string Id,
    string NameKey,
    IReadOnlyList<DepartmentWeight> Preferences,
    int ArrivalWeight,
    int PriceComfortRatioPermille,
    int PriceCeilingRatioPermille,
    int MinBuyChancePermille,
    int MoveCellsPerMinute,
    int BrowseMinutesPerItem,
    int PatienceMinutes,
    IntRange ShoppingListSize,
    SpriteDef? Sprite = null,
    [property: JsonPropertyName("_tbd")] bool Tbd = false);

public sealed record DepartmentWeight(Department Department, int Weight);
