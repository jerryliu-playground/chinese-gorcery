using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Calendar;

/// <summary>A clearance offer was taken: the goods are in storage.</summary>
public sealed record ClearanceBought(int EventInstanceId, string ProductId, int Units, long CostCents) : SimEvent;
