using ChineseGrocery.Sim.Data;

namespace ChineseGrocery.Sim.Tests.Story;

/// <summary>Spec 18.1 / 18.7.2: dialogue data loads, validates, and only points at portraits and keys that exist.</summary>
public class StoryDataTests
{
    private static readonly string SpriteDir = Path.Combine(RepoPaths.Root, "game", "assets", "sprites");

    private static readonly HashSet<string> CsvKeys = File.ReadAllLines(RepoPaths.StringsCsv).Skip(1).Select(l => l.Split(',')[0]).ToHashSet();

    private static StoryData Story => SimTestKit.RealData.Story!;

    [Fact]
    public void Real_story_loads_with_the_data_and_validates()
    {
        Assert.NotNull(SimTestKit.RealData.Story);
        Assert.Empty(StoryValidator.Validate(Story, SimTestKit.RealData));
        Assert.Equal([1, 2, 3, 4, 5, 6], Story.Settings.Tutorial.Select(t => t.Step));
    }

    [Fact]
    public void Every_portrait_and_translation_key_exists()
    {
        var problems = StoryValidator.CheckAssets(Story, s => File.Exists(Path.Combine(SpriteDir, s + ".png")), CsvKeys.Contains);

        Assert.Contains("portrait_dad_worried", StoryValidator.PortraitSprites(Story));
        Assert.Empty(problems);
    }

    [Fact]
    public void Missing_portraits_and_keys_are_reported()
    {
        var problems = StoryValidator.CheckAssets(Story, s => s != "portrait_mom_happy", k => k != "dialogue.opening_captions.line_01");

        Assert.Contains(problems, p => p.Contains("portrait_mom_happy"));
        Assert.Contains(problems, p => p.Contains("dialogue.opening_captions.line_01"));
    }

    [Fact]
    public void Side_stories_are_six_to_eight_short_optional_scenes()
    {
        var side = Story.Dialogues.Where(d => d.Id.StartsWith("side_", StringComparison.Ordinal)).ToList();

        Assert.InRange(side.Count, 6, 8);
        Assert.All(side, d =>
        {
            Assert.True(d.Optional);
            Assert.InRange(d.Lines.Count, 3, 6);
            Assert.NotNull(d.Trigger);
        });
    }

    [Fact]
    public void Every_line_is_marked_as_a_draft_and_characters_use_placeholders()
    {
        Assert.All(Story.Dialogues, d => Assert.True(d.Draft, d.Id));
        Assert.Contains(Story.Dialogues.SelectMany(d => d.Lines), l => l.Speaker == "protagonist");
        Assert.Contains(Story.Dialogues.SelectMany(d => d.Lines), l => l.Speaker == "narrator");
    }

    [Fact]
    public void Each_stage_upgrade_has_a_chapter()
    {
        foreach (var stage in new[] { 2, 3, 4 })
        {
            Assert.Contains(Story.Dialogues, d => d.Id.StartsWith("chapter", StringComparison.Ordinal) && d.Trigger is { On: DialogueMoment.StageReached } t && t.Stage == stage);
        }
    }

    private static IReadOnlyList<string> ValidateWith(Func<DialogueDef, DialogueDef> change, string id = "opening_mom_call")
    {
        var story = Story with { Dialogues = Story.Dialogues.Select(d => d.Id == id ? change(d) : d).ToList() };
        return StoryValidator.Validate(story, SimTestKit.RealData);
    }

    [Fact]
    public void Validator_reports_unknown_speakers_follow_ups_and_bad_keys()
    {
        Assert.Contains(ValidateWith(d => d with { Lines = [d.Lines[0] with { Speaker = "uncle" }] }), e => e.Contains("unknown speaker 'uncle'"));
        Assert.Contains(ValidateWith(d => d with { Next = "nowhere" }), e => e.Contains("next 'nowhere'"));
        Assert.Contains(ValidateWith(d => d with { Lines = [d.Lines[0] with { Key = "ui.oops" }] }), e => e.Contains("must start with"));
        Assert.Contains(ValidateWith(d => d with { Trigger = new DialogueTriggerDef(DialogueMoment.StageReached) }), e => e.Contains("stage_reached"));
        Assert.Contains(ValidateWith(d => d with { Trigger = new DialogueTriggerDef(DialogueMoment.DayStart, Role: "astronaut") }), e => e.Contains("astronaut"));
        Assert.Contains(ValidateWith(d => d with { Repeatable = true }), e => e.Contains("repeatable"));
    }

    [Fact]
    public void Validator_reports_follow_up_loops()
    {
        // opening_walk_in normally ends the opening; pointing it back at the start makes a loop.
        Assert.Contains(ValidateWith(d => d with { Next = "opening_captions" }, "opening_walk_in"), e => e.Contains("lead back"));
    }

    [Fact]
    public void Validator_reports_bad_tutorial_and_settings()
    {
        var s = Story.Settings;
        var bad = s with
        {
            Tutorial = [s.Tutorial[1], s.Tutorial[0] with { DialogueId = "opening_captions" }],
            Loan = s.Loan with { MinimumPaymentCents = 0 },
            Competition = s.Competition with { FromStage = 9 },
            ShopNameMaxLength = 0,
        };

        var errors = StoryValidator.Validate(Story with { Settings = bad }, SimTestKit.RealData);

        Assert.Contains(errors, e => e.Contains("numbered"));
        Assert.Contains(errors, e => e.Contains("must have no trigger"));
        Assert.Contains(errors, e => e.Contains("minimumPaymentCents"));
        Assert.Contains(errors, e => e.Contains("fromStage"));
        Assert.Contains(errors, e => e.Contains("shopNameMaxLength"));
    }

    [Fact]
    public void Side_folder_dialogues_must_be_optional()
    {
        string Read(string file) => file switch
        {
            StoryLoader.SettingsFile => File.ReadAllText(Path.Combine(RepoPaths.Data, file)),
            "dialogue/side/x.json" => """[{ "id": "side_x", "style": "talk", "lines": [{ "speaker": "mom", "key": "dialogue.side_x.line_01" }] }]""",
            _ => File.ReadAllText(Path.Combine(RepoPaths.Data, file)),
        };

        var files = StoryLoader.DialogueFiles(RepoPaths.Data).Append("dialogue/side/x.json").ToList();
        var ex = Assert.Throws<DataValidationException>(() => StoryLoader.Load(Read, files, SimTestKit.RealData));

        Assert.Contains(ex.Errors, e => e.Contains("side story 'side_x' must be optional"));
    }

    [Fact]
    public void Unknown_fields_in_dialogue_files_are_rejected()
    {
        var ex = Assert.Throws<DataValidationException>(() =>
            GameDataLoader.Deserialize<List<DialogueDef>>("dialogue/x.json", """[{ "id": "x", "style": "talk", "lines": [], "mood": 3 }]"""));

        Assert.Contains(ex.Errors, e => e.Contains("mood"));
    }
}
