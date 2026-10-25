using ChineseGrocery.Sim.Calendar;
using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Time;
using ChineseGrocery.Sim.Save;
using static ChineseGrocery.Sim.Tests.Calendar.CalendarScenario;

namespace ChineseGrocery.Sim.Tests.Calendar;

/// <summary>Spec 15.5 (3): the calendar position, events in progress and the decorations owned survive a save.</summary>
public class CalendarSaveTests
{
    private static GameSim Reload(GameSim sim) => SaveGame.Read(sim.Data, SaveGame.Write(sim));

    [Fact]
    public void Date_events_and_decorations_round_trip()
    {
        var sim = Store(OnlyEvent("supplier_stockout"), day: MidAutumn - 3);
        Shelve(sim, "rice_jasmine", 4);
        sim.Run(new BuyDecoration("rabbit_lantern"), new HangDecoration("rabbit_lantern"), new BuyDecoration("couplet_left"));
        NextMorning(sim);
        Assert.NotEmpty(sim.StoreEvents);

        var loaded = Reload(sim);

        Assert.Equal(SaveGame.Write(sim), SaveGame.Write(loaded));
        Assert.Equal(sim.Date, loaded.Date);
        Assert.Equal(sim.StoreEvents, loaded.StoreEvents);
        Assert.Equal(sim.Decorations, loaded.Decorations);
        Assert.Equal(sim.DemandOf(Product(sim, "mooncake")), loaded.DemandOf(Product(loaded, "mooncake")));
        Assert.Equal(RejectReason.SupplierOutOfStock, RejectionOf(Advance(loaded), new PlaceOrder("rice_jasmine", 1)));
    }

    [Fact]
    public void A_save_mid_power_cut_plays_on_exactly_as_if_never_saved()
    {
        GameSim Fresh()
        {
            var sim = Store(Seafood.SeafoodScenario.Unlocked(OnlyEvent("power_cut", e => e with { MinStage = 1 })), day: 4);
            NextMorning(sim);
            var tank = Seafood.SeafoodScenario.Tank(sim, 7, Seafood.SeafoodScenario.Fish);
            Seafood.SeafoodScenario.Stock(sim, tank, 0, Seafood.SeafoodScenario.Fish, 4);
            OpenUntil(sim, 13 * 60);
            return sim;
        }

        var straight = Fresh();
        var resumed = Reload(Fresh());
        Assert.True(resumed.PowerOut);
        foreach (var sim in new[] { straight, resumed })
        {
            while (sim.Clock.Phase != DayPhase.Closed) sim.Tick();
            sim.Run(new StartNextDay());
            for (var day = 0; day < 6; day++) sim.RunFullDay();
        }

        Assert.Equal(SaveGame.Write(straight), SaveGame.Write(resumed));
    }

    [Fact]
    public void Saves_from_before_the_calendar_still_load()
    {
        var json = SaveGame.Write(SimTestKit.NewSim());
        var old = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
        old.Remove("calendar");

        var loaded = SaveGame.Read(SimTestKit.Data, old.ToJsonString());

        Assert.Empty(loaded.StoreEvents);
        Assert.Empty(loaded.Decorations);
    }

    private static GameSim Advance(GameSim sim)
    {
        sim.Clock.Day++;
        return sim;
    }

    private static RejectReason? RejectionOf(GameSim sim, SimCommand command)
    {
        sim.DrainEvents();
        sim.Run(command);
        return sim.DrainEvents().OfType<CommandRejected>().SingleOrDefault()?.Reason;
    }
}
