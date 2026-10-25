using System.Text.RegularExpressions;

namespace ChineseGrocery.Sim.Data;

/// <summary>
/// Consistency checks on the story tables. <see cref="Validate"/> covers everything inside the data; the view also runs
/// <see cref="CheckAssets"/> at startup, since only it can see sprites and translations.
/// </summary>
public static partial class StoryValidator
{
    /// <summary>Speaker id for caption text with no one speaking.</summary>
    public const string Narrator = "narrator";

    [GeneratedRegex("^[a-z0-9]+(_[a-z0-9]+)*$")]
    private static partial Regex IdPattern();

    public static IReadOnlyList<string> Validate(StoryData story, GameData data)
    {
        var errors = new List<string>();
        var s = story.Settings;
        const string settingsFile = StoryLoader.SettingsFile;
        var stageIds = data.Stages.Select(x => x.Id).ToHashSet();
        var characters = s.Characters.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var dialogues = story.Dialogues.GroupBy(d => d.Id).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        CheckIds(settingsFile + " characters", s.Characters.Select(c => c.Id).ToList(), errors);
        foreach (var c in s.Characters)
        {
            if (c.Id == Narrator) errors.Add($"{settingsFile}: '{Narrator}' is reserved for captions");
            if (c.NameKey != $"character.{c.Id}.name") errors.Add($"{settingsFile}: character '{c.Id}' nameKey must be 'character.{c.Id}.name'");
            if (c.Portrait is null && c.Sprite is null) errors.Add($"{settingsFile}: character '{c.Id}' needs a portrait or a sprite");
        }

        CheckIds("dialogue", story.Dialogues.Select(d => d.Id).ToList(), errors);
        var roles = data.Employees.Roles.Select(r => r.Id).ToList();
        foreach (var d in story.Dialogues) ValidateDialogue(d, dialogues, characters, stageIds, roles, errors);
        ValidateChains(story, dialogues, errors);

        if (!dialogues.ContainsKey(s.TutorialStartsAfter)) errors.Add($"{settingsFile}: tutorialStartsAfter '{s.TutorialStartsAfter}' is not a dialogue");
        for (var i = 0; i < s.Tutorial.Count; i++)
        {
            var step = s.Tutorial[i];
            var at = $"{settingsFile}: tutorial step {step.Step}";
            if (step.Step != i + 1) errors.Add($"{settingsFile}: tutorial steps must be numbered 1, 2, 3... in order");
            if (!dialogues.TryGetValue(step.DialogueId, out var call)) errors.Add($"{at}: dialogue '{step.DialogueId}' does not exist");
            else if (call.Trigger is not null || call.Optional) errors.Add($"{at}: dialogue '{step.DialogueId}' must have no trigger and not be optional (the tutorial starts it)");
            if (step.After?.Any(a => a < 1 || a >= step.Step) == true) errors.Add($"{at}: 'after' may only name earlier steps");
            if (string.IsNullOrEmpty(step.ObjectiveKey)) errors.Add($"{at}: objectiveKey is required");
        }

        if (s.Tutorial.Select(t => t.DialogueId).Distinct().Count() != s.Tutorial.Count) errors.Add($"{settingsFile}: each tutorial step needs its own dialogue");

        var loan = s.Loan;
        if (loan.PrincipalCents <= 0) errors.Add($"{settingsFile}: loan.principalCents must be > 0");
        if (loan.MinimumPaymentCents <= 0) errors.Add($"{settingsFile}: loan.minimumPaymentCents must be > 0");
        if (loan.PaymentEveryDays <= 0) errors.Add($"{settingsFile}: loan.paymentEveryDays must be > 0");

        var comp = s.Competition;
        if (!stageIds.Contains(comp.FromStage)) errors.Add($"{settingsFile}: competition.fromStage {comp.FromStage} is not a stage");
        if (comp.ArrivalPermilleByReputation.Count == 0) errors.Add($"{settingsFile}: competition curve needs at least one point");
        for (var i = 0; i < comp.ArrivalPermilleByReputation.Count; i++)
        {
            var p = comp.ArrivalPermilleByReputation[i];
            if (p.ArrivalPermille is < 0 or > 1000) errors.Add($"{settingsFile}: competition arrivalPermille must be between 0 and 1000");
            if (i > 0 && p.Reputation <= comp.ArrivalPermilleByReputation[i - 1].Reputation) errors.Add($"{settingsFile}: competition points must rise in reputation");
        }

        if (s.ShopNameMaxLength is < 1 or > 64) errors.Add($"{settingsFile}: shopNameMaxLength must be between 1 and 64");
        if (string.IsNullOrEmpty(s.DefaultShopNameKey)) errors.Add($"{settingsFile}: defaultShopNameKey is required");
        return errors;
    }

    private static void ValidateDialogue(
        DialogueDef d, Dictionary<string, DialogueDef> dialogues, HashSet<string> characters, HashSet<int> stageIds, IReadOnlyList<string> roles, List<string> errors)
    {
        var at = $"dialogue '{d.Id}'";
        var prefix = $"dialogue.{d.Id}.";
        if (d.Lines.Count == 0) errors.Add($"{at}: needs at least one line");
        foreach (var line in d.Lines)
        {
            if (line.Speaker != Narrator && !characters.Contains(line.Speaker)) errors.Add($"{at}: unknown speaker '{line.Speaker}'");
            if (d.Style == DialogueStyle.Caption && line.Speaker != Narrator) errors.Add($"{at}: caption lines are spoken by '{Narrator}'");
            if (!line.Key.StartsWith(prefix, StringComparison.Ordinal)) errors.Add($"{at}: line key '{line.Key}' must start with '{prefix}'");
        }

        if (d.Lines.Select(l => l.Key).Distinct().Count() != d.Lines.Count) errors.Add($"{at}: line keys must be unique");

        var choices = d.Choices ?? [];
        if (d.Choices is { Count: 0 }) errors.Add($"{at}: choices, when given, need at least one entry");
        if (choices.Select(c => c.Id).Distinct().Count() != choices.Count) errors.Add($"{at}: choice ids must be unique");
        foreach (var c in choices)
        {
            if (!c.Key.StartsWith(prefix, StringComparison.Ordinal)) errors.Add($"{at}: choice key '{c.Key}' must start with '{prefix}'");
            if (c.Next is { } n && !dialogues.ContainsKey(n)) errors.Add($"{at}: choice '{c.Id}' leads to unknown dialogue '{n}'");
        }

        if (d.Next is { } next && !dialogues.ContainsKey(next)) errors.Add($"{at}: next '{next}' does not exist");
        if (d.ChancePermille is < 0 or > 1000) errors.Add($"{at}: chancePermille must be between 0 and 1000");
        if (d.CooldownDays < 0) errors.Add($"{at}: cooldownDays must be >= 0");
        if (d.Repeatable && (!d.Optional || d.CooldownDays <= 0)) errors.Add($"{at}: repeatable dialogues must be optional with cooldownDays > 0");

        if (d.Trigger is not { } t) return;
        if ((t.On == DialogueMoment.StageReached) != (t.Stage is not null)) errors.Add($"{at}: 'stage' goes with (and only with) stage_reached");
        if (t.Stage is { } stage && !stageIds.Contains(stage)) errors.Add($"{at}: stage {stage} does not exist");
        if (t.MinStage is { } min && !stageIds.Contains(min)) errors.Add($"{at}: minStage {min} does not exist");
        if (t.Role is { } role && roles.All(r => r != role)) errors.Add($"{at}: role '{role}' does not exist");
        if (t.On == DialogueMoment.NewGame && d.Optional) errors.Add($"{at}: the opening cannot be optional");
        foreach (var a in t.After ?? [])
        {
            if (!dialogues.ContainsKey(a)) errors.Add($"{at}: after '{a}' does not exist");
        }
    }

    /// <summary>Follow-ups (next and choices) must never lead back to where they started.</summary>
    private static void ValidateChains(StoryData story, Dictionary<string, DialogueDef> dialogues, List<string> errors)
    {
        IEnumerable<string> FollowUps(DialogueDef d) => new[] { d.Next }.Concat((d.Choices ?? []).Select(c => c.Next)).OfType<string>();

        foreach (var start in story.Dialogues)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var stack = new Stack<string>(FollowUps(start));
            while (stack.Count > 0)
            {
                var id = stack.Pop();
                if (id == start.Id)
                {
                    errors.Add($"dialogue '{start.Id}': follow-ups lead back to itself");
                    break;
                }

                if (!seen.Add(id) || !dialogues.TryGetValue(id, out var d)) continue;
                foreach (var f in FollowUps(d)) stack.Push(f);
            }
        }
    }

    /// <summary>Sprite name of a speaker's face: the portrait for the expression, or the walking sprite, or null (narrator).</summary>
    public static string? PortraitSprite(StoryData story, string speaker, PortraitExpression expression)
    {
        var c = story.Settings.Characters.FirstOrDefault(x => x.Id == speaker);
        if (c is null) return null;
        return c.Portrait is { } prefix ? $"{prefix}_{expression.ToString().ToLowerInvariant()}" : c.Sprite;
    }

    /// <summary>Every translation key the story uses.</summary>
    public static IEnumerable<string> TranslationKeys(StoryData story) =>
        story.Dialogues.SelectMany(d => d.Lines.Select(l => l.Key).Concat((d.Choices ?? []).Select(c => c.Key)))
            .Concat(story.Settings.Characters.Select(c => c.NameKey))
            .Concat(story.Settings.Tutorial.Select(t => t.ObjectiveKey))
            .Append(story.Settings.DefaultShopNameKey);

    /// <summary>Every sprite the dialogues show as a face.</summary>
    public static IEnumerable<string> PortraitSprites(StoryData story) =>
        story.Dialogues.SelectMany(d => d.Lines).Select(l => PortraitSprite(story, l.Speaker, l.Expression)).OfType<string>().Distinct();

    /// <summary>Missing sprites and translation keys, as developer messages.</summary>
    public static IReadOnlyList<string> CheckAssets(StoryData story, Func<string, bool> spriteExists, Func<string, bool> keyExists) =>
        PortraitSprites(story).Where(s => !spriteExists(s)).Select(s => $"story: sprite '{s}' not found")
            .Concat(TranslationKeys(story).Distinct().Where(k => !keyExists(k)).Select(k => $"story: translation key '{k}' not found"))
            .ToList();

    private static void CheckIds(string where, IReadOnlyList<string> ids, List<string> errors)
    {
        foreach (var id in ids.Where(id => !IdPattern().IsMatch(id))) errors.Add($"{where}: id '{id}' must be lower_snake_case");
        foreach (var dup in ids.GroupBy(id => id).Where(g => g.Count() > 1)) errors.Add($"{where}: duplicate id '{dup.Key}'");
    }
}
