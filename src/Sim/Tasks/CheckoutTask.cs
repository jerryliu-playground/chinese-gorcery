namespace ChineseGrocery.Sim.Tasks;

/// <summary>Serve the customer at the head of a checkout's queue. One task is one customer.</summary>
public sealed record CheckoutTask(int Id, int Priority, int CheckoutInstanceId)
    : SimTask(Id, TaskType.Checkout, Priority);
