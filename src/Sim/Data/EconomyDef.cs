using System.Text.Json.Serialization;

namespace ChineseGrocery.Sim.Data;

/// <summary>Root of economy.json.</summary>
/// <param name="OpenMinuteOfDay">Minute of the day (0-1439) the store opens.</param>
/// <param name="CloseMinuteOfDay">Minute of the day the store closes (exclusive, max 1440).</param>
/// <param name="SpeedMultipliers">Speed steps besides pause, ascending.</param>
public sealed record EconomyDef(
    long StartingCashCents,
    int MinutesPerTick,
    int OpenMinuteOfDay,
    int CloseMinuteOfDay,
    int RealSecondsPerOpenPhaseAt1x,
    IReadOnlyList<int> SpeedMultipliers,
    IReadOnlyList<ArrivalBucket> CustomerArrivals,
    int FixtureSellRefundPermille,
    [property: JsonPropertyName("_tbd")] bool Tbd = false);

/// <summary>From <paramref name="StartMinuteOfDay"/> until the next bucket (or closing), customers arrive at this rate.</summary>
public sealed record ArrivalBucket(int StartMinuteOfDay, int CustomersPerHour);
