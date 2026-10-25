using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Reputation;

/// <summary>A customer left with this satisfaction score (0-100).</summary>
public sealed record CustomerSatisfied(int CustomerId, string CustomerTypeId, int Score, int LongestWaitSeconds) : SimEvent;
