using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Staff;

/// <summary>Stock a manager keeps a product topped up to (0: none).</summary>
public sealed record SetOrderTarget(string ProductId, int Units) : SimCommand;
