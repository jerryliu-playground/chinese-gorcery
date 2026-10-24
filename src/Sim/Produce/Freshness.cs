using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Inventory;

namespace ChineseGrocery.Sim.Produce;

/// <summary>
/// Freshness of a batch from its age: a batch arrives Fresh and moves through Normal and Ripe by the product's
/// freshnessDays; past its last day it is Spoiled (and is thrown out at that day's closing). Non-produce is always Fresh.
/// </summary>
public static class Freshness
{
    public static FreshnessTier TierOf(ProductDef product, StockLot lot, int today)
    {
        if (product.FreshnessDays is not { } days) return FreshnessTier.Fresh;

        var arrived = lot.BestBeforeDay - product.ShelfLifeDays + 1;
        var age = today - arrived;
        if (age < days.Fresh) return FreshnessTier.Fresh;
        if (age < days.Fresh + days.Normal) return FreshnessTier.Normal;
        return age < days.Fresh + days.Normal + days.Ripe ? FreshnessTier.Ripe : FreshnessTier.Spoiled;
    }
}
