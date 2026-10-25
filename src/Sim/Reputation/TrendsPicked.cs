using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Reputation;

/// <summary>A new season started and these products trend for it.</summary>
public sealed record TrendsPicked(int Season, IReadOnlyList<string> ProductIds) : SimEvent;
