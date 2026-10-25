namespace ChineseGrocery.Sim.Story;

/// <summary>Names the shop; surrounding spaces are dropped and the length is limited by story.json.</summary>
public sealed record NameShop(string Name) : StoryCommand;
