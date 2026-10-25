namespace ChineseGrocery.Sim.Reputation;

/// <summary>A friend of a happy customer, due to come in at this minute of the day (if the doors are still open).</summary>
public sealed record WordOfMouthArrival(int MinuteOfDay, string CustomerTypeId);
