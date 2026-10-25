using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Reputation;

/// <summary>A buyer proposed a group order.</summary>
public sealed record GroupOrderOffered(int OrderId) : SimEvent;
