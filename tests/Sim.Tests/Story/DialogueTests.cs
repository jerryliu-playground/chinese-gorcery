using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Staff;
using ChineseGrocery.Sim.Story;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Tests.Story;

/// <summary>Spec 18.1 / 18.2 / 18.5: triggers, the queue, choices, skipping, never repeating, side stories, the shop's name.</summary>
public class DialogueTests
{
    [Fact]
    public void A_new_game_starts_with_the_opening_and_no_tutorial_yet()
    {
        var sim = StoryKit.NewSim();

        Assert.Equal(["opening_captions"], sim.Story.Pending);
        sim.Tick();
        Assert.Null(sim.Story.TutorialActive);
        Assert.Equal("opening_captions", sim.Story.Current);
    }

    [Fact]
    public void Reading_the_opening_walks_its_chain_and_records_the_choice()
    {
        var sim = StoryKit.NewSim();
        var shown = new List<string>();
        foreach (var id in new[] { "opening_captions", "opening_mom_call" })
        {
            sim.Run(new FinishDialogue(id));
            shown.Add(id);
        }

        Assert.Equal("opening_retire", sim.Story.Current);
        sim.Run(new FinishDialogue("opening_retire"));
        Assert.Equal(RejectReason.UnknownChoice, sim.Drain<CommandRejected>().Single().Reason);
        sim.Run(new FinishDialogue("opening_retire", "nope"));
        Assert.Equal(RejectReason.UnknownChoice, sim.Drain<CommandRejected>().Single().Reason);

        sim.Run(new FinishDialogue("opening_retire", "for_now"));

        Assert.Equal("for_now", sim.Story.Choices["opening_retire"]);
        Assert.Equal("opening_retire_for_now", sim.Story.Current);
        Assert.Equal(["opening_retire_for_now", "opening_name_shop", "opening_walk_in", "tutorial_1_order"], sim.ReadAll());
        Assert.False(sim.Story.HasFired("opening_retire_accept"));
    }

    [Fact]
    public void Only_the_dialogue_on_screen_can_be_finished_or_skipped()
    {
        var sim = StoryKit.NewSim();

        sim.Run(new FinishDialogue("opening_walk_in"));
        sim.Run(new SkipDialogue("chapter2_change"));

        Assert.All(sim.Drain<CommandRejected>(), r => Assert.Equal(RejectReason.DialogueNotShowing, r.Reason));
        Assert.Equal("opening_captions", sim.Story.Current);
    }

    [Fact]
    public void Skipping_the_opening_skips_all_of_it_and_the_tutorial_begins()
    {
        var sim = StoryKit.NewSim();

        sim.SkipOpening();

        Assert.True(sim.Story.IsShown("opening_walk_in"));
        Assert.Null(sim.Story.ShopName);
        Assert.Equal(1, sim.Story.TutorialActive);
        Assert.Equal(["tutorial_1_order"], sim.Story.Pending);
        Assert.Contains(sim.DrainEvents(), e => e is DialogueFinished { DialogueId: "opening_captions", Skipped: true });
    }

    [Fact]
    public void A_triggered_dialogue_waits_for_its_conditions_and_never_repeats()
    {
        var sim = StoryKit.NewSim(StoryKit.WithSettings(s => s with { Tutorial = [] }));
        sim.SkipOpening();
        sim.Run(new SetStoryPrompts(false));

        // chapter1_regular: when the store opens, from day 2 on.
        sim.Run(new OpenStore());
        Assert.False(sim.Story.HasFired("chapter1_regular"));
        sim.NextMorning();
        sim.Run(new OpenStore());
        Assert.Equal(["chapter1_regular"], sim.ReadAll());

        sim.NextMorning();
        sim.Run(new OpenStore());
        Assert.Empty(sim.ReadAll());
    }

    [Fact]
    public void Side_stories_stay_off_while_story_prompts_are_off()
    {
        var sim = StoryKit.NewSim(StoryKit.WithSettings(s => s with { Tutorial = [] }));
        sim.SkipOpening();
        sim.Run(new SetStoryPrompts(false));

        for (var day = 0; day < 30; day++)
        {
            sim.NextMorning();
            sim.ReadAll();
        }

        Assert.DoesNotContain(sim.Story.Fired, id => sim.Data.Story!.Dialogue(id).Optional);
        Assert.True(sim.Story.HasFired("chapter1_regular"));
    }

    [Fact]
    public void Side_stories_fire_one_at_a_time_spaced_by_their_cooldown_and_only_once()
    {
        var sim = StoryKit.NewSim(StoryKit.WithSettings(s => s with { Tutorial = [] }));
        sim.SkipOpening();
        var queued = new List<string>();

        for (var day = 0; day < 40; day++)
        {
            sim.NextMorning();
            queued.AddRange(sim.DrainEvents().OfType<DialogueQueued>().Select(e => e.DialogueId));
            sim.ReadAll();
        }

        var fired = queued.Where(id => id.StartsWith("side_", StringComparison.Ordinal)).Select(id => (Id: id, Day: sim.Story.LastFiredDay[id])).ToList();

        Assert.True(fired.Count >= 3, $"only {fired.Count} side stories in 40 days");
        Assert.Equal(fired.Count, fired.Select(f => f.Id).Distinct().Count());
        Assert.All(fired.Zip(fired.Skip(1)), pair => Assert.True(pair.Second.Day - pair.First.Day >= 3, $"{pair.First} then {pair.Second}"));
        Assert.DoesNotContain(fired, f => f.Id == "side_butcher_knife");
    }

    [Fact]
    public void A_staff_story_needs_that_staff_member()
    {
        var data = StoryKit.WithSettings(s => s with { Tutorial = [] }, StoryKit.FreeUpgrades());
        data = data with { Story = data.Story! with { Dialogues = data.Story.Dialogues.Select(d => d.Id == "side_butcher_knife" ? d with { ChancePermille = 1000 } : d).ToList() } };
        var sim = StoryKit.NewSim(data);
        sim.SkipOpening();
        sim.Run(new SetStoryPrompts(true));
        sim.Run(new UpgradeStage());
        sim.ReadAll();

        sim.RunToClose();
        Assert.DoesNotContain("side_butcher_knife", sim.ReadAll());

        sim.Run(new StartNextDay());
        sim.Run(new HireEmployee("butcher"));
        for (var day = 0; day < 10 && !sim.Story.HasFired("side_butcher_knife"); day++)
        {
            sim.RunToClose();
            sim.ReadAll();
            sim.Run(new StartNextDay());
            sim.ReadAll();
        }

        Assert.True(sim.Story.HasFired("side_butcher_knife"));
    }

    [Fact]
    public void Same_seed_gives_the_same_story()
    {
        static List<string> Play(ulong seed)
        {
            var sim = StoryKit.NewSim(StoryKit.WithSettings(s => s with { Tutorial = [] }), seed);
            sim.SkipOpening();
            var shown = new List<string>();
            for (var day = 0; day < 25; day++)
            {
                sim.NextMorning();
                shown.AddRange(sim.ReadAll());
            }

            return shown;
        }

        Assert.Equal(Play(5), Play(5));
    }

    [Fact]
    public void The_shop_can_be_named_within_the_length_limit()
    {
        var sim = StoryKit.NewSim();
        var max = sim.Data.Story!.Settings.ShopNameMaxLength;

        sim.Run(new NameShop("   "));
        sim.Run(new NameShop(new string('x', max + 1)));
        Assert.Equal(2, sim.Drain<CommandRejected>().Count(r => r.Reason == RejectReason.InvalidName));
        Assert.Null(sim.Story.ShopName);

        sim.Run(new NameShop("  Lotus Market "));
        Assert.Equal("Lotus Market", sim.Story.ShopName);
        Assert.Equal("Lotus Market", sim.Drain<ShopNamed>().Single().Name);

        // Chinese characters count one each.
        sim.Run(new NameShop(new string('福', max)));
        Assert.Equal(new string('福', max), sim.Story.ShopName);
    }

    [Fact]
    public void The_naming_dialogue_asks_for_the_shop_name()
    {
        Assert.Equal(DialoguePrompt.ShopName, StoryKit.Real.Story!.Dialogue("opening_name_shop").Prompt);
    }

    [Fact]
    public void Without_story_data_nothing_story_related_happens()
    {
        var sim = SimTestKit.NewSim(data: SimTestKit.Data with { Story = null });
        sim.RunFullDay();

        Assert.Empty(sim.Story.Fired);
        Assert.Null(sim.Loan);
        Assert.Equal(1000, sim.CompetitionArrivalPermille());
    }
}
