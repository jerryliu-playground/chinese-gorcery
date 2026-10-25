using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Customers;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Reputation;
using ChineseGrocery.Sim.Time;
using static ChineseGrocery.Sim.Tests.Reputation.ReputationScenario;

namespace ChineseGrocery.Sim.Tests.Reputation;

/// <summary>Spec 16.3: trending products each season, and ABC teens who come looking for them.</summary>
public class TrendTests
{
    private const string Teen = "abc_teen";

    private static GameData TeensAlwaysSeek(int perHour = 6, int heartPermille = 0, IReadOnlyList<ArrivalBucket>? arrivals = null) =>
        Only(Teen, perHour, wants: Department.DryGoods, arrivals: arrivals,
            reputation: r => r with { Trends = r.Trends with { SeekPermille = 1000, HeartPermille = heartPermille, Count = new IntRange(1, 1) } });

    [Fact]
    public void A_new_game_has_one_or_two_trending_snacks_or_drinks()
    {
        var sim = SimTestKit.NewSim();

        Assert.InRange(sim.TrendingProducts.Count, Rules.Trends.Count.Min, Rules.Trends.Count.Max);
        Assert.All(sim.TrendingProducts, id => Assert.Contains(sim.Data.Products.Single(p => p.Id == id).Department, Rules.Trends.Departments));
        Assert.Equal(sim.TrendingProducts.Count, sim.TrendingProducts.Distinct().Count());
    }

    [Fact]
    public void Same_seed_picks_the_same_trends()
    {
        for (ulong seed = 1; seed <= 5; seed++)
        {
            Assert.Equal(SimTestKit.NewSim(seed).TrendingProducts, SimTestKit.NewSim(seed).TrendingProducts);
        }
    }

    [Fact]
    public void Trend_picks_do_not_change_the_customers_rolls()
    {
        var plain = SimTestKit.NewSim(seed: 3);
        var noTrends = SimTestKit.NewSim(seed: 3, data: SimTestKit.Data with { Reputation = Rules with { Trends = Rules.Trends with { Count = new IntRange(0, 0) } } });

        Assert.Equal(plain.Rng.State, noTrends.Rng.State);
    }

    [Fact]
    public void Trends_only_change_when_a_new_season_starts()
    {
        var sim = SimTestKit.NewSim(seed: 2);
        var first = sim.TrendingProducts.ToList();
        sim.DrainEvents();

        var picked = new List<TrendsPicked>();
        for (var day = 1; day <= 30; day++)
        {
            sim.OpenAndRunToClose();
            sim.Run(new StartNextDay());
            picked.AddRange(sim.DrainEvents().OfType<TrendsPicked>());
            if (sim.Clock.Day <= 28) Assert.Equal(first, sim.TrendingProducts);
        }

        Assert.Equal(1, GameSim.SeasonOf(29));
        var season = Assert.Single(picked);
        Assert.Equal(1, season.Season);
        Assert.Equal(season.ProductIds, sim.TrendingProducts);
    }

    [Fact]
    public void Seasons_are_28_days_until_the_calendar_takes_over()
    {
        Assert.Equal(0, GameSim.SeasonOf(1));
        Assert.Equal(0, GameSim.SeasonOf(28));
        Assert.Equal(1, GameSim.SeasonOf(29));
        Assert.Equal(3, GameSim.SeasonOf(112));
    }

    [Fact]
    public void Teens_who_find_the_trend_are_happier_than_teens_who_do_not()
    {
        var stocked = SimTestKit.NewSim(data: TeensAlwaysSeek());
        var trend = stocked.TrendingProducts[0];
        Shelve(stocked, trend, 200);
        Shelve(stocked, Rice, 200, slot: 1);
        var found = PlayDay(stocked);

        var missing = SimTestKit.NewSim(data: TeensAlwaysSeek());
        Shelve(missing, Rice, 200, slot: 1);
        var missed = PlayDay(missing);

        Assert.NotEmpty(Scores(found));
        Assert.NotEmpty(Scores(missed));
        Assert.All(Scores(found), s => Assert.True(s >= Rules.Satisfaction.Base + Rules.Satisfaction.TrendFound, $"score {s}"));
        Assert.All(Scores(missed), s => Assert.True(s <= Rules.Satisfaction.Base + Rules.Satisfaction.TrendMissed + Rules.Satisfaction.PerNotFound + Rules.Satisfaction.MaxItemBonus, $"score {s}"));
        Assert.True(Scores(found).Average() - Scores(missed).Average() >= Rules.Satisfaction.TrendFound - Rules.Satisfaction.TrendMissed);
    }

    [Fact]
    public void Teens_look_for_the_trend_first_even_when_it_is_not_on_sale()
    {
        var sim = SimTestKit.NewSim(data: TeensAlwaysSeek());
        Shelve(sim, Rice, 200, slot: 1);
        sim.WalkPlayerTo(CashierCell);
        sim.Run(new OpenStore());

        CustomerScenarioTick(sim, () => sim.Customers.Count > 0);
        var teen = sim.Customers[0];

        Assert.Equal(sim.TrendingProducts[0], teen.SeekingTrend);
        var notFound = new List<CustomerReaction>();
        CustomerScenarioTick(sim, () => (notFound = sim.DrainEvents().OfType<CustomerReaction>().Where(r => r.Kind == CustomerReactionKind.NotFound).ToList()).Count > 0);
        Assert.Equal(sim.TrendingProducts[0], notFound[0].ProductId);
        Assert.False(teen.TrendFound);
    }

    [Fact]
    public void A_happy_teen_sometimes_shows_a_heart_and_brings_a_friend_later()
    {
        var open = SimTestKit.Data.Economy.OpenMinuteOfDay;
        var sim = SimTestKit.NewSim(data: TeensAlwaysSeek(heartPermille: 1000, arrivals: [new ArrivalBucket(open, 20), new ArrivalBucket(open + 60, 0)]));
        Shelve(sim, sim.TrendingProducts[0], 200);

        var events = PlayDayTimed(sim);

        var hearts = events.Where(e => e.Event is CustomerReaction { Kind: CustomerReactionKind.Heart }).ToList();
        Assert.NotEmpty(hearts);
        var lateArrivals = events.Where(e => e.Event is CustomerEntered && e.Minute >= open + 60).ToList();
        Assert.NotEmpty(lateArrivals);
        Assert.All(lateArrivals, e => Assert.Equal(Teen, ((CustomerEntered)e.Event).TypeId));
        Assert.True(lateArrivals.Min(e => e.Minute) >= hearts.Min(e => e.Minute) + Rules.Trends.WordOfMouthDelayMinutes);
    }

    [Fact]
    public void Without_hearts_no_friends_come()
    {
        var open = SimTestKit.Data.Economy.OpenMinuteOfDay;
        var sim = SimTestKit.NewSim(data: TeensAlwaysSeek(heartPermille: 0, arrivals: [new ArrivalBucket(open, 20), new ArrivalBucket(open + 60, 0)]));
        Shelve(sim, sim.TrendingProducts[0], 200);

        var events = PlayDayTimed(sim);

        Assert.DoesNotContain(events, e => e.Event is CustomerEntered && e.Minute >= open + 60);
    }

    private static void CustomerScenarioTick(GameSim sim, Func<bool> done) => Customers.CustomerScenario.TickUntil(sim, done);
}
