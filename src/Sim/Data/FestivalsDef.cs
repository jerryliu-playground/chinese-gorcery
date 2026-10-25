using System.Text.Json.Serialization;

namespace ChineseGrocery.Sim.Data;

/// <summary>Root of festivals.json.</summary>
/// <param name="DefaultPrepDays">Days of rising demand before a festival that does not set its own.</param>
/// <param name="OrderWindowDays">Festival goods can be ordered while their festival is 1 to this many days away.</param>
/// <param name="OffSeasonDemandPermille">Demand for festival goods outside their festival's window.</param>
/// <param name="DecorationBonusPermille">Demand for a festival's goods while one of its decorations is hung (prep and day only).</param>
public sealed record FestivalsDef(
    int DefaultPrepDays,
    int OrderWindowDays,
    int OffSeasonDemandPermille,
    int DecorationBonusPermille,
    FestivalCurveDef Curve,
    IReadOnlyList<FestivalDecorationDef> Decorations,
    IReadOnlyList<FestivalDef> Festivals,
    [property: JsonPropertyName("_tbd")] bool Tbd = false);

/// <summary>
/// How much of a festival's peak demand applies on each day around it, as a share (permille): rising in a straight line
/// from <paramref name="PrepStartSharePermille"/> on the first prep day to <paramref name="EveSharePermille"/> the
/// day before, all of it on the day, then one share per day after.
/// </summary>
public sealed record FestivalCurveDef(int PrepStartSharePermille, int EveSharePermille, IReadOnlyList<int> AfterSharesPermille);

/// <param name="Day">Day of the season (1-based).</param>
/// <param name="Goods">Festival goods: barely wanted outside the festival's window, orderable only before it.</param>
/// <param name="GoodsPeakPermille">Demand for the festival goods on the day.</param>
/// <param name="Demand">Peak demand for other goods on the day (the curve scales it on the days around).</param>
/// <param name="DayCustomersPermille">Arrivals on the festival day itself.</param>
/// <param name="LineKeys">Display only: what shoppers sometimes say around the festival.</param>
public sealed record FestivalDef(
    string Id,
    string NameKey,
    string Season,
    int Day,
    IReadOnlyList<string> Goods,
    int GoodsPeakPermille,
    IReadOnlyList<DemandFactorDef> Demand,
    int DayCustomersPermille,
    int? PrepDays = null,
    IReadOnlyList<string>? LineKeys = null);

/// <summary>A wall decoration bought once and hung for the festivals it belongs to.</summary>
public sealed record FestivalDecorationDef(string Id, string NameKey, string Sprite, long CostCents, IReadOnlyList<string> Festivals);
