namespace ChineseGrocery.Sim.Data;

/// <summary>economy.json "deli": extra customers who come for hot food at mealtimes (only while a station has food).</summary>
public sealed record DeliSettingsDef(IReadOnlyList<RushBucket> RushArrivals);

/// <summary>From <paramref name="StartMinuteOfDay"/> until the next bucket, deli customers arrive at this rate, of these types.</summary>
public sealed record RushBucket(int StartMinuteOfDay, int CustomersPerHour, IReadOnlyList<TypeWeight> TypeWeights);

public sealed record TypeWeight(string TypeId, int Weight);
