using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Staff;

/// <summary>Any time: let a staff member go, paying severance.</summary>
public sealed record FireEmployee(int WorkerId) : SimCommand;
