using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Customers;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Reputation;
using static ChineseGrocery.Sim.Tests.Reputation.ReputationScenario;

namespace ChineseGrocery.Sim.Tests.Reputation;

/// <summary>Spec 16.4: the bilingual price labels upgrade and local customers.</summary>
public class BilingualLabelTests
{
    private const string Local = "local_a";

    /// <summary>Locals only; prices set so they buy about half the time; every puzzled roll comes up.</summary>
    private static GameSim LocalStore(bool labels, int confusedPermille = 1000)
    {
        var sim = SimTestKit.NewSim(data: Only(Local, perHour: 10, listSize: 2,
            reputation: r => r with { BilingualLabels = r.BilingualLabels with { ConfusedPermille = confusedPermille } }));
        Shelve(sim, Rice, 400);
        var reference = sim.Prices.ReferenceOf(Rice);
        sim.Run(new SetPrice(Rice, reference * 1250 / 1000));
        if (labels) sim.Run(new BuyBilingualLabels());
        return sim;
    }

    [Fact]
    public void Labels_are_bought_once_with_cash()
    {
        var sim = SimTestKit.NewSim();
        var cash = sim.Economy.CashCents;
        sim.DrainEvents();

        sim.Run(new BuyBilingualLabels());
        sim.Run(new BuyBilingualLabels());

        Assert.True(sim.HasBilingualLabels);
        Assert.Equal(cash - Rules.BilingualLabels.CostCents, sim.Economy.CashCents);
        var events = sim.DrainEvents();
        Assert.Single(events.OfType<BilingualLabelsBought>());
        Assert.Equal(RejectReason.AlreadyOwned, events.Single<CommandRejected>().Reason);
    }

    [Fact]
    public void Labels_need_the_cash()
    {
        var sim = SimTestKit.NewSim(data: SimTestKit.Data with { Economy = SimTestKit.Data.Economy with { StartingCashCents = Rules.BilingualLabels.CostCents - 1 } });

        sim.Run(new BuyBilingualLabels());

        Assert.False(sim.HasBilingualLabels);
        Assert.Equal(RejectReason.InsufficientCash, sim.DrainEvents().Single<CommandRejected>().Reason);
    }

    [Fact]
    public void Without_labels_locals_sometimes_show_a_question_mark()
    {
        var sim = LocalStore(labels: false);

        var events = PlayDay(sim);

        var customers = events.OfType<CustomerEntered>().Count();
        var puzzled = Reactions(events, CustomerReactionKind.Confused).ToList();
        Assert.Equal(customers, puzzled.Count);
        Assert.Equal(customers, puzzled.Select(p => p.CustomerId).Distinct().Count());
    }

    [Fact]
    public void Other_customers_are_never_puzzled_by_labels()
    {
        var sim = SimTestKit.NewSim(data: Only("chinese_family", perHour: 10,
            reputation: r => r with { BilingualLabels = r.BilingualLabels with { ConfusedPermille = 1000 } }));
        Shelve(sim, Rice, 100);

        Assert.Empty(Reactions(PlayDay(sim), CustomerReactionKind.Confused));
    }

    [Fact]
    public void With_labels_locals_are_more_satisfied_and_buy_more()
    {
        var without = LocalStore(labels: false);
        var with = LocalStore(labels: true);

        var before = PlayDay(without);
        var after = PlayDay(with);

        Assert.Empty(Reactions(after, CustomerReactionKind.Confused));
        Assert.True(Scores(after).Average() > Scores(before).Average() + Rules.Satisfaction.LabelsOwned, $"{Scores(after).Average()} vs {Scores(before).Average()}");
        var unitsBefore = without.Reports[^1].Sales.Sum(s => s.Units) / (double)without.Reports[^1].Customers;
        var unitsAfter = with.Reports[^1].Sales.Sum(s => s.Units) / (double)with.Reports[^1].Customers;
        Assert.True(unitsAfter > unitsBefore, $"{unitsAfter} vs {unitsBefore} units per customer");
    }
}
