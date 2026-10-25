using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Save;
using ChineseGrocery.Sim.Story;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Tests.Story;

/// <summary>Spec 18.4: a chapter per stage upgrade, department calls, the epilogue, and the chain supermarket's pull on customers.</summary>
public class ChapterTests
{
    private static GameSim UpgradeReady(GameData? data = null)
    {
        var sim = StoryKit.NewSim(StoryKit.WithSettings(s => s with { Tutorial = [] }, StoryKit.FreeUpgrades(data)));
        sim.SkipOpening();
        sim.Run(new SetStoryPrompts(false));
        return sim;
    }

    [Fact]
    public void Each_upgrade_brings_its_chapter_first_then_the_new_departments()
    {
        var sim = UpgradeReady();

        sim.Run(new UpgradeStage());
        Assert.Equal(["chapter2_change", "dept_meat", "dept_produce"], sim.Story.Pending);
        sim.Run(new FinishDialogue("chapter2_change", "ask"));
        Assert.Equal("chapter2_advice", sim.Story.Current);
        sim.ReadAll();

        sim.Run(new UpgradeStage());
        Assert.Equal(["chapter3_rival", "dept_seafood"], sim.Story.Pending);
        sim.ReadAll();

        sim.Run(new UpgradeStage());
        Assert.Equal(["chapter4_own_store", "dept_deli"], sim.Story.Pending);
        sim.Run(new FinishDialogue("chapter4_own_store"));

        // The epilogue follows straight on, then play continues.
        Assert.Equal(["chapter4_epilogue", "dept_deli"], sim.Story.Pending);
        sim.ReadAll();
        sim.NextMorning();
        Assert.Equal(DayPhase.PreOpen, sim.Clock.Phase);
    }

    [Fact]
    public void A_chapter_fires_once_even_if_the_stage_event_repeats_after_a_reload()
    {
        var sim = UpgradeReady();
        sim.Run(new UpgradeStage());
        sim.ReadAll();

        var reloaded = SaveGame.Read(sim.Data, SaveGame.Write(sim));
        reloaded.Run(new UpgradeStage());

        Assert.DoesNotContain("chapter2_change", reloaded.Story.Pending);
        Assert.Equal("chapter3_rival", reloaded.Story.Current);
    }

    [Fact]
    public void Competition_starts_with_chapter_three_and_hurts_less_with_a_better_reputation()
    {
        var sim = UpgradeReady();
        Assert.Equal(1000, sim.CompetitionArrivalPermille());
        sim.Run(new UpgradeStage());
        Assert.Equal(1000, sim.CompetitionArrivalPermille());
        sim.Run(new UpgradeStage());

        Assert.Equal(850, sim.CompetitionArrivalPermille());

        var curve = sim.Data.Story!.Settings.Competition;
        Assert.Equal(700, GameSim.CompetitionFactor(curve, 0));
        Assert.Equal(925, GameSim.CompetitionFactor(curve, 75));
        Assert.Equal(1000, GameSim.CompetitionFactor(curve, 100));
        Assert.True(GameSim.CompetitionFactor(curve, 20) < GameSim.CompetitionFactor(curve, 60));
    }

    [Fact]
    public void The_reputation_the_competition_reads_comes_from_one_place()
    {
        var good = UpgradeReady(StoryKit.WithSettings(s => s with { Competition = s.Competition with { PlaceholderReputation = 100 } }));
        good.Run(new UpgradeStage());
        good.Run(new UpgradeStage());

        Assert.Equal(100, good.CurrentReputation());
        Assert.Equal(1000, good.CompetitionArrivalPermille());
    }

    [Fact]
    public void Competition_really_keeps_customers_away()
    {
        var curve = new List<CompetitionPoint> { new(0, 0) };
        var sim = UpgradeReady(StoryKit.WithSettings(s => s with { Competition = s.Competition with { ArrivalPermilleByReputation = curve } }));
        sim.Run(new UpgradeStage());
        sim.RunToClose();
        Assert.True(sim.Reports[^1].Customers > 0);
        sim.Run(new StartNextDay());

        sim.Run(new UpgradeStage());
        sim.RunToClose();

        Assert.Equal(0, sim.Reports[^1].Customers);
    }
}
