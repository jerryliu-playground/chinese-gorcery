using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Inventory;

/// <summary>Before opening or after closing: buys from the local wholesaler, paid now, delivered before the next opening.</summary>
public sealed record PlaceOrder(string ProductId, int Quantity) : SimCommand;
