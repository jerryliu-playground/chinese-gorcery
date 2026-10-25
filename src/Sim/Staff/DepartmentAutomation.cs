namespace ChineseGrocery.Sim.Staff;

/// <summary>What a department's manager does each morning: order up to the targets, and/or reset prices to suggested.</summary>
public sealed record DepartmentAutomation(bool AutoOrder, bool AutoPrice);
