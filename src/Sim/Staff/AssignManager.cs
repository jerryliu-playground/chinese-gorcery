using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Staff;

/// <summary>Put a manager in charge of a department (null: none); a department has one manager.</summary>
public sealed record AssignManager(int WorkerId, Data.Department? Department) : SimCommand;
