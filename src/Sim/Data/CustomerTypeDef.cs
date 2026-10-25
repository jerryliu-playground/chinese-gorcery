using System.Text.Json.Serialization;

namespace ChineseGrocery.Sim.Data;

/// <summary>Row of customer_types.json.</summary>
/// <param name="ArrivalWeight">Relative share of arriving customers that are this type.</param>
/// <param name="PriceComfortRatioPermille">At or below this price / reference ratio the customer always buys.</param>
/// <param name="PriceCeilingRatioPermille">Above this ratio the customer never buys; in between, the buy chance falls linearly.</param>
/// <param name="MinBuyChancePermille">Buy chance right at the ceiling.</param>
/// <param name="PrefersDiscount">Takes the cheapest (marked-down) batch on a stand instead of the freshest.</param>
/// <param name="ProduceCategories">If set, the only produce categories this type buys.</param>
/// <param name="ProductWeights">
/// Relative pick weights of particular products within their department; products not listed weigh
/// <see cref="DefaultProductWeight"/>.
/// </param>
/// <param name="LineKeys">Display only: translation keys of things this type sometimes says while shopping.</param>
/// <param name="MinStars">The type only comes in once the store has this many stars (reputation.json).</param>
/// <param name="RequiresDepartments">The type only comes in once the store has a fixture selling each of these departments.</param>
/// <param name="ArrivalWeightPerStar">Added to the arrival weight for each star above <paramref name="MinStars"/> (a weight of 0 stays 0).</param>
/// <param name="SeeksTrends">Comes in looking for this season's trending products.</param>
/// <param name="WantsEnglishLabels">Shops more happily with bilingual price labels; sometimes puzzled without them.</param>
/// <param name="OffersGroupOrders">Restaurant buyers: after paying, may propose a group order.</param>
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
    IReadOnlyList<string>? LineKeys = null,
    bool PrefersDiscount = false,
    IReadOnlyList<ProduceCategory>? ProduceCategories = null,
    IReadOnlyList<ProductWeight>? ProductWeights = null,
    [property: JsonPropertyName("_tbd")] bool Tbd = false,
    int MinStars = 1,
    IReadOnlyList<Department>? RequiresDepartments = null,
    int ArrivalWeightPerStar = 0,
    bool SeeksTrends = false,
    bool WantsEnglishLabels = false,
    bool OffersGroupOrders = false)
{
    public const int DefaultProductWeight = 10;

    public int WeightOf(string productId) =>
        ProductWeights?.FirstOrDefault(w => w.ProductId == productId)?.Weight ?? DefaultProductWeight;
}

public sealed record DepartmentWeight(Department Department, int Weight);

public sealed record ProductWeight(string ProductId, int Weight);
