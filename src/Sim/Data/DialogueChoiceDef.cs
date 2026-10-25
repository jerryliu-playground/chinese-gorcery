namespace ChineseGrocery.Sim.Data;

/// <summary>An answer the player can pick; <paramref name="Next"/> is the dialogue it leads to, if any.</summary>
public sealed record DialogueChoiceDef(string Id, string Key, string? Next = null);
