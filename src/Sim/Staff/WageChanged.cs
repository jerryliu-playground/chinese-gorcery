using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Staff;

public sealed record WageChanged(int WorkerId, long WageCentsPerDay, int MoralePermille) : SimEvent;
