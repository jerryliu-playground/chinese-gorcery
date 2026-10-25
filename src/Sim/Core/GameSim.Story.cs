using System.Globalization;
using ChineseGrocery.Sim.Customers;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Save;
using ChineseGrocery.Sim.Story;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Core;

/// <summary>
/// Story, dialogue and tutorial (spec 18). After every tick the events it produced are read: they reach tutorial goals
/// and are the moments at which dialogue triggers are checked. Dialogues wait in <see cref="StoryState.Pending"/> until
/// the view reports them finished or skipped; the view pauses time while one is showing.
/// </summary>
public sealed partial class GameSim
{
    /// <summary>Mixed into the game seed for the story's own generator.</summary>
    private const ulong StorySeedSalt = 0x53_54_4F_52_59;

    public StoryState Story { get; private set; } = null!;

    private void StartStory(ulong seed)
    {
        Story = new StoryState(new SimRng(seed ^ StorySeedSalt));
        if (Data.Story is not { } story) return;

        Loan = new StudentLoan(story.Settings.Loan.PrincipalCents);
        FireMoment(DialogueMoment.NewGame);
    }

    private StorySave SaveStory() => new(
        Story.Rng.State,
        Story.Fired.ToList(),
        Story.Pending.ToList(),
        new SortedDictionary<string, int>(Story.LastFiredDay.ToDictionary(), StringComparer.Ordinal),
        new SortedDictionary<string, string>(Story.Choices.ToDictionary(), StringComparer.Ordinal),
        Story.LastOptionalDay,
        Story.TutorialDone.ToList(),
        Story.TutorialActive,
        Story.GoodsArrived,
        Story.ShopName,
        Story.OptionalEnabled,
        Loan?.BalanceCents,
        Loan?.ArrearsCents ?? 0);

    /// <summary>Restores story progress; a save from before the story existed continues as a game past the tutorial.</summary>
    private void RestoreStory(StorySave? save, RngState mainRng)
    {
        if (save is null)
        {
            ContinueWithoutStorySave(mainRng);
            return;
        }

        Story = new StoryState(SimRng.FromState(save.Rng))
        {
            TutorialActive = save.TutorialActive, LastOptionalDay = save.LastOptionalDay, GoodsArrived = save.GoodsArrived,
            ShopName = save.ShopName, OptionalEnabled = save.OptionalEnabled,
        };
        Story.Restore(save.Fired, save.Pending, save.LastFiredDay, save.Choices, save.TutorialDone);
        if (Data.Story is not { } story) return;

        foreach (var id in save.Pending) _ = story.Dialogue(id);
        if (save.TutorialActive is { } step && (step < 1 || step > story.Settings.Tutorial.Count))
        {
            throw new InvalidOperationException($"Tutorial step {step} does not exist.");
        }

        Loan = save.LoanBalanceCents is { } balance ? StudentLoan.Restore(balance, save.LoanArrearsCents) : new StudentLoan(story.Settings.Loan.PrincipalCents);
    }

    /// <summary>Old saves: the opening, the tutorial and the chapters of stages already reached count as seen.</summary>
    private void ContinueWithoutStorySave(RngState mainRng)
    {
        Story = new StoryState(new SimRng(mainRng.S0 ^ StorySeedSalt));
        if (Data.Story is not { } story) return;

        Loan = new StudentLoan(story.Settings.Loan.PrincipalCents);
        var seen = story.Dialogues.Where(d => d.Trigger is { On: DialogueMoment.NewGame } || d.Trigger is { On: DialogueMoment.StageReached } t && t.Stage <= Economy.Stage);
        var stack = new Stack<string>(seen.Select(d => d.Id));
        while (stack.Count > 0)
        {
            var id = stack.Pop();
            if (Story.HasFired(id)) continue;
            Story.MarkFired(id, Clock.Day);
            var d = story.Dialogue(id);
            foreach (var next in new[] { d.Next }.Concat((d.Choices ?? []).Select(c => c.Next)).OfType<string>()) stack.Push(next);
        }

        foreach (var step in story.Settings.Tutorial) Story.CompleteStep(step.Step);
    }

    private Rejection? HandleStoryCommand(StoryCommand command) => command switch
    {
        FinishDialogue f => HandleFinishDialogue(f),
        SkipDialogue s => HandleSkipDialogue(s),
        NameShop n => HandleNameShop(n),
        ReportViewed => ReachGoal(TutorialGoal.ViewReport),
        SetStoryPrompts p => SetOptionalStory(p.Enabled),
        PayLoan p => HandlePayLoan(p),
        _ => throw new ArgumentException($"Unknown story command {command.GetType().Name}", nameof(command)),
    };

    private Rejection? HandleFinishDialogue(FinishDialogue finish)
    {
        if (Data.Story is not { } story || Story.Current != finish.DialogueId) return Reject(RejectReason.DialogueNotShowing);

        var dialogue = story.Dialogue(finish.DialogueId);
        DialogueChoiceDef? choice = null;
        if (dialogue.Choices is { Count: > 0 } choices)
        {
            choice = choices.FirstOrDefault(c => c.Id == finish.ChoiceId);
            if (choice is null) return Reject(RejectReason.UnknownChoice);
            Story.SetChoice(dialogue.Id, choice.Id);
        }
        else if (finish.ChoiceId is not null)
        {
            return Reject(RejectReason.UnknownChoice);
        }

        Story.RemoveCurrent();
        _events.Add(new DialogueFinished(dialogue.Id, choice?.Id, Skipped: false));
        if ((choice?.Next ?? dialogue.Next) is { } next) QueueDialogue(next, front: true);
        CallDismissed(dialogue.Id);
        return null;
    }

    private Rejection? HandleSkipDialogue(SkipDialogue skip)
    {
        if (Data.Story is not { } story || Story.Current != skip.DialogueId) return Reject(RejectReason.DialogueNotShowing);

        Story.RemoveCurrent();
        _events.Add(new DialogueFinished(skip.DialogueId, null, Skipped: true));
        CallDismissed(skip.DialogueId);

        // What would have followed counts as seen, so a skipped opening still lets the tutorial begin.
        for (var next = FollowUp(story.Dialogue(skip.DialogueId)); next is not null; next = FollowUp(story.Dialogue(next)))
        {
            Story.MarkFired(next, Clock.Day);
        }

        return null;
    }

    /// <summary>The dialogue that follows by default: the next one, or where the first answer leads.</summary>
    private static string? FollowUp(DialogueDef d) => d.Next ?? d.Choices?.FirstOrDefault()?.Next;

    private Rejection? HandleNameShop(NameShop command)
    {
        if (Data.Story is not { } story) return Reject(RejectReason.NothingToDo);

        var name = command.Name.Trim();
        var length = new StringInfo(name).LengthInTextElements;
        if (length == 0 || length > story.Settings.ShopNameMaxLength || name.Any(char.IsControl)) return Reject(RejectReason.InvalidName);

        Story.ShopName = name;
        _events.Add(new ShopNamed(name));
        return null;
    }

    private Rejection? SetOptionalStory(bool enabled)
    {
        Story.OptionalEnabled = enabled;
        return null;
    }

    /// <summary>Reads what happened during this tick (events from index <paramref name="mark"/> on).</summary>
    private void UpdateStory(int mark)
    {
        if (Data.Story is null) return;

        var fresh = _events.Skip(mark).ToList();
        foreach (var e in fresh)
        {
            switch (e)
            {
                case OrderPlaced: ReachGoal(TutorialGoal.PlaceOrder); break;
                case ItemsShelved or ShelvesRestocked { Units: > 0 }: ReachGoal(TutorialGoal.ShelveGoods); break;
                case CheckoutCompleted: ReachGoal(TutorialGoal.Checkout); break;
                case PriceChanged: ReachGoal(TutorialGoal.ChangePrice); break;
                case OrderDelivered: Story.GoodsArrived = true; break;
                case StageUpgraded up:
                    ReachGoal(TutorialGoal.UpgradeOrDismiss);
                    FireMoment(DialogueMoment.StageReached, up.Stage);
                    break;
                case DayStarted: FireMoment(DialogueMoment.DayStart); break;
                case StoreOpened: FireMoment(DialogueMoment.StoreOpen); break;
                case StoreClosed: FireMoment(DialogueMoment.StoreClose); break;
                case LoanPaidOff: FireMoment(DialogueMoment.LoanPaidOff); break;
                case LoanPaymentMade { ArrearsCents: > 0, Scheduled: true }: FireMoment(DialogueMoment.LoanShort); break;
            }
        }

        AdvanceTutorial();
    }

    /// <summary>
    /// Queues every main-story dialogue whose trigger matches, then at most one optional one (each rolls its chance in
    /// data order) if the story-prompts setting allows.
    /// </summary>
    private void FireMoment(DialogueMoment moment, int? stage = null)
    {
        var optional = new List<DialogueDef>();
        foreach (var d in Data.Story!.Dialogues)
        {
            if (d.Trigger is not { } t || t.On != moment || !CanFire(d, t, stage)) continue;
            if (d.Optional) optional.Add(d);
            else QueueDialogue(d.Id, front: false);
        }

        if (!Story.OptionalEnabled) return;
        foreach (var d in optional)
        {
            if (Story.Rng.NextInt(0, 1000) >= d.ChancePermille) continue;
            QueueDialogue(d.Id, front: false);
            break;
        }
    }

    private bool CanFire(DialogueDef d, DialogueTriggerDef t, int? stage)
    {
        if (Story.HasFired(d.Id) && !d.Repeatable) return false;
        if (Story.Pending.Contains(d.Id)) return false;
        if (Clock.Day < t.MinDay) return false;
        if (t.Stage is { } wanted && wanted != stage) return false;
        if (t.MinStage is { } minStage && Economy.Stage < minStage) return false;
        if ((t.After ?? []).Any(a => !Story.IsShown(a))) return false;
        if (t.Role is { } role && _workers.All(w => w.RoleId != role)) return false;
        if (!d.Optional) return true;

        if (Story.LastOptionalDay is { } last && Clock.Day - last < d.CooldownDays) return false;
        return !Story.LastFiredDay.TryGetValue(d.Id, out var own) || Clock.Day - own >= d.CooldownDays;
    }

    private void QueueDialogue(string id, bool front)
    {
        Story.MarkFired(id, Clock.Day);
        if (Data.Story!.Dialogue(id).Optional) Story.LastOptionalDay = Clock.Day;
        Story.Enqueue(id, front);
        _events.Add(new DialogueQueued(id));
    }

    // ---------- tutorial ----------

    private TutorialStepDef StepDef(int step) => Data.Story!.Settings.Tutorial[step - 1];

    /// <summary>The running tutorial step, if any (for the view's reminder line).</summary>
    public TutorialStepDef? ActiveTutorialStep => Story.TutorialActive is { } step && Data.Story is not null ? StepDef(step) : null;

    private Rejection? ReachGoal(TutorialGoal goal)
    {
        if (Story.TutorialActive is { } step && StepDef(step).Goal == goal) CompleteStep(step);
        return null;
    }

    /// <summary>Closing the upgrade call counts as dealing with it: the upgrade can wait.</summary>
    private void CallDismissed(string dialogueId)
    {
        if (Story.TutorialActive is { } step && StepDef(step) is { Goal: TutorialGoal.UpgradeOrDismiss } def && def.DialogueId == dialogueId)
        {
            CompleteStep(step);
        }
    }

    private void CompleteStep(int step)
    {
        Story.CompleteStep(step);
        _events.Add(new TutorialStepCompleted(step));
    }

    /// <summary>Starts the first unfinished step whose earlier steps are done and whose situation holds; one at a time.</summary>
    private void AdvanceTutorial()
    {
        if (Data.Story is not { } story || Story.TutorialActive is not null || !Story.IsShown(story.Settings.TutorialStartsAfter)) return;

        foreach (var step in story.Settings.Tutorial)
        {
            if (Story.TutorialDone.Contains(step.Step) || (step.After ?? []).Any(a => !Story.TutorialDone.Contains(a))) continue;

            // Someone who already upgraded needs no call about upgrading.
            if (step.Goal == TutorialGoal.UpgradeOrDismiss && Economy.Stage > 1)
            {
                CompleteStep(step.Step);
                continue;
            }

            if (Clock.Day < step.MinDay || !Holds(step.Trigger)) continue;

            Story.TutorialActive = step.Step;
            _events.Add(new TutorialStepStarted(step.Step));
            QueueDialogue(step.DialogueId, front: false);
            return;
        }
    }

    private bool Holds(TutorialTrigger trigger) => trigger switch
    {
        TutorialTrigger.PreOpen => Clock.Phase == DayPhase.PreOpen,
        TutorialTrigger.GoodsArrived => Clock.Phase == DayPhase.PreOpen && Story.GoodsArrived,
        TutorialTrigger.StoreOpen => Clock.Phase == DayPhase.Open,
        TutorialTrigger.StoreClosed => Clock.Phase == DayPhase.Closed,
        TutorialTrigger.UpgradeAvailable => Clock.Phase == DayPhase.PreOpen && CanUpgrade(),
        _ => false,
    };

    /// <summary>The next stage's revenue target and cost are both met.</summary>
    public bool CanUpgrade()
    {
        if (Economy.Stage >= Data.Stages.Count) return false;
        var next = Data.Stages[Economy.Stage];
        return Economy.CumulativeRevenueCents >= next.RequiredCumulativeRevenueCents && Economy.CanAfford(next.UpgradeCostCents);
    }

    // ---------- competition (chapter 3) ----------

    /// <summary>
    /// The store's reputation as the competition sees it. A placeholder from story.json until the reputation system
    /// exists: this is the one place to rewire.
    /// </summary>
    public int CurrentReputation() => Data.Reputation is not null ? Reputation : Data.Story?.Settings.Competition.PlaceholderReputation ?? 0;

    /// <summary>Share (per mille) of regular customers still coming once the chain supermarket has opened.</summary>
    public int CompetitionArrivalPermille() =>
        Data.Story is { } story && Economy.Stage >= story.Settings.Competition.FromStage
            ? CompetitionFactor(story.Settings.Competition, CurrentReputation())
            : 1000;

    public static int CompetitionFactor(CompetitionDef competition, int reputation) =>
        Interpolation.Linear(competition.ArrivalPermilleByReputation.Select(p => (p.Reputation, p.ArrivalPermille)).ToList(), reputation);
}
