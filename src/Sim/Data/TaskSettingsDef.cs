using ChineseGrocery.Sim.Tasks;

namespace ChineseGrocery.Sim.Data;

/// <summary>Task timings and priorities (economy.json "tasks"). Minutes are at 1000 permille work speed.</summary>
/// <param name="RestockBelowPermille">A shelf slot gets a restock task when it is below this share of full.</param>
public sealed record TaskSettingsDef(
    int RestockBelowPermille,
    int RestockPickupMinutes,
    int RestockShelveMinutes,
    int CheckoutBaseMinutes,
    int CheckoutMinutesPerItem,
    IReadOnlyList<TaskPriority> Priorities)
{
    public int PriorityOf(TaskType type) => Priorities.Single(p => p.TaskType == type).Priority;
}

public sealed record TaskPriority(TaskType TaskType, int Priority);
