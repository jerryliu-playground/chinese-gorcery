using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Deli;

/// <summary>Before opening: set (or with a null dish, clear) what a deli station slot sells and today's target portions.</summary>
public sealed record SetMenuItem(int StationInstanceId, int Slot, string? DishId, int TargetPortions) : SimCommand;
