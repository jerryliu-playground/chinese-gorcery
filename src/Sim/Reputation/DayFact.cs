namespace ChineseGrocery.Sim.Reputation;

/// <summary>One thing that happened today (about a product, or the store when null), how often, and who it last happened to.</summary>
public sealed record DayFact(ReviewFact Fact, string? ProductId, int Count, string? CustomerTypeId);
