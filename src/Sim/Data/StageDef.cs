using System.Text.Json.Serialization;
using ChineseGrocery.Sim.Grid;

namespace ChineseGrocery.Sim.Data;

/// <summary>Row of stages.json. The revenue target and cost are what it takes to upgrade INTO this stage.</summary>
public sealed record StageDef(
    int Id,
    string NameKey,
    GridSize StoreSize,
    long RequiredCumulativeRevenueCents,
    long UpgradeCostCents,
    [property: JsonPropertyName("_tbd")] bool Tbd = false);
