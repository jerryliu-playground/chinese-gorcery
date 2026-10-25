using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Customers;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Reputation;
using ChineseGrocery.Sim.Time;
using static ChineseGrocery.Sim.Tests.Reputation.ReputationScenario;

namespace ChineseGrocery.Sim.Tests.Reputation;

/// <summary>Spec 16.1: satisfaction per customer, the daily moving average, stars, and what reputation changes.</summary>
public class ReputationTests
{
    private const string Family = "chinese_family";

    [Fact]
    public void New_game_starts_at_the_data_reputation()
    {
        var sim = SimTestKit.NewSim();

        Assert.Equal(Rules.StartReputation, sim.Reputation);
        Assert.Equal(Rules.StarMinReputation.Count(m => sim.Reputation >= m), sim.Stars);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(24, 1)]
    [InlineData(25, 2)]
    [InlineData(50, 3)]
    [InlineData(70, 4)]
    [InlineData(100, 5)]
    public void Stars_follow_the_thresholds(int reputation, int stars)
    {
        var sim = SimTestKit.NewSim();

        SetReputation(sim, reputation);

        Assert.Equal(stars, sim.Stars);
    }

    [Fact]
    public void Nudges_are_clamped_and_announced_with_their_reason()
    {
        var sim = SimTestKit.NewSim();
        sim.DrainEvents();

        sim.NudgeReputation(500, "reputation.reason.test");
        sim.NudgeReputation(-30, "reputation.reason.other");

        Assert.Equal(70, sim.Reputation);
        var changes = sim.DrainEvents().OfType<ReputationChanged>().ToList();
        Assert.Equal([(40, 100, "reputation.reason.test"), (100, 70, "reputation.reason.other")], changes.Select(c => (c.Before, c.After, c.ReasonKey)));
    }

    [Fact]
    public void A_customer_who_buys_without_trouble_scores_the_base_plus_the_item_bonus()
    {
        var sim = SimTestKit.NewSim(data: Only(Family, perHour: 6));
        Shelve(sim, Rice, 200);

        var scores = Scores(PlayDay(sim)).ToList();

        Assert.NotEmpty(scores);
        Assert.All(scores, s => Assert.Equal(Rules.Satisfaction.Base + Rules.Satisfaction.PerItemBought, s));
    }

    [Fact]
    public void Not_finding_anything_lowers_the_score()
    {
        var sim = SimTestKit.NewSim(data: Only(Family, perHour: 6));
        Shelve(sim, Rice, 1);

        var events = PlayDay(sim);
        var nothingBought = events.OfType<CustomerSatisfied>()
            .Where(s => events.OfType<CustomerLeft>().Any(l => l.CustomerId == s.CustomerId && l.Exit == CustomerExit.NothingBought))
            .Select(s => s.Score)
            .ToList();

        Assert.NotEmpty(nothingBought);
        Assert.All(nothingBought, s => Assert.Equal(Rules.Satisfaction.Base + Rules.Satisfaction.PerNotFound, s));
    }

    [Fact]
    public void Giving_up_in_line_scores_lowest()
    {
        var sim = SimTestKit.NewSim(data: Only(Family, perHour: 30, patienceMinutes: 2));
        Shelve(sim, Rice, 50);

        var events = PlayDay(sim, serve: false);
        var angry = events.OfType<CustomerLeft>().Where(l => l.Exit == CustomerExit.Angry).Select(l => l.CustomerId).ToHashSet();

        Assert.NotEmpty(angry);
        var expected = Math.Max(0, Rules.Satisfaction.Base + Rules.Satisfaction.GaveUp + Rules.Satisfaction.LongWait);
        Assert.All(events.OfType<CustomerSatisfied>().Where(s => angry.Contains(s.CustomerId)), s => Assert.Equal(expected, s.Score));
    }

    [Fact]
    public void Closing_pulls_reputation_toward_the_days_average_by_the_data_rate()
    {
        var sim = SimTestKit.NewSim(data: Only(Family, perHour: 6));
        Shelve(sim, Rice, 200);
        var before = sim.Reputation;

        var scores = Scores(PlayDay(sim)).ToList();

        var averageMilli = scores.Sum() * 1000L / scores.Count;
        var expectedMilli = before * 1000L + (averageMilli - before * 1000L) * Rules.DailyPullPermille / 1000;
        Assert.Equal((int)(expectedMilli / 1000), sim.Reputation);
        Assert.Equal(new ReputationOfDay(1, sim.Reputation), sim.ReputationHistory.Single());
    }

    [Fact]
    public void Repeated_bad_days_keep_lowering_reputation_and_good_days_raise_it()
    {
        var bad = SimTestKit.NewSim(data: Only(Family, perHour: 6));
        var good = SimTestKit.NewSim(data: Only(Family, perHour: 6));
        for (var day = 0; day < 3; day++)
        {
            PlayDay(bad);
            bad.Run(new StartNextDay());
            Shelve(good, Rice, 200);
            PlayDay(good);
            good.Run(new StartNextDay());
        }

        Assert.True(bad.ReputationHistory.Zip(bad.ReputationHistory.Skip(1)).All(p => p.Second.Reputation <= p.First.Reputation));
        Assert.True(bad.Reputation < Rules.StartReputation);
        Assert.True(good.Reputation > Rules.StartReputation);
    }

    [Fact]
    public void A_day_with_no_customers_leaves_reputation_alone()
    {
        var sim = SimTestKit.NewSim(data: Only(Family, perHour: 0));

        PlayDay(sim);

        Assert.Equal(Rules.StartReputation, sim.Reputation);
    }

    [Fact]
    public void Higher_reputation_brings_more_customers()
    {
        int CustomersAt(int reputation)
        {
            var sim = SimTestKit.NewSim(data: Only(Family, perHour: 20));
            Shelve(sim, Rice, 400);
            SetReputation(sim, reputation);
            PlayDay(sim);
            return sim.Reports[^1].Customers;
        }

        var low = CustomersAt(0);
        var start = CustomersAt(Rules.StartReputation);
        var high = CustomersAt(100);

        Assert.True(low < start && start < high, $"{low} < {start} < {high}");
    }

    [Fact]
    public void Arrival_multiplier_follows_the_data_curve()
    {
        var sim = SimTestKit.NewSim();

        Assert.Equal(1000, sim.ArrivalPermille());
        SetReputation(sim, 100);
        Assert.Equal(Rules.ArrivalPoints[^1].Permille, sim.ArrivalPermille());
        SetReputation(sim, 0);
        Assert.Equal(Rules.ArrivalPoints[0].Permille, sim.ArrivalPermille());
    }

    [Fact]
    public void New_customer_types_only_come_once_the_store_has_their_stars()
    {
        var sim = SimTestKit.NewSim();
        CustomerTypeDef Type(string id) => sim.Data.CustomerTypes.Single(t => t.Id == id);

        SetReputation(sim, 0);
        Assert.Equal(0, sim.ArrivalWeightNow(Type("abc_teen")));
        Assert.True(sim.ArrivalWeightNow(Type("bargain_hunter")) > 0);

        SetReputation(sim, 30);
        Assert.True(sim.ArrivalWeightNow(Type("abc_teen")) > 0);
        Assert.Equal(0, sim.ArrivalWeightNow(Type("local_a")));
        Assert.Equal(0, sim.ArrivalWeightNow(Type("local_b")));

        SetReputation(sim, 55);
        Assert.True(sim.ArrivalWeightNow(Type("local_a")) > 0);
        Assert.True(sim.ArrivalWeightNow(Type("local_b")) > 0);
    }

    [Fact]
    public void The_mix_shifts_toward_the_new_types_with_more_stars()
    {
        var sim = SimTestKit.NewSim();
        var teen = sim.Data.CustomerTypes.Single(t => t.Id == "abc_teen");

        SetReputation(sim, 30);
        var atTwo = sim.ArrivalWeightNow(teen);
        SetReputation(sim, 90);

        Assert.True(sim.ArrivalWeightNow(teen) > atTwo);
    }

    [Fact]
    public void Restaurant_buyers_need_three_stars_and_both_meat_and_seafood()
    {
        var unlocked = Seafood.SeafoodScenario.Unlocked(SimTestKit.Data);
        var sim = SimTestKit.NewSim(data: unlocked with { Fixtures = unlocked.Fixtures.Select(f => f.Kind == FixtureKind.MeatDisplay ? f with { UnlockStage = 1 } : f).ToList() });
        var buyer = sim.Data.CustomerTypes.Single(t => t.Id == "restaurant_buyer");
        SetReputation(sim, 100);
        Assert.Equal(0, sim.ArrivalWeightNow(buyer));

        Seafood.SeafoodScenario.Place(sim, "meat_display", 6, 0);
        Assert.Equal(0, sim.ArrivalWeightNow(buyer));

        Seafood.SeafoodScenario.Tank(sim, 0);
        Assert.True(sim.ArrivalWeightNow(buyer) > 0);

        SetReputation(sim, 40);
        Assert.Equal(0, sim.ArrivalWeightNow(buyer));
    }

    [Fact]
    public void Locals_walk_in_once_the_store_reaches_three_stars()
    {
        var types = SimTestKit.Data.CustomerTypes.Select(t => t.Id).ToList();
        var sim = SimTestKit.NewSim(seed: 4);
        var entered = PlayDay(sim).OfType<CustomerEntered>().Select(e => e.TypeId).ToList();
        sim.Run(new StartNextDay());
        Assert.DoesNotContain("local_a", entered);

        SetReputation(sim, 80);
        var events = PlayDay(sim);

        Assert.Contains(events.OfType<CustomerEntered>(), e => e.TypeId is "local_a" or "local_b");
        Assert.All(events.OfType<CustomerEntered>(), e => Assert.Contains(e.TypeId, types));
    }

    [Fact]
    public void Better_reputation_brings_better_applicants()
    {
        double AverageSpeed(int reputation)
        {
            var speeds = new List<int>();
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var sim = SimTestKit.NewSim(seed);
                SetReputation(sim, reputation);
                for (var day = 0; day < 3; day++)
                {
                    sim.OpenAndRunToClose();
                    sim.Run(new StartNextDay());
                    speeds.AddRange(sim.Applicants.Select(a => a.SpeedPermille));
                    SetReputation(sim, reputation);
                }
            }

            return speeds.Average();
        }

        Assert.True(AverageSpeed(100) > AverageSpeed(Rules.StartReputation));
    }

    [Fact]
    public void Applicants_stay_within_their_role_ranges_at_top_reputation()
    {
        var sim = SimTestKit.NewSim();
        SetReputation(sim, 100);
        sim.OpenAndRunToClose();
        sim.Run(new StartNextDay());

        Assert.All(sim.Applicants, a =>
        {
            var role = sim.Data.Employees.Roles.Single(r => r.Id == a.RoleId);
            Assert.InRange(a.SpeedPermille, role.SpeedPermille.Min, role.SpeedPermille.Max);
            Assert.InRange(a.Skill, role.KnifeSkill.Min, role.KnifeSkill.Max);
        });
    }

    [Fact]
    public void Same_seed_same_reputation_story()
    {
        static string Play()
        {
            var sim = SimTestKit.NewSim(seed: 9, data: SimTestKit.RealData);
            var bot = new SimRunner.PlayerBot(stockTarget: 40);
            for (var d = 0; d < 6; d++) bot.PlayDay(sim);
            return SimSnapshot.Hash(sim) + sim.Reputation + string.Join(",", sim.Reviews.Select(r => r.TemplateId));
        }

        Assert.Equal(Play(), Play());
    }
}
