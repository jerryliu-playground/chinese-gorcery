using System.Text.Json.Serialization;
using ChineseGrocery.Sim.Tasks;

namespace ChineseGrocery.Sim.Data;

/// <summary>Root of employees.json: hireable roles plus the player character's fixed stats.</summary>
public sealed record EmployeesDef(
    IReadOnlyList<RoleDef> Roles,
    PlayerDef Player);

public sealed record RoleDef(
    string Id,
    string NameKey,
    int UnlockStage,
    int MaxCount,
    IReadOnlyList<TaskType> TaskTypes,
    long MarketWageCentsPerDay,
    IntRange KnifeSkill,
    IntRange SpeedPermille,
    int XpPerTask,
    int XpPerLevel,
    [property: JsonPropertyName("_tbd")] bool Tbd = false);

/// <param name="WorkSpeedPermille">1000 = baseline task speed.</param>
public sealed record PlayerDef(
    int MoveCellsPerTick,
    int WorkSpeedPermille,
    [property: JsonPropertyName("_tbd")] bool Tbd = false);
