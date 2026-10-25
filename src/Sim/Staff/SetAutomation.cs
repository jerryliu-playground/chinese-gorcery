using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Staff;

/// <summary>What a department's manager takes care of.</summary>
public sealed record SetAutomation(Data.Department Department, bool AutoOrder, bool AutoPrice) : SimCommand;
