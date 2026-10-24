namespace ChineseGrocery.Sim.Tasks;

/// <summary>Take dead animals out of a fish tank before they foul the water further.</summary>
public sealed record RemoveDeadTask(int Id, int Priority, int TankInstanceId) : SimTask(Id, TaskType.RemoveDead, Priority);
