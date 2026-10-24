using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Customers;

public sealed record CustomerLeft(int CustomerId, CustomerExit Exit) : SimEvent;
