using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Meat;

public sealed record MeatCut(int TaskId, int WorkerId, string PrimalId, IReadOnlyList<CutOutput> Outputs, long LossCents) : SimEvent;
