using ChineseGrocery.Sim.Tasks;

namespace ChineseGrocery.Sim.Data;

/// <summary>Task timings and priorities (economy.json "tasks"). Minutes are at 1000 permille work speed.</summary>
/// <param name="RestockBelowPermille">A shelf slot gets a restock task when it is below this share of full.</param>
/// <param name="KillSkillSpeedPermille">Each knife skill point above 1 speeds up killing a fish by this share.</param>
public sealed record TaskSettingsDef(
    int RestockBelowPermille,
    int RestockPickupMinutes,
    int RestockShelveMinutes,
    int CheckoutBaseMinutes,
    int CheckoutMinutesPerItem,
    int CutPickupMinutes,
    int CutMeatMinutes,
    IReadOnlyList<TaskPriority> Priorities,
    int TidyMinutes = 0,
    int KillPickupMinutes = 0,
    int KillFishMinutes = 0,
    int KillSkillSpeedPermille = 0,
    int CleanTankMinutes = 0,
    int RemoveDeadMinutes = 0)
{
    public int PriorityOf(TaskType type) => Priorities.Single(p => p.TaskType == type).Priority;
}

public sealed record TaskPriority(TaskType TaskType, int Priority);
