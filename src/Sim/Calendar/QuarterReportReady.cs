using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Calendar;

/// <summary>The last day of a season closed; its summary is ready.</summary>
public sealed record QuarterReportReady(Economy.QuarterReport Report) : SimEvent;
