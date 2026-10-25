using System.Text.Json.Serialization;
using ChineseGrocery.Sim.Tasks;

namespace ChineseGrocery.Sim.Data;

/// <summary>Root of employees.json: hireable roles plus the player character's fixed stats.</summary>
/// <param name="ApplicantsPerDay">How many people apply each morning.</param>
/// <param name="AskingWage">Asking wage as permille of the market wage, from the weakest to the strongest applicant.</param>
/// <param name="SeveranceDays">Days of wage paid on firing.</param>
/// <param name="ManagerErrorPerPointPermille">How far off a manager's orders may be per skill point below the best.</param>
public sealed record EmployeesDef(
    IReadOnlyList<RoleDef> Roles,
    PlayerDef Player,
    int ApplicantsPerDay = 0,
    IntRange? AskingWage = null,
    int SeveranceDays = 0,
    MoraleRulesDef? Morale = null,
    int ManagerErrorPerPointPermille = 0);

/// <summary>employees.json "morale": what wears staff morale down or lifts it, and when unhappy staff quit.</summary>
/// <param name="MaxStreakDays">Days in a row a worker can work before each further day costs morale.</param>
/// <param name="RestRecoveryPermille">Morale a rest day gives back (never above the wage-based level).</param>
/// <param name="WorkloadFactorPermille">A day with more tasks than this share of the staff average costs morale.</param>
/// <param name="QuitBelowPermille">Below this morale a worker is warned about at closing, and may quit the next closing.</param>
public sealed record MoraleRulesDef(
    int MaxStreakDays,
    int StreakPenaltyPermille,
    int RestRecoveryPermille,
    int WorkloadFactorPermille,
    int WorkloadPenaltyPermille,
    int LevelUpBonusPermille,
    int QuitBelowPermille,
    int QuitChancePermille);

/// <summary>A hireable role. Attributes are rolled within the ranges at hire.</summary>
/// <param name="MoraleAtMarketWagePermille">Morale when paid exactly the market wage.</param>
/// <param name="MoralePerWageRatioPermille">Morale change per 1000 permille of wage / market wage ratio (so +10% wage = +10% of this).</param>
/// <param name="MinMoraleSpeedPermille">Share of full work speed at zero morale; full morale gives full speed.</param>
/// <param name="KnifeSkill">The role's craft skill range: knife work for butchers and fishmongers, cooking for chefs.</param>
/// <param name="SkillNameKey">Display only: what the skill is called for this role.</param>
/// <param name="Manager">Does no tasks; runs a department's ordering, pricing and menu instead.</param>
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
    SpriteDef? Sprite = null,
    string? SkillNameKey = null,
    bool Manager = false,
    [property: JsonPropertyName("_tbd")] bool Tbd = false);

/// <param name="WorkSpeedPermille">1000 = baseline task speed.</param>
/// <param name="KnifeSkill">Used when the player cuts meat.</param>
public sealed record PlayerDef(
    int MoveCellsPerMinute,
    int WorkSpeedPermille,
    int KnifeSkill,
    SpriteDef? Sprite = null,
    [property: JsonPropertyName("_tbd")] bool Tbd = false);
