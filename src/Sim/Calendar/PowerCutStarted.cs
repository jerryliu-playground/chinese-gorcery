using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Calendar;

/// <summary>The power went out; with a generator nothing is affected. <paramref name="LostCents"/> is chilled stock it spoiled.</summary>
public sealed record PowerCutStarted(int EventInstanceId, bool Generator, long LostCents) : SimEvent;
