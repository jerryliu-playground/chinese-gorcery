using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Staff;

/// <summary>Days of the week (0-6) a staff member has off.</summary>
public sealed record SetRestDays(int WorkerId, IReadOnlyList<int> Weekdays) : SimCommand;
