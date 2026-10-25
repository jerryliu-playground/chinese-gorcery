using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Calendar;

/// <summary>A festival decoration was bought (and stored).</summary>
public sealed record DecorationBought(string DecorationId, long CostCents) : SimEvent;
