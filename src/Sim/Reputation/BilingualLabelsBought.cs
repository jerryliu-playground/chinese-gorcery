using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Reputation;

/// <summary>The bilingual price labels went up.</summary>
public sealed record BilingualLabelsBought(long CostCents) : SimEvent;
