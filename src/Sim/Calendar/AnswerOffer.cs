using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Calendar;

/// <summary>Accepts or declines a clearance offer or group order (before opening).</summary>
public sealed record AnswerOffer(int EventInstanceId, bool Accept) : SimCommand;
