using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Calendar;

/// <summary>Buys a festival decoration; it is kept (stored) once bought.</summary>
public sealed record BuyDecoration(string DecorationId) : SimCommand;
