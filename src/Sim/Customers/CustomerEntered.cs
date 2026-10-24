using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Customers;

public sealed record CustomerEntered(int CustomerId, string TypeId) : SimEvent;
