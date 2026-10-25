using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Calendar;

/// <summary>A festival's preparation window starts today.</summary>
public sealed record FestivalReminder(string FestivalId, int DaysUntil) : SimEvent;
