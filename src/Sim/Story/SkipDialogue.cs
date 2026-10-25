namespace ChineseGrocery.Sim.Story;

/// <summary>Skips the current dialogue and everything that would follow it (taking the first answer where there is a choice).</summary>
public sealed record SkipDialogue(string DialogueId) : StoryCommand;
