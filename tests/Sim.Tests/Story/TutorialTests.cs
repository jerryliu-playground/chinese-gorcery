using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Story;
using ChineseGrocery.Sim.Time;
using static ChineseGrocery.Sim.Tests.Customers.CustomerScenario;

namespace ChineseGrocery.Sim.Tests.Story;

/// <summary>Spec 18.3: the father's calls come one at a time, each when its situation arises, and end when the player does the thing.</summary>
public class TutorialTests
{
    [Fact]
    public void The_first_days_teach_ordering_checkout_report_shelving_and_prices_in_play_order()
    {
        var sim = StoryKit.NewSim();
        var started = new List<int>();
        void Collect() => started.AddRange(sim.DrainEvents().OfType<TutorialStepStarted>().Select(e => e.Step));

        sim.SkipOpening();
        Collect();
        Assert.Equal(1, sim.Story.TutorialActive);
        Assert.Equal(["tutorial_1_order"], sim.ReadAll());

        // Doing something else does not count.
        sim.Run(new SetPrice("rice_jasmine", 999));
        Assert.Equal(1, sim.Story.TutorialActive);

        sim.Run(new PlaceOrder("rice_jasmine", 10));
        Assert.Contains(1, sim.Story.TutorialDone);
        Assert.Null(sim.Story.TutorialActive);

        sim.WalkPlayerTo(CashierCell);
        sim.Run(new OpenStore());
        Collect();
        Assert.Equal(3, sim.Story.TutorialActive);
        sim.ReadAll();
        sim.TickUntil(() => sim.Story.TutorialDone.Contains(3));

        sim.RunToClose();
        Collect();
        Assert.Equal(4, sim.Story.TutorialActive);
        sim.ReadAll();
        Assert.Equal(4, sim.Story.TutorialActive);
        sim.Run(new ReportViewed());
        Assert.Contains(4, sim.Story.TutorialDone);

        sim.Run(new StartNextDay());
        Collect();
        Assert.Equal(2, sim.Story.TutorialActive);
        sim.ReadAll();
        sim.Run(new RestockAll());
        Assert.Contains(2, sim.Story.TutorialDone);

        // Prices wait for day 3.
        sim.Tick();
        Assert.Null(sim.Story.TutorialActive);
        sim.NextMorning();
        Collect();
        Assert.Equal(3, sim.Clock.Day);
        Assert.Equal(5, sim.Story.TutorialActive);
        sim.ReadAll();
        sim.Run(new SetPrice("rice_jasmine", 1299));

        Assert.Equal([1, 3, 4, 2, 5], started);
        Assert.Equal([1, 2, 3, 4, 5], sim.Story.TutorialDone);
        Assert.Null(sim.Story.TutorialActive);
    }

    [Fact]
    public void A_step_waits_until_the_one_before_is_done()
    {
        var sim = StoryKit.NewSim();
        sim.SkipOpening();
        sim.ReadAll();

        // Opening without ordering: the checkout call has to wait.
        sim.RunToClose();
        sim.Run(new StartNextDay());
        Assert.Equal(1, sim.Story.TutorialActive);
        Assert.False(sim.Story.HasFired("tutorial_3_checkout"));

        sim.Run(new PlaceOrder("rice_jasmine", 10));
        sim.Run(new OpenStore());
        Assert.Equal(3, sim.Story.TutorialActive);
    }

    [Fact]
    public void A_finished_step_never_comes_back()
    {
        var sim = StoryKit.NewSim();
        sim.SkipOpening();
        sim.ReadAll();
        sim.Run(new PlaceOrder("rice_jasmine", 10));
        sim.DrainEvents();

        var later = new List<SimEvent>();
        for (var day = 0; day < 3; day++)
        {
            sim.NextMorning();
            later.AddRange(sim.DrainEvents());
            sim.Run(new PlaceOrder("rice_jasmine", 10));
        }

        Assert.DoesNotContain(later, e => e is TutorialStepStarted { Step: 1 } or TutorialStepCompleted { Step: 1 });
        Assert.DoesNotContain(later, e => e is DialogueQueued { DialogueId: "tutorial_1_order" });
    }

    private static GameData UpgradeCallOnly(GameData data) =>
        StoryKit.WithSettings(s => s with { Tutorial = [s.Tutorial[5] with { Step = 1, After = null }] }, data);

    [Fact]
    public void The_upgrade_call_comes_when_an_upgrade_is_affordable_and_closing_it_is_enough()
    {
        var sim = StoryKit.NewSim(UpgradeCallOnly(StoryKit.Real));
        sim.SkipOpening();
        Assert.Null(sim.Story.TutorialActive);

        var free = StoryKit.NewSim(UpgradeCallOnly(StoryKit.FreeUpgrades()));
        free.SkipOpening();
        Assert.Equal(1, free.Story.TutorialActive);
        Assert.Equal(["tutorial_6_upgrade"], free.ReadAll());
        Assert.Contains(1, free.Story.TutorialDone);
        Assert.Equal(1, free.Economy.Stage);
    }

    [Fact]
    public void Upgrading_while_the_upgrade_call_runs_finishes_it()
    {
        var sim = StoryKit.NewSim(UpgradeCallOnly(StoryKit.FreeUpgrades()));
        sim.SkipOpening();

        sim.Run(new UpgradeStage());

        Assert.Contains(1, sim.Story.TutorialDone);
    }

    [Fact]
    public void Someone_who_already_upgraded_gets_no_upgrade_call()
    {
        var data = StoryKit.WithSettings(
            s => s with { Tutorial = [s.Tutorial[0], s.Tutorial[5] with { Step = 2, After = [1] }] }, StoryKit.FreeUpgrades());
        var sim = StoryKit.NewSim(data);
        sim.SkipOpening();
        sim.ReadAll();
        sim.Run(new UpgradeStage());
        sim.ReadAll();

        sim.Run(new PlaceOrder("rice_jasmine", 10));

        Assert.Equal([1, 2], sim.Story.TutorialDone);
        Assert.False(sim.Story.HasFired("tutorial_6_upgrade"));
    }

    [Fact]
    public void The_view_can_show_the_running_step()
    {
        var sim = StoryKit.NewSim();
        Assert.Null(sim.ActiveTutorialStep);

        sim.SkipOpening();

        Assert.Equal("tutorial.step1.objective", sim.ActiveTutorialStep?.ObjectiveKey);
    }
}
