namespace ChineseGrocery.Sim.Tasks;

/// <summary>Straighten a produce stand that shoppers have messed up.</summary>
public sealed record TidyTask(int Id, int Priority, int StandInstanceId) : SimTask(Id, TaskType.Tidy, Priority);
