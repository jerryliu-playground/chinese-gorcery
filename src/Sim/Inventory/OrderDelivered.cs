using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Inventory;

public sealed record OrderDelivered(int Day, string ProductId, int Quantity, int FixtureInstanceId) : SimEvent;
