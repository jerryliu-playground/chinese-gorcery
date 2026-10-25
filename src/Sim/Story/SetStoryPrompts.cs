namespace ChineseGrocery.Sim.Story;

/// <summary>Settings toggle: allow side stories and other optional dialogues.</summary>
public sealed record SetStoryPrompts(bool Enabled) : StoryCommand;
