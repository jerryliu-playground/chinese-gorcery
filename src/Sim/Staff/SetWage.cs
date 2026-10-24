using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Staff;

/// <summary>PreOpen only: change an employee's daily wage, which sets their morale.</summary>
public sealed record SetWage(int WorkerId, long WageCentsPerDay) : SimCommand;
