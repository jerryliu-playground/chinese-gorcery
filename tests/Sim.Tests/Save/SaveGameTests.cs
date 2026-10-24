using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Customers;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Save;
using ChineseGrocery.Sim.Staff;
using ChineseGrocery.Sim.Tasks;
using ChineseGrocery.SimRunner;
using static ChineseGrocery.Sim.Tests.Customers.CustomerScenario;

namespace ChineseGrocery.Sim.Tests.Save;

/// <summary>Spec 5.11 / 7.4: a save holds the whole state; loading and playing on matches never having stopped.</summary>
public class SaveGameTests
{
    private static GameSim Reload(GameSim sim) => SaveGame.Read(sim.Data, SaveGame.Write(sim));

    [Fact]
    public void Save_is_versioned_json()
    {
        var json = SaveGame.Write(SimTestKit.NewSim());

        Assert.Contains($"\"version\":{SaveGame.CurrentVersion}", json);
    }

    [Fact]
    public void Round_trip_keeps_every_field_at_many_points_of_play()
    {
        var sim = SimTestKit.NewSim(2);
        var bot = new PlayerBot(stockTarget: 40);
        var checks = 0;
        bot.AfterTick = () =>
        {
            if (sim.TickCount % 997 != 0) return;
            var json = SaveGame.Write(sim);
            Assert.Equal(json, SaveGame.Write(SaveGame.Read(sim.Data, json)));
            checks++;
        };

        for (var day = 0; day < 24; day++) bot.PlayDay(sim);

        // Long enough to save through stage 2, meat and staff.
        Assert.True(checks > 50);
        Assert.Contains(sim.Reports.SelectMany(r => r.Sales), s => s.ProductId == "pork_belly");
        Assert.Equal(2, sim.Economy.Stage);
        Assert.Contains(sim.Workers, w => w.RoleId is not null);
    }

    [Fact]
    public void Loading_at_the_start_of_a_day_and_playing_on_matches_never_stopping()
    {
        var straight = SimTestKit.NewSim(4);
        var bot = new PlayerBot(stockTarget: 40);
        for (var day = 0; day < 14; day++) bot.PlayDay(straight);

        var first = SimTestKit.NewSim(4);
        var firstBot = new PlayerBot(stockTarget: 40);
        for (var day = 0; day < 7; day++) firstBot.PlayDay(first);
        var resumed = Reload(first);
        var secondBot = new PlayerBot(stockTarget: 40);
        for (var day = 7; day < 14; day++) secondBot.PlayDay(resumed);

        Assert.Equal(SaveGame.Write(straight), SaveGame.Write(resumed));
    }

    [Fact]
    public void Loading_mid_checkout_with_a_queue_and_a_pending_command_matches_never_stopping()
    {
        var sim = SimTestKit.NewSim(data: ScenarioData(perHour: 360, patienceMinutes: 20));
        ShelveDirectly(sim, Rice, 4);
        Open(sim);
        TickUntil(sim, () => sim.Customers.Count(c => c.State == CustomerState.Queued) >= 2);
        sim.WalkPlayerTo(CashierCell);
        sim.Run(new PlayerInteract());
        Assert.Equal(WorkStep.Serving, sim.Player.Step);
        sim.Enqueue(new MovePlayer(new GridPoint(7, 3)));

        var resumed = Reload(sim);
        for (var i = 0; i < 600; i++)
        {
            sim.Tick();
            resumed.Tick();
        }

        Assert.Equal(SaveGame.Write(sim), SaveGame.Write(resumed));
    }

    [Fact]
    public void Loading_while_the_butcher_is_mid_cut_matches_never_stopping()
    {
        var sim = Meat.MeatScenario.Stage2(primals: 2);
        sim.Run(new HireEmployee("butcher"));
        var butcher = sim.Workers.Single(w => w.RoleId == "butcher");
        TickUntil(sim, () => butcher.Step == WorkStep.Cutting);

        var resumed = Reload(sim);
        sim.OpenAndRunToClose();
        resumed.OpenAndRunToClose();

        Assert.Equal(SaveGame.Write(sim), SaveGame.Write(resumed));
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{\"version\":1}")]
    public void Broken_saves_are_rejected(string json)
    {
        Assert.Throws<SaveException>(() => SaveGame.Read(SimTestKit.Data, json));
    }

    [Fact]
    public void Saves_from_an_unknown_version_are_rejected()
    {
        var json = SaveGame.Write(SimTestKit.NewSim()).Replace($"\"version\":{SaveGame.CurrentVersion}", "\"version\":999");

        var ex = Assert.Throws<SaveException>(() => SaveGame.Read(SimTestKit.Data, json));
        Assert.Contains("999", ex.Message);
    }
}
