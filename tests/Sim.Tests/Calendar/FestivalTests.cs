using ChineseGrocery.Sim.Calendar;
using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Time;
using static ChineseGrocery.Sim.Tests.Calendar.CalendarScenario;

namespace ChineseGrocery.Sim.Tests.Calendar;

/// <summary>Spec 15.2: festival demand curves, festival goods, the order window, reminders and festival-day crowds.</summary>
public class FestivalTests
{
    private static readonly FestivalsDef Festivals = SimTestKit.RealData.Festivals!;

    private static FestivalDef Festival(string id) => Festivals.Festivals.Single(f => f.Id == id);

    private static int DemandOn(GameSim sim, int day, string productId)
    {
        sim.Clock.Day = day;
        return sim.DemandOf(Product(sim, productId)).Permille;
    }

    [Fact]
    public void Share_rises_through_the_prep_days_peaks_on_the_day_and_falls_over_two_days()
    {
        var f = Festival("mid_autumn");
        var shares = Enumerable.Range(-9, 13).Select(o => GameCalendar.SharePermille(Festivals, f, o)).ToList();

        Assert.Equal([0, 0, 150, 241, 333, 425, 516, 608, 700, 1000, 400, 100, 0], shares);
    }

    [Fact]
    public void Festival_goods_barely_sell_outside_their_window_and_peak_on_the_day()
    {
        var sim = Store(Quiet());
        var curve = Enumerable.Range(MidAutumn - 9, 13).Select(d => DemandOn(sim, d, "mooncake")).ToList();

        // Day 74 on is Thanksgiving's prep, which lifts all snacks a little (mooncakes too).
        Assert.All(new[] { 1, 20, 40, 60, 63, 100 }, d => Assert.Equal(Festivals.OffSeasonDemandPermille, DemandOn(sim, d, "mooncake")));
        Assert.Equal(curve.Max(), curve[9]);
        Assert.Equal(Festival("mid_autumn").GoodsPeakPermille, curve[9]);
        for (var i = 2; i < 9; i++) Assert.True(curve[i] > curve[i - 1], $"demand should rise on prep day {i - 9}");
        Assert.True(curve[10] < curve[9] && curve[11] < curve[10] && curve[12] < curve[11]);
        Assert.True(curve[12] < 2 * Festivals.OffSeasonDemandPermille);
        Assert.Equal(Festivals.OffSeasonDemandPermille, (int)sim.DemandOf(Product(sim, "mooncake")).Baseline);
    }

    [Fact]
    public void Ordinary_goods_get_the_festival_boost_around_it_and_are_normal_otherwise()
    {
        var sim = Store(Quiet());

        Assert.Equal(1000, DemandOn(sim, 50, "crab"));
        Assert.Equal(2500, DemandOn(sim, MidAutumn, "crab"));
        Assert.True(DemandOn(sim, MidAutumn - 3, "crab") is > 1000 and < 2500);
        Assert.Equal(3000, DemandOn(sim, SpringFestival, "crab"));
        Assert.Equal(1000, DemandOn(sim, 50, "rice_jasmine"));
    }

    [Fact]
    public void Seasons_change_drink_and_fruit_demand()
    {
        var sim = Store(Quiet());

        Assert.Equal(1000, DemandOn(sim, 10, "soy_milk"));
        Assert.Equal(1500, DemandOn(sim, 40, "soy_milk"));
        Assert.Equal(1300, DemandOn(sim, 40, "lychee"));
        Assert.Equal(800, DemandOn(sim, 108, "jasmine_tea"));
        Assert.Equal(1000, DemandOn(sim, 40, "rice_jasmine"));
    }

    [Fact]
    public void Festival_goods_can_only_be_ordered_in_the_days_before_their_festival()
    {
        var sim = Store(Quiet());
        RejectReason? Order(int day)
        {
            sim.Clock.Day = day;
            sim.DrainEvents();
            sim.Run(new PlaceOrder("mooncake", 1));
            return sim.DrainEvents().OfType<CommandRejected>().SingleOrDefault()?.Reason;
        }

        Assert.Equal(RejectReason.OutOfSeason, Order(50));
        Assert.Equal(RejectReason.OutOfSeason, Order(MidAutumn - Festivals.OrderWindowDays - 1));
        Assert.Null(Order(MidAutumn - Festivals.OrderWindowDays));
        Assert.Null(Order(MidAutumn - 1));
        Assert.Equal(RejectReason.OutOfSeason, Order(MidAutumn));
        Assert.Equal(RejectReason.OutOfSeason, Order(MidAutumn + 1));
        Assert.Null(Order(MidAutumn - 1 + 112));
    }

    [Fact]
    public void A_reminder_comes_on_the_first_prep_day()
    {
        var sim = Store(Quiet(), day: MidAutumn - 9);
        var reminders = new List<(int Day, FestivalReminder Reminder)>();
        for (var i = 0; i < 6; i++)
        {
            NextMorning(sim);
            reminders.AddRange(sim.DrainEvents().OfType<FestivalReminder>().Select(r => (sim.Clock.Day, r)));
        }

        var (day, reminder) = Assert.Single(reminders);
        Assert.Equal((MidAutumn - 7, "mid_autumn", 7), (day, reminder.FestivalId, reminder.DaysUntil));
    }

    [Fact]
    public void More_customers_come_on_the_festival_day_only()
    {
        var sim = Store(Quiet());

        sim.Clock.Day = MidAutumn - 1;
        Assert.Equal(1000, sim.ArrivalPermille());
        sim.Clock.Day = MidAutumn;
        Assert.Equal(Festival("mid_autumn").DayCustomersPermille, sim.ArrivalPermille());
        sim.Clock.Day = SpringFestival;
        Assert.Equal(Festival("spring_festival").DayCustomersPermille, sim.ArrivalPermille());
    }

    [Fact]
    public void Festival_day_brings_more_shoppers_through_the_door()
    {
        int Customers(int day)
        {
            var sim = Store(Quiet(perHour: 20), day: day);
            sim.OpenAndRunToClose();
            return sim.Reports[^1].Customers;
        }

        Assert.True(Customers(MidAutumn) > Customers(MidAutumn - 20) * 13 / 10);
    }

    [Fact]
    public void Shoppers_pick_mooncakes_on_the_day_and_hardly_ever_off_season()
    {
        var sim = Store(Quiet());
        Shelve(sim, "mooncake", 12, slot: 0);
        Shelve(sim, "shrimp_chips", 16, slot: 1);
        Shelve(sim, "milk_candy", 16, slot: 2);
        Shelve(sim, "rice_jasmine", 4, slot: 3);
        var student = sim.Data.CustomerTypes.Single(t => t.Id == "international_student");
        int Mooncakes(int day)
        {
            sim.Clock.Day = day;
            return Enumerable.Range(0, 400).Sum(_ => sim.BuildShoppingList(student).Count(id => id == "mooncake"));
        }

        var offSeason = Mooncakes(40);
        var peak = Mooncakes(MidAutumn);

        Assert.True(offSeason < 15, $"off season {offSeason}");
        Assert.True(peak > 10 * Math.Max(1, offSeason), $"peak {peak} vs off season {offSeason}");
    }

    [Fact]
    public void Stocking_mooncakes_off_season_does_not_cost_the_other_snacks_their_shoppers()
    {
        var plain = Store(Quiet());
        Shelve(plain, "shrimp_chips", 16, slot: 1);
        Shelve(plain, "milk_candy", 16, slot: 2);
        Shelve(plain, "rice_jasmine", 4, slot: 3);
        var withMooncakes = Store(Quiet());
        Shelve(withMooncakes, "mooncake", 12, slot: 0);
        Shelve(withMooncakes, "shrimp_chips", 16, slot: 1);
        Shelve(withMooncakes, "milk_candy", 16, slot: 2);
        Shelve(withMooncakes, "rice_jasmine", 4, slot: 3);
        var student = plain.Data.CustomerTypes.Single(t => t.Id == "international_student");
        int Snacks(GameSim sim) => Enumerable.Range(0, 400).Sum(_ => sim.BuildShoppingList(student).Count(id => Product(sim, id).Department == Department.Snacks));
        plain.Clock.Day = withMooncakes.Clock.Day = 40;

        var without = Snacks(plain);
        var with = Snacks(withMooncakes);

        Assert.InRange(with, without * 9 / 10, without * 11 / 10);
    }
}
