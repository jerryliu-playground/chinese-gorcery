using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Seafood;

public sealed record FishKilled(int CounterInstanceId, int CustomerId, string ProductId, int WorkerId) : SimEvent;
