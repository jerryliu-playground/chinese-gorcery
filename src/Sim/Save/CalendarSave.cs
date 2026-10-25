using ChineseGrocery.Sim.Calendar;
using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Save;

/// <summary>Calendar state beyond the day number: the event stream, events in progress and the decorations owned.</summary>
public sealed record CalendarSave(
    RngState Rng,
    int NextEventId,
    IReadOnlyList<StoreEvent> Events,
    IReadOnlyList<OwnedDecoration> Decorations);
