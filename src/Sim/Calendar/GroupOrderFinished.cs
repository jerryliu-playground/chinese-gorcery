using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Calendar;

/// <summary>A group order was collected and paid for, or could not be filled.</summary>
public sealed record GroupOrderFinished(int EventInstanceId, bool Delivered, long RevenueCents) : SimEvent;
