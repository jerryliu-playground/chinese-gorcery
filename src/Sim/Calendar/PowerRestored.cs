using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Calendar;

/// <summary>The power came back on.</summary>
public sealed record PowerRestored(int EventInstanceId) : SimEvent;
