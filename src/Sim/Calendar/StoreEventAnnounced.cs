using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Calendar;

/// <summary>A random event became known (it may start later).</summary>
public sealed record StoreEventAnnounced(StoreEvent Event) : SimEvent;
