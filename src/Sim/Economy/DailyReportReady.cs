using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Economy;

public sealed record DailyReportReady(DailyReport Report) : SimEvent;
