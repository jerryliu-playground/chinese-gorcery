using System.Text.Json.Serialization;

namespace ChineseGrocery.Sim.Data;

/// <summary>
/// The chain supermarket of chapter 3: from <paramref name="FromStage"/> on, regular arrivals are scaled by a factor read
/// off <paramref name="ArrivalPermilleByReputation"/> (straight lines between points, rising reputation).
/// </summary>
/// <param name="PlaceholderReputation">Stands in for the store's reputation until the reputation system exists.</param>
public sealed record CompetitionDef(
    int FromStage,
    int PlaceholderReputation,
    IReadOnlyList<CompetitionPoint> ArrivalPermilleByReputation,
    [property: JsonPropertyName("_tbd")] bool Tbd = false);
