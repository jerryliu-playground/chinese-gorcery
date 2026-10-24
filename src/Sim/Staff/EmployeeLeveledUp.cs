using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Staff;

public sealed record EmployeeLeveledUp(int WorkerId, int Level, int KnifeSkill, int SpeedPermille) : SimEvent;
