using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Story;

public sealed record DialogueFinished(string DialogueId, string? ChoiceId, bool Skipped) : SimEvent;
