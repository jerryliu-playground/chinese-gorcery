using System.Text.Json.Serialization;
using ChineseGrocery.Sim.Tasks;

namespace ChineseGrocery.Sim.Data;

/// <summary>Root of employees.json: hireable roles plus the player character's fixed stats.</summary>
public sealed record EmployeesDef(
    IReadOnlyList<RoleDef> Roles,
    PlayerDef Player);

/// <summary>A hireable role. Attributes are rolled within the ranges at hire.</summary>
/// <param name="MoraleAtMarketWagePermille">Morale when paid exactly the market wage.</param>
/// <param name="MoralePerWageRatioPermille">Morale change per 1000 permille of wage / market wage ratio (so +10% wage = +10% of this).</param>
/// <param name="MinMoraleSpeedPermille">Share of full work speed at zero morale; full morale gives full speed.</param>
public sealed record RoleDef(
    string Id,
    string NameKey,
    int UnlockStage,
    int MaxCount,
    IReadOnlyList<TaskType> TaskTypes,
    long MarketWageCentsPerDay,
    IntRange KnifeSkill,
    IntRange SpeedPermille,
    int MoveCellsPerMinute,
    int MoraleAtMarketWagePermille,
    int MoralePerWageRatioPermille,
    int MinMoraleSpeedPermille,
    int XpPerTask,
    int XpPerLevel,
    int MaxLevel,
    int LevelUpKnifeSkill,
    int LevelUpSpeedPermille,
    [property: JsonPropertyName("_tbd")] bool Tbd = false);

/// <param name="WorkSpeedPermille">1000 = baseline task speed.</param>
/// <param name="KnifeSkill">Used when the player cuts meat.</param>
public sealed record PlayerDef(
    int MoveCellsPerMinute,
    int WorkSpeedPermille,
    int KnifeSkill,
    [property: JsonPropertyName("_tbd")] bool Tbd = false);
