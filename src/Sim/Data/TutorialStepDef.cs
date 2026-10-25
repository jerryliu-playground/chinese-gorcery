namespace ChineseGrocery.Sim.Data;

/// <summary>A step of the father's phone tutorial (spec 18.3).</summary>
/// <param name="DialogueId">The call that teaches it, queued when the step starts.</param>
/// <param name="Trigger">The situation the step starts in.</param>
/// <param name="Goal">What the player must do (after the step starts) to finish it.</param>
/// <param name="ObjectiveKey">Short reminder shown while the step is unfinished.</param>
/// <param name="After">Steps that must be finished first. Only one step runs at a time.</param>
/// <param name="MinDay">Not before this day.</param>
public sealed record TutorialStepDef(
    int Step,
    string DialogueId,
    TutorialTrigger Trigger,
    TutorialGoal Goal,
    string ObjectiveKey,
    IReadOnlyList<int>? After = null,
    int MinDay = 0);
