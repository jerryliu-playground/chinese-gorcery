using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Tests.Economy;

public class StageUpgradeTests
{
    private static readonly long Target = SimTestKit.Data.Stages[1].RequiredCumulativeRevenueCents;
    private static readonly long Cost = SimTestKit.Data.Stages[1].UpgradeCostCents;

    /// <summary>A sim that has already earned <paramref name="revenue"/> (test shortcut through the ledger).</summary>
    private static GameSim WithRevenue(long revenue)
    {
        var sim = SimTestKit.NewSim();
        if (revenue > 0) sim.Economy.Earn("rice_jasmine", 1, revenue);
        sim.DrainEvents();
        return sim;
    }

    private static RejectReason RejectionFor(GameSim sim, SimCommand command)
    {
        sim.DrainEvents();
        sim.Run(command);
        return sim.DrainEvents().Single<CommandRejected>().Reason;
    }

    [Fact]
    public void Upgrade_needs_the_revenue_target()
    {
        var sim = WithRevenue(Target - 1);

        Assert.Equal(RejectReason.NotEnoughRevenue, RejectionFor(sim, new UpgradeStage()));
        Assert.Equal(1, sim.Economy.Stage);
    }

    [Fact]
    public void Upgrade_needs_cash_for_the_cost()
    {
        var sim = WithRevenue(Target);
        sim.Economy.Spend(ExpenseCategory.Purchases, sim.Economy.CashCents - Cost + 1);

        Assert.True(sim.Economy.CashCents < Cost);
        Assert.Equal(RejectReason.InsufficientCash, RejectionFor(sim, new UpgradeStage()));
    }

    [Fact]
    public void Upgrade_only_before_opening()
    {
        var sim = WithRevenue(Target);
        sim.Run(new OpenStore());

        Assert.Equal(RejectReason.WrongPhase, RejectionFor(sim, new UpgradeStage()));
    }

    [Fact]
    public void Upgrading_pays_the_cost_and_grows_the_store_around_the_existing_layout()
    {
        var sim = WithRevenue(Target);
        var cash = sim.Economy.CashCents;
        var layout = sim.Layout.Fixtures.ToList();

        sim.Run(new UpgradeStage());

        Assert.Equal(2, sim.Economy.Stage);
        Assert.Equal(cash - Cost, sim.Economy.CashCents);
        Assert.Equal(Cost, sim.Economy.Today.FixtureSpendCents);
        Assert.Equal(SimTestKit.Data.Stages[1].StoreSize, sim.Layout.Size);
        Assert.Equal(layout, sim.Layout.Fixtures);
        Assert.Equal(new StageUpgraded(2, Cost), sim.DrainEvents().Single<StageUpgraded>());
    }

    [Fact]
    public void Last_stage_cannot_upgrade()
    {
        var stages = SimTestKit.Data.Stages;
        var sim = WithRevenue(stages[^1].RequiredCumulativeRevenueCents);
        sim.Economy.Earn("rice_jasmine", 1, stages.Sum(s => s.UpgradeCostCents));
        for (var i = 1; i < stages.Count; i++) sim.Run(new UpgradeStage());
        Assert.Equal(stages.Count, sim.Economy.Stage);

        Assert.Equal(RejectReason.MaxStage, RejectionFor(sim, new UpgradeStage()));
    }

    [Fact]
    public void Stage_two_unlocks_meat_fixtures_and_primal_orders()
    {
        var sim = WithRevenue(Target);
        sim.Run(new UpgradeStage());
        sim.DrainEvents();

        sim.Run(new PlaceFixture("cold_storage", new GridPoint(14, 0)));
        sim.Run(new PlaceFixture("meat_display", new GridPoint(14, 4)));
        sim.Run(new PlaceFixture("cutting_table", new GridPoint(14, 7)));
        sim.Run(new PlaceOrder("pork_primal", 2));

        Assert.Empty(sim.DrainEvents().OfType<CommandRejected>());
        Assert.Single(sim.PendingOrders);
    }
}
