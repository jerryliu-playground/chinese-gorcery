using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Tasks;

public sealed record TaskStarted(int TaskId, TaskType TaskType, int WorkerId) : SimEvent;
