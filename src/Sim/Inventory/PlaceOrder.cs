using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Inventory;

/// <summary>PreOpen only: buys from the local wholesaler, paid now, delivered before the next day's opening.</summary>
public sealed record PlaceOrder(string ProductId, int Quantity) : SimCommand;
