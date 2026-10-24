using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Inventory;

public sealed record OrderPlaced(int Day, string ProductId, int Quantity, long CostCents) : SimEvent;
