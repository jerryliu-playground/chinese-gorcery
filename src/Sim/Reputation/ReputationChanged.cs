using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Reputation;

/// <summary>Reputation moved; ReasonKey is a translation key saying why.</summary>
public sealed record ReputationChanged(int Before, int After, string ReasonKey) : SimEvent;
