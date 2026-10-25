using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Reputation;

/// <summary>Take on an offered restaurant group order.</summary>
public sealed record AcceptGroupOrder(int OrderId) : SimCommand;
