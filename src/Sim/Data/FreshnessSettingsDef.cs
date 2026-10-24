namespace ChineseGrocery.Sim.Data;

/// <summary>economy.json "freshness": price factor and display score of each tier.</summary>
public sealed record FreshnessSettingsDef(TierValues PriceFactorsPermille, TierValues Scores);
