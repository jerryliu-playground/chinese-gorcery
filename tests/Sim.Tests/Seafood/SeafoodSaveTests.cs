using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Customers;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Save;
using ChineseGrocery.Sim.Staff;
using ChineseGrocery.Sim.Tasks;
using ChineseGrocery.Sim.Time;
using static ChineseGrocery.Sim.Tests.Customers.CustomerScenario;
using static ChineseGrocery.Sim.Tests.Seafood.SeafoodScenario;

namespace ChineseGrocery.Sim.Tests.Seafood;

/// <summary>Spec 12.10 item 3: water quality, animals in tanks and dead ones still in them survive save and load.</summary>
public class SeafoodSaveTests
{
    /// <summary>A busy seafood corner: fouled water, dead in the tank, a fishmonger killing to order for a queue.</summary>
    private static GameSim Busy(ulong seed)
    {
        var sim = Store(ScenarioData(perHour: 40, wants: Department.Seafood, patienceMinutes: 60), seed);
        var tank = Tank(sim, 7, Fish, Shrimp);
        Stock(sim, tank, 0, Fish, 6);
        Stock(sim, tank, 1, Shrimp, 12);
        Counter(sim);
        sim.Run(new HireEmployee("fishmonger"));
        sim.AddDead(tank, DeadLot(sim, Crab, 2));
        sim.SetWaterQuality(tank, 70);
        sim.WalkPlayerTo(CashierCell);
        Open(sim);
        return sim;
    }

    [Fact]
    public void Loading_mid_kill_with_dirty_water_and_dead_in_the_tank_matches_never_stopping()
    {
        var straight = Busy(3);
        var paused = Busy(3);
        TickUntil(paused, () => paused.Workers.Any(w => w.Step is WorkStep.Killing or WorkStep.Carrying) && paused.Customers.Any(c => c.State == CustomerState.AtCounter));
        var tank = paused.Layout.Fixtures.Single(f => f.FixtureId == "fish_tank").InstanceId;
        Assert.True(paused.WaterQualityOf(tank) < 70);

        var json = SaveGame.Write(paused);
        var resumed = SaveGame.Read(paused.Data, json);
        Assert.Equal(json, SaveGame.Write(resumed));
        Assert.Equal(paused.WaterQualityOf(tank), resumed.WaterQualityOf(tank));
        Assert.Equal(paused.DeadIn(tank), resumed.DeadIn(tank));

        while (straight.TickCount < paused.TickCount) straight.Tick();
        for (var i = 0; i < 600; i++) straight.Tick();
        for (var i = 0; i < 600; i++) resumed.Tick();
        Assert.Equal(SaveGame.Write(straight), SaveGame.Write(resumed));
    }

    [Fact]
    public void Loading_after_a_night_of_deaths_keeps_the_dead_in_the_tank()
    {
        var sim = Store();
        var tank = Tank(sim, 7, Fish, Shrimp);
        Stock(sim, tank, 0, Fish, 6);
        Stock(sim, tank, 1, Shrimp, 12);
        sim.TankCleaningDisabled = true;
        sim.SetWaterQuality(tank, 0);
        sim.OpenAndRunToClose();

        var json = SaveGame.Write(sim);
        var resumed = SaveGame.Read(sim.Data, json);

        Assert.NotEmpty(sim.DeadIn(tank));
        Assert.True(sim.Reports[^1].SeafoodDeaths > 0);
        Assert.Equal(sim.DeadIn(tank), resumed.DeadIn(tank));
        Assert.Equal(json, SaveGame.Write(resumed));
    }
}
