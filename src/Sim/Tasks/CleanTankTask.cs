namespace ChineseGrocery.Sim.Tasks;

/// <summary>Change the water of a fish tank that has got too dirty.</summary>
public sealed record CleanTankTask(int Id, int Priority, int TankInstanceId) : SimTask(Id, TaskType.CleanTank, Priority);
