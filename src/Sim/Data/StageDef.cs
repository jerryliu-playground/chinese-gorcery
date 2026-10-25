using System.Text.Json.Serialization;
using ChineseGrocery.Sim.Grid;

namespace ChineseGrocery.Sim.Data;

/// <summary>How many of a role a stage allows.</summary>
public sealed record RoleLimit(string RoleId, int Max);

/// <summary>Row of stages.json. The revenue target and cost are what it takes to upgrade INTO this stage.</summary>
public sealed record StageDef(
    int Id,
    string NameKey,
    GridSize StoreSize,
    long RequiredCumulativeRevenueCents,
    long UpgradeCostCents,
    string? FloorSprite = null,
    string? WallSprite = null,
    IReadOnlyList<DecorationDef>? Decorations = null,
    IReadOnlyList<RoleLimit>? RoleLimits = null,
    [property: JsonPropertyName("_tbd")] bool Tbd = false);
