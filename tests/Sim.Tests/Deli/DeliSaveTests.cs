using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Customers;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Deli;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Save;
using ChineseGrocery.Sim.Staff;
using ChineseGrocery.Sim.Tasks;
using static ChineseGrocery.Sim.Tests.Customers.CustomerScenario;
using static ChineseGrocery.Sim.Tests.Deli.DeliScenario;

namespace ChineseGrocery.Sim.Tests.Deli;

/// <summary>Spec 13.10 item 3: the menu, dishes on the stations and cooking in progress survive save and load.</summary>
public class DeliSaveTests
{
    private static GameSim Busy(ulong seed)
    {
        var sim = DeliStore(ScenarioData(perHour: 30, wants: Department.Deli, patienceMinutes: 60), seed);
        Stove(sim);
        var table = SteamTable(sim);
        var roast = RoastStation(sim);
        var backroom = Backroom(sim);
        Put(sim, backroom, "bok_choy", 30, ageDays: 1);
        Put(sim, backroom, "whole_duck", 3);
        sim.Run(new SetMenuItem(table, 0, "stir_fried_bok_choy", 30));
        sim.Run(new SetMenuItem(roast, 0, "roast_duck", 16));
        sim.Run(new HireEmployee("chef"));
        sim.WalkPlayerTo(CashierCell);
        Open(sim);
        return sim;
    }

    [Fact]
    public void Loading_mid_cooking_with_a_queue_at_the_station_matches_never_stopping()
    {
        var straight = Busy(2);
        var paused = Busy(2);
        TickUntil(paused, () => paused.Workers.Any(w => w.Step == WorkStep.Cooking) && paused.Customers.Any(c => c.State is CustomerState.AtCounter or CustomerState.ToCounter));
        Assert.NotEmpty(paused.Menu);
        Assert.Contains(paused.Menu.Keys, slot => paused.CookedToday(slot) > 0);

        var json = SaveGame.Write(paused);
        var resumed = SaveGame.Read(paused.Data, json);
        Assert.Equal(json, SaveGame.Write(resumed));
        Assert.Equal(paused.Menu, resumed.Menu);

        while (straight.TickCount < paused.TickCount) straight.Tick();
        for (var i = 0; i < 900; i++)
        {
            straight.Tick();
            resumed.Tick();
        }

        Assert.Equal(SaveGame.Write(straight), SaveGame.Write(resumed));
    }

    [Fact]
    public void Dishes_on_a_station_keep_their_price_bonus_through_a_save()
    {
        var sim = DeliStore();
        var table = SteamTable(sim);
        sim.Inventory.GetOrCreate(new StockLocation(table, 0)).Add(new StockLot("braised_pork", 3, sim.Clock.Day, 100, PriceBonusPermille: 150));

        var resumed = SaveGame.Read(sim.Data, SaveGame.Write(sim));

        Assert.Equal(150, resumed.Inventory.Get(new StockLocation(table, 0)).Lots.Single().PriceBonusPermille);
    }
}
