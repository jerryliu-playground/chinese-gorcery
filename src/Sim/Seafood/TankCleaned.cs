using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Seafood;

public sealed record TankCleaned(int TankInstanceId, int WorkerId) : SimEvent;
