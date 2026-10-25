using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Calendar;

/// <summary>Takes a decoration down and keeps it for next time.</summary>
public sealed record StoreDecoration(string DecorationId) : SimCommand;
