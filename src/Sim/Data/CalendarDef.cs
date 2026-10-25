using System.Text.Json.Serialization;

namespace ChineseGrocery.Sim.Data;

/// <summary>Root of calendar.json: the seasons of a year in order (the game starts on day 1 of the first) and seasonal demand.</summary>
public sealed record CalendarDef(
    IReadOnlyList<SeasonDef> Seasons,
    IReadOnlyList<SeasonalDemandDef> SeasonalDemand,
    [property: JsonPropertyName("_tbd")] bool Tbd = false)
{
    public int DaysPerYear => Seasons.Sum(s => s.Days);
}

public sealed record SeasonDef(string Id, string NameKey, int Days);

/// <summary>A demand multiplier that applies all through one season.</summary>
public sealed record SeasonalDemandDef(string Season, int Permille, string? ProductId = null, Department? Department = null, ProduceCategory? Category = null)
{
    public DemandFactorDef Factor => new(Permille, ProductId, Department, Category);
}
