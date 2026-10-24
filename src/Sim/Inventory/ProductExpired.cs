using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Inventory;

/// <param name="Location">Where the goods were; null when a worker was carrying them.</param>
public sealed record ProductExpired(int Day, string ProductId, int Quantity, StockLocation? Location, long LossCents) : SimEvent;
