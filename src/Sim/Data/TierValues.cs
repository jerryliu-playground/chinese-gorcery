namespace ChineseGrocery.Sim.Data;

/// <summary>One number per freshness tier (days, price factor, score...).</summary>
public sealed record TierValues(int Fresh, int Normal, int Ripe)
{
    public int Of(FreshnessTier tier) => tier switch
    {
        FreshnessTier.Fresh => Fresh,
        FreshnessTier.Normal => Normal,
        _ => Ripe,
    };
}
