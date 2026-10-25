using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Calendar;

/// <summary>The inspector's verdict; <paramref name="CashCents"/> is the reward (positive) or the fine (negative).</summary>
public sealed record InspectionFinished(int EventInstanceId, HygieneFindings Findings, long CashCents) : SimEvent;
