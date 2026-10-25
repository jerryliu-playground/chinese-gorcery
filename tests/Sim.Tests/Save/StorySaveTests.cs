using System.Text.Json.Nodes;
using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Save;
using ChineseGrocery.Sim.Story;
using ChineseGrocery.Sim.Tests.Story;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Tests.Save;

/// <summary>Spec 18.7.3: story progress, tutorial step, loan and shop name survive a save and load.</summary>
public class StorySaveTests
{
    private static GameSim Reload(GameSim sim) => SaveGame.Read(sim.Data, SaveGame.Write(sim));

    [Fact]
    public void Mid_opening_the_same_dialogue_is_still_on_screen()
    {
        var sim = StoryKit.NewSim();
        sim.Run(new FinishDialogue("opening_captions"));

        var loaded = Reload(sim);

        Assert.Equal(["opening_mom_call"], loaded.Story.Pending);
        Assert.Equal(SaveGame.Write(sim), SaveGame.Write(loaded));
    }

    [Fact]
    public void Choices_name_tutorial_loan_and_settings_round_trip()
    {
        var sim = StoryKit.NewSim();
        sim.Run(new FinishDialogue("opening_captions"));
        sim.Run(new FinishDialogue("opening_mom_call"));
        sim.Run(new FinishDialogue("opening_retire", "accept"));
        sim.Run(new FinishDialogue("opening_retire_accept"));
        sim.Run(new NameShop("Lotus Market"));
        sim.ReadAll();
        sim.Run(new PlaceOrder("rice_jasmine", 10));
        sim.Run(new PayLoan(12_345));
        sim.Run(new SetStoryPrompts(false));
        sim.Run(new OpenStore());
        Assert.Equal(3, sim.Story.TutorialActive);

        var loaded = Reload(sim);

        Assert.Equal("accept", loaded.Story.Choices["opening_retire"]);
        Assert.Equal("Lotus Market", loaded.Story.ShopName);
        Assert.Equal([1], loaded.Story.TutorialDone);
        Assert.Equal(3, loaded.Story.TutorialActive);
        Assert.Equal(["tutorial_3_checkout"], loaded.Story.Pending);
        Assert.False(loaded.Story.OptionalEnabled);
        Assert.Equal(sim.Loan!.BalanceCents, loaded.Loan!.BalanceCents);
        Assert.Equal(sim.Story.Fired, loaded.Story.Fired);
        Assert.Equal(SaveGame.Write(sim), SaveGame.Write(loaded));
    }

    [Fact]
    public void Loading_and_playing_on_tells_the_same_story_as_never_stopping()
    {
        static void Play(GameSim sim, int days)
        {
            for (var d = 0; d < days; d++)
            {
                sim.NextMorning();
                sim.ReadAll();
                sim.Run(new PlaceOrder("rice_jasmine", 5));
            }
        }

        var straight = StoryKit.NewSim(seed: 9);
        straight.SkipOpening();
        Play(straight, 16);

        var first = StoryKit.NewSim(seed: 9);
        first.SkipOpening();
        Play(first, 8);
        var resumed = Reload(first);
        Play(resumed, 8);

        Assert.Equal(SimSnapshot.Describe(straight), SimSnapshot.Describe(resumed));
        Assert.Contains(resumed.Story.Fired, id => id.StartsWith("side_", StringComparison.Ordinal));
        Assert.True(resumed.Loan!.BalanceCents < resumed.Data.Story!.Settings.Loan.PrincipalCents);
    }

    [Fact]
    public void A_save_from_before_the_story_continues_past_the_opening_and_tutorial()
    {
        var sim = StoryKit.NewSim(StoryKit.FreeUpgrades());
        sim.SkipOpening();
        sim.Run(new UpgradeStage());
        var json = JsonNode.Parse(SaveGame.Write(sim))!.AsObject();
        json.Remove("story");
        foreach (var name in new[] { "today" }) json[name]!.AsObject().Remove("loanPaymentCents");

        var old = SaveGame.Read(sim.Data, json.ToJsonString());

        Assert.Empty(old.Story.Pending);
        Assert.True(old.Story.IsShown("opening_walk_in"));
        Assert.True(old.Story.HasFired("chapter2_change"));
        Assert.True(old.Story.HasFired("chapter2_reassured"));
        Assert.False(old.Story.HasFired("chapter3_rival"));
        Assert.Equal([1, 2, 3, 4, 5, 6], old.Story.TutorialDone);
        Assert.Equal(sim.Data.Story!.Settings.Loan.PrincipalCents, old.Loan!.BalanceCents);

        old.Run(new UpgradeStage());
        Assert.Equal("chapter3_rival", old.Story.Current);
    }

    [Fact]
    public void A_save_queueing_an_unknown_dialogue_is_rejected()
    {
        var sim = StoryKit.NewSim();
        var json = SaveGame.Write(sim).Replace("\"pending\":[\"opening_captions\"]", "\"pending\":[\"no_such_scene\"]");
        Assert.Contains("no_such_scene", json);

        Assert.Throws<SaveException>(() => SaveGame.Read(sim.Data, json));
    }

    [Fact]
    public void Queued_story_commands_are_saved_too()
    {
        var sim = StoryKit.NewSim();
        sim.Enqueue(new SkipDialogue("opening_captions"));
        sim.Enqueue(new PayLoan(100));
        sim.Enqueue(new NameShop("Lotus"));
        sim.Enqueue(new ReportViewed());
        sim.Enqueue(new SetStoryPrompts(false));
        sim.Enqueue(new FinishDialogue("x", "y"));

        var loaded = Reload(sim);
        loaded.Tick();
        sim.Tick();

        Assert.Equal(SaveGame.Write(sim), SaveGame.Write(loaded));
        Assert.Equal("Lotus", loaded.Story.ShopName);
    }
}
