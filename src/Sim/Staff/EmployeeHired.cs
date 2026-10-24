using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Staff;

public sealed record EmployeeHired(int WorkerId, string RoleId, int KnifeSkill, int SpeedPermille, long WageCentsPerDay) : SimEvent;
