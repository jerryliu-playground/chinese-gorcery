using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Customers;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Reputation;
using ChineseGrocery.Sim.Time;
using ChineseGrocery.SimRunner;
using static ChineseGrocery.Sim.Tests.Reputation.ReputationScenario;

namespace ChineseGrocery.Sim.Tests.Reputation;

/// <summary>Spec 16.6: reviews at closing, written only from what really happened that day.</summary>
public class ReviewTests
{
    /// <summary>Plays days with the player bot on real data, keeping each day's events.</summary>
    private static List<(GameSim Sim, List<SimEvent> Events)> BotDays(ulong seed, int days)
    {
        var sim = SimTestKit.NewSim(seed, SimTestKit.RealData);
        var bot = new PlayerBot(stockTarget: 30);
        var result = new List<(GameSim, List<SimEvent>)>();
        var events = new List<SimEvent>();
        bot.EventsSeen = events.AddRange;
        for (var d = 0; d < days; d++)
        {
            bot.PlayDay(sim);
            events.AddRange(sim.DrainEvents());
            result.Add((sim, events.ToList()));
            events.Clear();
        }

        return result;
    }

    /// <summary>Whether the day's events show the thing a review says happened.</summary>
    private static bool Happened(Review review, List<SimEvent> day, GameSim sim)
    {
        var reactions = day.OfType<CustomerReaction>().ToList();
        var sold = day.OfType<DailyReportReady>().Single().Report.Sales.Select(s => s.ProductId).ToHashSet();
        var tookIt = reactions.Any(r => r.Kind == CustomerReactionKind.TookItem && r.ProductId == review.ProductId);
        var missedIt = reactions.Any(r => r.Kind == CustomerReactionKind.NotFound && r.ProductId == review.ProductId);
        return review.Fact switch
        {
            ReviewFact.SoldOut => day.OfType<ProductSoldOut>().Any(s => s.ProductId == review.ProductId),
            ReviewFact.NotFound => reactions.Any(r => r.Kind == CustomerReactionKind.NotFound && r.ProductId == review.ProductId),
            ReviewFact.TooExpensive => reactions.Any(r => r.Kind == CustomerReactionKind.TooExpensive && r.ProductId == review.ProductId),
            ReviewFact.FreshBuy or ReviewFact.DeliTreat => sold.Contains(review.ProductId!),
            ReviewFact.GaveUp => day.OfType<CustomerLeft>().Any(l => l.Exit == CustomerExit.Angry),
            ReviewFact.LongQueue => day.OfType<CustomerSatisfied>().Any(s => s.LongestWaitSeconds >= Rules.Reviews.LongQueueMinutes * 60),
            ReviewFact.QuickCheckout => day.OfType<CustomerLeft>().Count(l => l.Exit == CustomerExit.Paid) >= Rules.Reviews.QuickCheckoutMinCustomers
                                        && day.OfType<CustomerLeft>().All(l => l.Exit != CustomerExit.Angry),
            ReviewFact.TrendFound => sim.TrendingProducts.Contains(review.ProductId!) && tookIt,
            ReviewFact.TrendMissed => sim.TrendingProducts.Contains(review.ProductId!) && missedIt,
            ReviewFact.LabelsConfused => reactions.Any(r => r.Kind == CustomerReactionKind.Confused),
            ReviewFact.LabelsLoved => sim.HasBilingualLabels,
            ReviewFact.GroupOrderDelivered => day.OfType<GroupOrderClosed>().Any(c => c.Outcome == GroupOrderOutcome.Delivered),
            ReviewFact.GroupOrderFailed => day.OfType<GroupOrderClosed>().Any(c => c.Outcome == GroupOrderOutcome.Failed),
            _ => false,
        };
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(5UL)]
    public void Every_review_is_about_something_that_happened_that_day(ulong seed)
    {
        var days = BotDays(seed, 8);
        var posted = 0;

        foreach (var (sim, events) in days)
        {
            var reviews = events.OfType<ReviewsPosted>().SelectMany(p => p.Reviews).ToList();
            posted += reviews.Count;
            Assert.All(reviews, r => Assert.True(Happened(r, events, sim), $"day {r.Day}: {r.TemplateId} about {r.ProductId}"));
        }

        Assert.True(posted > 0);
    }

    [Fact]
    public void Two_or_three_reviews_a_day_each_about_something_different()
    {
        foreach (var (_, events) in BotDays(2, 6))
        {
            var reviews = events.OfType<ReviewsPosted>().Single().Reviews;
            Assert.InRange(reviews.Count, Rules.Reviews.PerDay.Min, Rules.Reviews.PerDay.Max);
            Assert.Equal(reviews.Count, reviews.Select(r => r.Fact).Distinct().Count());
        }
    }

    [Fact]
    public void A_day_with_both_good_and_bad_shows_both()
    {
        var sim = SimTestKit.NewSim(data: Only("chinese_family", perHour: 12, listSize: 2, reputation: r => r with { Reviews = r.Reviews with { PerDay = new IntRange(2, 2) } }));
        Shelve(sim, Rice, 30);

        PlayDay(sim);

        var stars = sim.Reviews.Select(r => r.Stars).ToList();
        Assert.Contains(stars, s => s >= 4);
        Assert.Contains(stars, s => s <= 3);
    }

    [Fact]
    public void A_sold_out_shelf_and_missing_goods_show_up_in_reviews_with_the_product()
    {
        var sim = SimTestKit.NewSim(data: Only("chinese_family", perHour: 12, listSize: 1,
            reputation: r => r with { Reviews = r.Reviews with { PerDay = new IntRange(5, 5) } }));
        Shelve(sim, Rice, 3);

        PlayDay(sim);

        Assert.Contains(sim.Reviews, r => r.Fact == ReviewFact.SoldOut && r.ProductId == Rice);
        Assert.Contains(sim.Reviews, r => r.Fact == ReviewFact.NotFound && r.ProductId == Rice);
        Assert.All(sim.Reviews, r => Assert.Equal(1, r.Day));
    }

    [Fact]
    public void No_customers_no_reviews()
    {
        var sim = SimTestKit.NewSim(data: Only("chinese_family", perHour: 0));

        PlayDay(sim);

        Assert.Empty(sim.Reviews);
    }

    [Fact]
    public void Only_the_most_recent_reviews_are_kept()
    {
        var sim = SimTestKit.NewSim(data: Only("chinese_family", perHour: 12, reputation: r => r with { Reviews = r.Reviews with { Keep = 4 } }));
        for (var day = 0; day < 4; day++)
        {
            Shelve(sim, Rice, 5);
            PlayDay(sim);
            sim.Run(new StartNextDay());
        }

        Assert.Equal(4, sim.Reviews.Count);
        Assert.Equal(4, sim.Reviews[^1].Day);
    }

    [Fact]
    public void Every_review_template_has_text_in_both_languages()
    {
        var rows = File.ReadAllLines(RepoPaths.StringsCsv);
        foreach (var template in Rules.Reviews.Templates)
        {
            var key = new Review(1, template.Id, template.Fact, template.Stars, null, null).TextKey;
            var row = rows.SingleOrDefault(r => r.StartsWith(key + ",", StringComparison.Ordinal));
            Assert.True(row is not null, $"missing {key}");
        }

        Assert.All(Enum.GetValues<ReviewFact>(), f => Assert.Contains(Rules.Reviews.Templates, t => t.Fact == f));
    }
}
