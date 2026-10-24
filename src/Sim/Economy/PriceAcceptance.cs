using ChineseGrocery.Sim.Data;

namespace ChineseGrocery.Sim.Economy;

/// <summary>
/// How likely a customer is to take an item at a given price, from the price / reference ratio:
/// always at or below the type's comfort ratio, never above its ceiling, falling linearly in between.
/// </summary>
public static class PriceAcceptance
{
    private const int Certain = 1000;

    public static int BuyChancePermille(CustomerTypeDef type, long priceCents, long referenceCents)
    {
        var ratio = priceCents * 1000 / referenceCents;
        if (ratio <= type.PriceComfortRatioPermille) return Certain;
        if (ratio > type.PriceCeilingRatioPermille) return 0;

        var span = type.PriceCeilingRatioPermille - type.PriceComfortRatioPermille;
        var drop = (Certain - type.MinBuyChancePermille) * (ratio - type.PriceComfortRatioPermille) / span;
        return (int)(Certain - drop);
    }
}
