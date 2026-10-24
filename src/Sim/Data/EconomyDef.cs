using System.Text.Json.Serialization;
using ChineseGrocery.Sim.Grid;

namespace ChineseGrocery.Sim.Data;

/// <summary>Root of economy.json.</summary>
/// <param name="SecondsPerTick">Game time per simulation tick; must divide a minute evenly.</param>
/// <param name="OpenMinuteOfDay">Minute of the day (0-1439) the store opens.</param>
/// <param name="CloseMinuteOfDay">Minute of the day the store closes (exclusive, max 1440).</param>
/// <param name="SpeedMultipliers">Speed steps besides pause, ascending.</param>
/// <param name="StartingLayout">Fixtures already in the store on day 1, free of charge.</param>
/// <param name="StartingStock">Goods already in storage on day 1; the starting shelves are filled from it.</param>
public sealed record EconomyDef(
    long StartingCashCents,
    int SecondsPerTick,
    int OpenMinuteOfDay,
    int CloseMinuteOfDay,
    int RealSecondsPerOpenPhaseAt1x,
    IReadOnlyList<int> SpeedMultipliers,
    IReadOnlyList<ArrivalBucket> CustomerArrivals,
    int FixtureSellRefundPermille,
    TaskSettingsDef Tasks,
    IReadOnlyList<LayoutEntry> StartingLayout,
    IReadOnlyList<StockEntryDef>? StartingStock = null,
    [property: JsonPropertyName("_tbd")] bool Tbd = false);

/// <summary>From <paramref name="StartMinuteOfDay"/> until the next bucket (or closing), customers arrive at this rate.</summary>
public sealed record ArrivalBucket(int StartMinuteOfDay, int CustomersPerHour);

/// <summary>A fixture at a grid position (top-left of its rotated footprint).</summary>
/// <param name="Slots">For shelves and displays: the product each slot starts assigned to (null = left empty).</param>
public sealed record LayoutEntry(string FixtureId, GridPoint Origin, Rotation Rotation = Rotation.None, IReadOnlyList<string?>? Slots = null);
