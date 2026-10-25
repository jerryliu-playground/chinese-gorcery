namespace ChineseGrocery.Sim.Story;

/// <summary>The player read the current dialogue to the end; <paramref name="ChoiceId"/> answers its choices, if it has any.</summary>
public sealed record FinishDialogue(string DialogueId, string? ChoiceId = null) : StoryCommand;
