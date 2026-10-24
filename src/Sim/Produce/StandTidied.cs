using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Produce;

public sealed record StandTidied(int StandInstanceId, int WorkerId) : SimEvent;
