using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Staff;

/// <summary>Which part of the day a staff member works.</summary>
public sealed record SetShift(int WorkerId, Shift Shift) : SimCommand;
