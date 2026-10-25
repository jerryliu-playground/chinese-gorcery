using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Tests.Calendar;

/// <summary>
/// Calendar setups: the real seasons, festivals and events in a store with no customers (unless asked for), and stores
/// where one chosen event happens every morning.
/// Year 1 festival days: Dragon Boat 33, Mid-Autumn 71, Thanksgiving 81, Christmas 94, Spring Festival 104.
/// </summary>
public static class CalendarScenario
{
    public const int DragonBoat = 33;
    public const int MidAutumn = 71;
    public const int Thanksgiving = 81;
    public const int SpringFestival = 104;

    public static GameData Quiet(GameData? data = null, int perHour = 0)
    {
        var d = data ?? SimTestKit.CalendarData;
        return d with { Economy = d.Economy with { CustomerArrivals = [new ArrivalBucket(d.Economy.OpenMinuteOfDay, perHour)], StartingCashCents = 1_000_000 } };
    }

    /// <summary>A quiet store where the given event (optionally changed) happens every morning from day 1.</summary>
    public static GameData OnlyEvent(string eventId, Func<EventDef, EventDef>? change = null, GameData? data = null)
    {
        var quiet = Quiet(data);
        var def = SimTestKit.RealData.Events!.Events.Single(e => e.Id == eventId);
        return quiet with { Events = SimTestKit.RealData.Events! with { FirstDay = 1, DailyChancePermille = 1000, Events = [change is null ? def : change(def)] } };
    }

    public static GameSim Store(GameData data, int day = 1, ulong seed = 1)
    {
        var sim = new GameSim(data, seed);
        sim.Clock.Day = day;
        return sim;
    }

    /// <summary>Closes the current day without playing it and starts the next one (deliveries, the morning's event).</summary>
    public static void NextMorning(GameSim sim)
    {
        sim.Clock.Phase = DayPhase.Closed;
        sim.Run(new StartNextDay());
    }

    public static ProductDef Product(GameSim sim, string id) => sim.Data.Products.Single(p => p.Id == id);

    /// <summary>Assigns slot 0 of the first shelf and puts units straight onto it.</summary>
    public static StockLocation Shelve(GameSim sim, string productId, int units, int slot = 0, int? bestBefore = null)
    {
        var location = new StockLocation(sim.InstanceOf("shelf"), slot);
        sim.Run(new AssignShelfSlot(location.FixtureInstanceId, slot, productId));
        var p = Product(sim, productId);
        sim.Inventory.GetOrCreate(location).Add(new StockLot(productId, units, bestBefore ?? sim.Clock.Day + p.ShelfLifeDays - 1, p.CostCents));
        return location;
    }

    /// <summary>From PreOpen: opens and ticks until the clock reaches the minute.</summary>
    public static void OpenUntil(GameSim sim, int minuteOfDay)
    {
        if (sim.Clock.Phase == DayPhase.PreOpen) sim.Run(new OpenStore());
        while (sim.Clock.MinuteOfDay < minuteOfDay && sim.Clock.Phase == DayPhase.Open) sim.Tick();
    }
}
