using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Tasks;

public sealed record TaskCompleted(int TaskId, TaskType TaskType, int WorkerId) : SimEvent;
