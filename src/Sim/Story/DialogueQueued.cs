using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Story;

/// <summary>A dialogue joined the queue the view shows (see <see cref="StoryState.Pending"/>).</summary>
public sealed record DialogueQueued(string DialogueId) : SimEvent;
