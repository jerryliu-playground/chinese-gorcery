using System.Text.Json.Serialization;

namespace ChineseGrocery.Sim.Data;

/// <summary>One conversation, shown line by line.</summary>
/// <param name="Trigger">When it starts by itself; null = only as a follow-up (<see cref="Next"/>, a choice) or a tutorial call.</param>
/// <param name="Choices">Answers offered after the last line; the player must pick one.</param>
/// <param name="Next">Dialogue that follows right after this one (unless the chosen answer names its own).</param>
/// <param name="Prompt">Something the view asks the player after the last line (e.g. the shop's name).</param>
/// <param name="Optional">Side content the "story prompts" setting can turn off; optional dialogues share one cooldown.</param>
/// <param name="Repeatable">May fire again after <see cref="CooldownDays"/>; otherwise a dialogue fires at most once.</param>
/// <param name="CooldownDays">Optional dialogues: days since the last optional dialogue (and since this one, if repeatable).</param>
/// <param name="ChancePermille">Optional dialogues: chance to fire when everything else allows it.</param>
/// <param name="Draft">Lines are drafts awaiting Jerry's review.</param>
public sealed record DialogueDef(
    string Id,
    DialogueStyle Style,
    IReadOnlyList<DialogueLineDef> Lines,
    DialogueTriggerDef? Trigger = null,
    IReadOnlyList<DialogueChoiceDef>? Choices = null,
    string? Next = null,
    DialoguePrompt? Prompt = null,
    bool Optional = false,
    bool Repeatable = false,
    int CooldownDays = 0,
    int ChancePermille = 1000,
    [property: JsonPropertyName("_draft")] bool Draft = false);
