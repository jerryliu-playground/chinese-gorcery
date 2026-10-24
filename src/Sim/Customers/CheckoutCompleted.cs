using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Customers;

public sealed record CheckoutCompleted(int CustomerId, long RevenueCents, int WorkerId) : SimEvent;
