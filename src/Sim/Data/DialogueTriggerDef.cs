namespace ChineseGrocery.Sim.Data;

/// <summary>When a dialogue starts by itself.</summary>
/// <param name="On">The moment that is checked.</param>
/// <param name="Stage">For stage_reached: the stage just reached.</param>
/// <param name="MinDay">Not before this day.</param>
/// <param name="MinStage">Not before the store reaches this stage.</param>
/// <param name="After">Dialogues that must have been shown (finished or skipped) first.</param>
/// <param name="Role">Only while someone in this staff role works at the store.</param>
public sealed record DialogueTriggerDef(
    DialogueMoment On,
    int? Stage = null,
    int MinDay = 0,
    int? MinStage = null,
    IReadOnlyList<string>? After = null,
    string? Role = null);
