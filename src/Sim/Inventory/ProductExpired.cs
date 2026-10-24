using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Inventory;

public sealed record ProductExpired(int Day, string ProductId, int Quantity, StockLocation Location, long LossCents) : SimEvent;
