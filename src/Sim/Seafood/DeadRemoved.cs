using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Inventory;

namespace ChineseGrocery.Sim.Seafood;

/// <summary>The dead were taken out of a tank: <paramref name="Kept"/> went on as chilled goods, the rest was thrown away.</summary>
public sealed record DeadRemoved(int TankInstanceId, int WorkerId, IReadOnlyList<StockLot> Kept, long LossCents) : SimEvent;
