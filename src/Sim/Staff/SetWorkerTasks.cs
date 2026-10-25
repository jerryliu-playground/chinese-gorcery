using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Staff;

/// <summary>Limit a staff member to some of their role's task types (null: all of them).</summary>
public sealed record SetWorkerTasks(int WorkerId, IReadOnlyList<Tasks.TaskType>? Only) : SimCommand;
