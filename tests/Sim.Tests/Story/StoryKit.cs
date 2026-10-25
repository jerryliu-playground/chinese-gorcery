using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Story;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Tests.Story;

/// <summary>Setup and drivers for story tests: the real data (opening stock included), with tweaks.</summary>
public static class StoryKit
{
    public static GameData Real => SimTestKit.RealData;

    public static GameData WithSettings(Func<StorySettingsDef, StorySettingsDef> change, GameData? data = null)
    {
        var d = data ?? Real;
        return d with { Story = d.Story! with { Settings = change(d.Story.Settings) } };
    }

    /// <summary>Every stage upgrade free and with no revenue target.</summary>
    public static GameData FreeUpgrades(GameData? data = null)
    {
        var d = data ?? Real;
        return d with { Stages = d.Stages.Select(s => s with { RequiredCumulativeRevenueCents = 0, UpgradeCostCents = 0 }).ToList() };
    }

    public static GameSim NewSim(GameData? data = null, ulong seed = 1) => new(data ?? Real, seed);

    public static void SkipOpening(this GameSim sim)
    {
        Assert.Equal("opening_captions", sim.Story.Current);
        sim.Run(new SkipDialogue("opening_captions"));
    }

    /// <summary>Reads every queued dialogue to the end, answering choices with the first answer. Returns what was shown.</summary>
    public static List<string> ReadAll(this GameSim sim)
    {
        var shown = new List<string>();
        while (sim.Story.Current is { } id)
        {
            var choice = sim.Data.Story!.Dialogue(id).Choices?.FirstOrDefault()?.Id;
            sim.Run(new FinishDialogue(id, choice));
            Assert.NotEqual(id, sim.Story.Pending.FirstOrDefault());
            shown.Add(id);
        }

        return shown;
    }

    public static void TickUntil(this GameSim sim, Func<bool> done, int max = 30_000)
    {
        for (var i = 0; i < max && !done(); i++) sim.Tick();
        Assert.True(done(), "condition never came true");
    }

    public static void RunToClose(this GameSim sim)
    {
        if (sim.Clock.Phase == DayPhase.PreOpen) sim.Run(new OpenStore());
        sim.TickUntil(() => sim.Clock.Phase == DayPhase.Closed, max: 24 * 60 * 6 + 10);
    }

    /// <summary>From any phase, to the next morning's PreOpen.</summary>
    public static void NextMorning(this GameSim sim)
    {
        sim.RunToClose();
        sim.Run(new StartNextDay());
    }

    public static List<T> Drain<T>(this GameSim sim) where T : SimEvent => sim.DrainEvents().OfType<T>().ToList();
}
