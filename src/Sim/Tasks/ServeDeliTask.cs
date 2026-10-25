namespace ChineseGrocery.Sim.Tasks;

/// <summary>Pack a portion of a dish at a deli station for the customer waiting there.</summary>
public sealed record ServeDeliTask(int Id, int Priority, int StationInstanceId, int CustomerId, string DishId)
    : SimTask(Id, TaskType.ServeDeli, Priority);
