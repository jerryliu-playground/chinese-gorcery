using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Economy;

public sealed record StageUpgraded(int Stage, long CostCents) : SimEvent;
