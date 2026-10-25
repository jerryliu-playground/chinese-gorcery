using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Story;

/// <summary>
/// Story and tutorial progress: which dialogues have fired (they never repeat unless marked repeatable), the queue the
/// view is showing, answers given, tutorial steps, the shop's name and the story-prompts setting.
/// </summary>
public sealed class StoryState
{
    private readonly SortedSet<string> _fired = new(StringComparer.Ordinal);
    private readonly List<string> _pending = [];
    private readonly SortedDictionary<string, int> _lastFiredDay = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, string> _choices = new(StringComparer.Ordinal);
    private readonly SortedSet<int> _tutorialDone = [];

    internal StoryState(SimRng rng) => Rng = rng;

    /// <summary>Rolls for side stories; separate from the main generator so the story never shifts customer randomness.</summary>
    internal SimRng Rng { get; }

    public IReadOnlyCollection<string> Fired => _fired;

    /// <summary>Dialogues waiting to be shown, the current one first.</summary>
    public IReadOnlyList<string> Pending => _pending;

    /// <summary>The dialogue the view should be showing now, if any.</summary>
    public string? Current => _pending.Count > 0 ? _pending[0] : null;

    public IReadOnlyDictionary<string, int> LastFiredDay => _lastFiredDay;

    /// <summary>Answer picked in each dialogue that offered choices.</summary>
    public IReadOnlyDictionary<string, string> Choices => _choices;

    public IReadOnlyCollection<int> TutorialDone => _tutorialDone;

    /// <summary>The tutorial step whose call has come and whose goal is not reached yet.</summary>
    public int? TutorialActive { get; internal set; }

    /// <summary>Day the last optional dialogue fired (they share one cooldown).</summary>
    public int? LastOptionalDay { get; internal set; }

    /// <summary>Some order has been delivered (tutorial: goods arrived).</summary>
    public bool GoodsArrived { get; internal set; }

    /// <summary>The name the player gave the shop; null = the default name.</summary>
    public string? ShopName { get; internal set; }

    /// <summary>Settings toggle: side stories and other optional dialogues. The tutorial and main story always run.</summary>
    public bool OptionalEnabled { get; internal set; } = true;

    public bool HasFired(string id) => _fired.Contains(id);

    /// <summary>Fired and no longer waiting: the player has seen (or skipped) it.</summary>
    public bool IsShown(string id) => _fired.Contains(id) && !_pending.Contains(id);

    internal void MarkFired(string id, int day)
    {
        _fired.Add(id);
        _lastFiredDay[id] = day;
    }

    internal void Enqueue(string id, bool front)
    {
        if (front) _pending.Insert(0, id);
        else _pending.Add(id);
    }

    internal void RemoveCurrent() => _pending.RemoveAt(0);

    internal void SetChoice(string dialogueId, string choiceId) => _choices[dialogueId] = choiceId;

    internal void CompleteStep(int step)
    {
        _tutorialDone.Add(step);
        if (TutorialActive == step) TutorialActive = null;
    }

    internal void Restore(
        IEnumerable<string> fired, IEnumerable<string> pending, IEnumerable<KeyValuePair<string, int>> lastFired,
        IEnumerable<KeyValuePair<string, string>> choices, IEnumerable<int> tutorialDone)
    {
        _fired.UnionWith(fired);
        _pending.AddRange(pending);
        foreach (var (id, day) in lastFired) _lastFiredDay[id] = day;
        foreach (var (id, choice) in choices) _choices[id] = choice;
        _tutorialDone.UnionWith(tutorialDone);
    }
}
