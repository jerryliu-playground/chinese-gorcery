using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Calendar;

/// <summary>The inspector came in and is looking round.</summary>
public sealed record InspectorArrived(int EventInstanceId) : SimEvent;
