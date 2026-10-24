using ChineseGrocery.Sim.Inventory;

namespace ChineseGrocery.Sim.Tasks;

/// <summary>
/// Take a live animal from a tank slot (<paramref name="Source"/>) to the seafood counter and kill and prepare it for the
/// customer waiting there.
/// </summary>
public sealed record KillFishTask(int Id, int Priority, int CounterInstanceId, int CustomerId, string ProductId, StockLocation Source)
    : SimTask(Id, TaskType.KillFish, Priority);
