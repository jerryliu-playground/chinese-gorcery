using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Reputation;

/// <summary>The player took on a group order, due at the closing of DueDay.</summary>
public sealed record GroupOrderAccepted(int OrderId, int DueDay) : SimEvent;
