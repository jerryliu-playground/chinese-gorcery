using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Meat;
using ChineseGrocery.Sim.Staff;
using ChineseGrocery.Sim.Tasks;
using ChineseGrocery.Sim.Time;
using static ChineseGrocery.Sim.Tests.Meat.MeatScenario;

namespace ChineseGrocery.Sim.Tests.Staff;

public class StaffTests
{
    private static readonly RoleDef Butcher = SimTestKit.Data.Employees.Roles.Single(r => r.Id == "butcher");

    private static RejectReason RejectionFor(GameSim sim, SimCommand command)
    {
        sim.DrainEvents();
        sim.Run(command);
        return sim.DrainEvents().Single<CommandRejected>().Reason;
    }

    private static Worker Hire(GameSim sim)
    {
        sim.Run(new HireEmployee("butcher"));
        return sim.Workers.Single(w => w.RoleId == "butcher");
    }

    [Fact]
    public void Butcher_is_locked_before_stage_two()
    {
        Assert.Equal(RejectReason.RoleLocked, RejectionFor(SimTestKit.NewSim(), new HireEmployee("butcher")));
    }

    [Fact]
    public void Hiring_rolls_attributes_within_the_role_ranges_at_market_wage()
    {
        var sim = Stage2(primals: 0);
        sim.DrainEvents();

        var butcher = Hire(sim);

        Assert.False(butcher.IsPlayer);
        Assert.InRange(butcher.KnifeSkill, Butcher.KnifeSkill.Min, Butcher.KnifeSkill.Max);
        Assert.InRange(butcher.WorkSpeedPermille, Butcher.SpeedPermille.Min, Butcher.SpeedPermille.Max);
        Assert.Equal(Butcher.MarketWageCentsPerDay, butcher.WageCentsPerDay);
        Assert.Equal(Butcher.MoraleAtMarketWagePermille, butcher.MoralePermille);
        Assert.Equal((1, 0), (butcher.Level, butcher.Xp));
        var hired = sim.DrainEvents().Single<EmployeeHired>();
        Assert.Equal((butcher.Id, "butcher"), (hired.WorkerId, hired.RoleId));
    }

    [Fact]
    public void Role_limits_and_phase_are_enforced()
    {
        var sim = Stage2(primals: 0);
        Hire(sim);

        Assert.Equal(RejectReason.RoleFull, RejectionFor(sim, new HireEmployee("butcher")));
        Assert.Equal(RejectReason.UnknownRole, RejectionFor(sim, new HireEmployee("cashier")));
        sim.Run(new OpenStore());
        Assert.Equal(RejectReason.WrongPhase, RejectionFor(sim, new SetWage(2, 1)));
    }

    [Theory]
    [InlineData(1000, 700)]
    [InlineData(1200, 900)]
    [InlineData(500, 200)]
    [InlineData(2000, 1000)]
    [InlineData(0, 0)]
    public void Morale_follows_wage_against_the_market(int wageRatioPermille, int expectedMorale)
    {
        var sim = Stage2(primals: 0);
        var butcher = Hire(sim);

        sim.Run(new SetWage(butcher.Id, Butcher.MarketWageCentsPerDay * wageRatioPermille / 1000));

        Assert.Equal(expectedMorale, butcher.MoralePermille);
    }

    [Fact]
    public void Wage_must_not_be_negative_and_worker_must_exist()
    {
        var sim = Stage2(primals: 0);
        var butcher = Hire(sim);

        Assert.Equal(RejectReason.InvalidWage, RejectionFor(sim, new SetWage(butcher.Id, -1)));
        Assert.Equal(RejectReason.UnknownWorker, RejectionFor(sim, new SetWage(99, 100)));
        Assert.Equal(RejectReason.UnknownWorker, RejectionFor(sim, new SetWage(sim.Player.Id, 100)));
    }

    [Fact]
    public void Butcher_cuts_waiting_primals_on_their_own()
    {
        var sim = Stage2(primals: 1);
        var butcher = Hire(sim);
        sim.DrainEvents();

        TickUntil(sim, () => sim.DrainEvents().OfType<MeatCut>().Any(m => m.WorkerId == butcher.Id));

        Assert.Equal(0, sim.Inventory.Count(ColdStorage(sim), Primal));
        Assert.True(sim.Inventory.Count(ColdStorage(sim), "pork_belly") > 0);
        Assert.Equal(WorkStep.Idle, sim.Player.Step);
    }

    [Fact]
    public void Butcher_ignores_tasks_outside_the_role()
    {
        var sim = Stage2(primals: 0);
        sim.Run(new PlaceOrder("rice_jasmine", 10));
        sim.RunFullDay();
        sim.Run(new AssignShelfSlot(sim.InstanceOf("shelf"), 0, "rice_jasmine"));
        var butcher = Hire(sim);

        for (var i = 0; i < 20; i++) sim.Tick();

        Assert.Contains(sim.Tasks.OpenTasks, t => t is RestockTask);
        Assert.Equal(WorkStep.Idle, butcher.Step);
        Assert.Null(butcher.TaskId);
    }

    [Fact]
    public void A_task_the_butcher_claimed_is_not_available_to_the_player()
    {
        var sim = Stage2(primals: 1);
        Hire(sim);
        sim.Tick();
        WalkTo(sim, ColdStorageCell);
        sim.DrainEvents();

        sim.Run(new PlayerInteract());

        Assert.Equal(RejectReason.NothingToDo, sim.DrainEvents().Single<CommandRejected>().Reason);
    }

    [Fact]
    public void Better_morale_means_faster_work()
    {
        int TicksToCut(long wage)
        {
            var sim = Stage2(primals: 1);
            var butcher = Hire(sim);
            sim.Run(new SetWage(butcher.Id, wage));
            TickUntil(sim, () => butcher.Step == WorkStep.Cutting);
            var ticks = 0;
            while (butcher.Step == WorkStep.Cutting)
            {
                sim.Tick();
                ticks++;
            }

            return ticks;
        }

        Assert.True(TicksToCut(Butcher.MarketWageCentsPerDay * 2) < TicksToCut(Butcher.MarketWageCentsPerDay / 2));
    }

    [Fact]
    public void Wages_are_paid_at_closing_even_into_negative_cash()
    {
        var sim = Stage2(primals: 0);
        var butcher = Hire(sim);
        sim.Economy.Spend(ExpenseCategory.Purchases, sim.Economy.CashCents);

        sim.OpenAndRunToClose();

        var report = sim.Reports[^1];
        Assert.Equal(butcher.WageCentsPerDay, report.WagesCents);
        Assert.True(sim.Economy.CashCents < 0);
        Assert.Equal(report.RevenueCents - report.PurchaseCostCents - report.WagesCents - report.FixtureSpendCents, report.CashEndCents - report.CashStartCents);
    }

    [Fact]
    public void Negative_cash_blocks_spending_but_not_hiring()
    {
        var sim = Stage2(primals: 0);
        sim.Economy.Spend(ExpenseCategory.Purchases, sim.Economy.CashCents);
        sim.Economy.Pay(ExpenseCategory.Wages, 1);

        Assert.Equal(RejectReason.InsufficientCash, RejectionFor(sim, new PlaceOrder("rice_jasmine", 1)));
        Assert.Equal(RejectReason.InsufficientCash, RejectionFor(sim, new PlaceFixture("shelf", new GridPoint(7, 3))));
        sim.Run(new HireEmployee("butcher"));
        Assert.Single(sim.Workers, w => w.RoleId == "butcher");
    }

    [Fact]
    public void Experience_from_tasks_levels_the_butcher_up()
    {
        var data = SimTestKit.Data with
        {
            Employees = SimTestKit.Data.Employees with
            {
                Roles = [Butcher with { XpPerTask = 10, XpPerLevel = 10, MaxLevel = 2, LevelUpKnifeSkill = 2, LevelUpSpeedPermille = 100 }],
            },
        };
        var sim = SimTestKit.NewSim(data: data);
        sim.Economy.Earn("rice_jasmine", 1, data.Stages[1].RequiredCumulativeRevenueCents);
        sim.Run(new UpgradeStage());
        sim.Run(new PlaceFixture("cold_storage", new GridPoint(14, 0)));
        sim.Run(new PlaceFixture("cutting_table", new GridPoint(16, 7)));
        sim.Run(new PlaceOrder(Primal, 2));
        sim.RunFullDay();
        var butcher = Hire(sim);
        var (knife, speed) = (butcher.KnifeSkill, butcher.WorkSpeedPermille);
        sim.DrainEvents();

        TickUntil(sim, () => sim.Inventory.Count(ColdStorage(sim), Primal) == 0 && butcher.Step == WorkStep.Idle);

        Assert.Equal(2, butcher.Level);
        Assert.Equal((knife + 2, speed + 100), (butcher.KnifeSkill, butcher.WorkSpeedPermille));
        Assert.Single(sim.DrainEvents().OfType<EmployeeLeveledUp>());
    }
}
