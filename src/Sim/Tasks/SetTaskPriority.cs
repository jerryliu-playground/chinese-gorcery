using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Tasks;

/// <summary>Override how urgent a task type is for everyone (null: back to the data default).</summary>
public sealed record SetTaskPriority(TaskType TaskType, int? Priority) : SimCommand;
