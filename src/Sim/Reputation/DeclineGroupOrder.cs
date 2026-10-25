using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Reputation;

/// <summary>Turn down an offered restaurant group order (reputation is not affected).</summary>
public sealed record DeclineGroupOrder(int OrderId) : SimCommand;
