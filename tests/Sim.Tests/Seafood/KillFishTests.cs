using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Customers;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Staff;
using ChineseGrocery.Sim.Tasks;
using static ChineseGrocery.Sim.Tests.Customers.CustomerScenario;
using static ChineseGrocery.Sim.Tests.Seafood.SeafoodScenario;

namespace ChineseGrocery.Sim.Tests.Seafood;

/// <summary>Spec 12.4: live fish and lobster are killed to order at the seafood counter while the customer waits.</summary>
public class KillFishTests
{
    private static readonly GridPoint TankCell = new(7, 1);
    private static readonly GridPoint CounterWorkCell = new(0, 3);

    /// <summary>One fish-buying customer type, a tank of fish at (7,0) and a seafood counter at (0,4).</summary>
    private static (GameSim Sim, int Tank, int Counter) Shop(int perHour = 60, int patienceMinutes = 30, string product = Fish)
    {
        var sim = Store(ScenarioData(perHour: perHour, wants: Department.Seafood, patienceMinutes: patienceMinutes));
        var tank = product is Lobster or Crab ? ShellfishTank(sim, 7, product) : Tank(sim, 7, product);
        Stock(sim, tank, 0, product, 3);
        var counter = Counter(sim);
        return (sim, tank, counter);
    }

    [Fact]
    public void A_customer_queues_at_the_counter_and_a_kill_task_appears()
    {
        var (sim, tank, counter) = Shop();
        Open(sim);

        TickUntil(sim, () => sim.Tasks.Tasks.OfType<KillFishTask>().Any());

        var task = sim.Tasks.Tasks.OfType<KillFishTask>().Single();
        var customer = sim.Customers.Single(c => c.Id == task.CustomerId);
        Assert.Equal((counter, tank, Fish), (task.CounterInstanceId, task.Source.FixtureInstanceId, task.ProductId));
        Assert.Equal(CustomerState.AtCounter, customer.State);
        Assert.Equal(counter, customer.CheckoutInstanceId);
    }

    [Fact]
    public void The_player_takes_a_fish_from_the_tank_kills_it_at_the_counter_and_the_customer_pays_for_it()
    {
        var (sim, tank, _) = Shop(perHour: 6);
        Open(sim);
        TickUntil(sim, () => sim.Tasks.OpenTasks.OfType<KillFishTask>().Any());
        var task = sim.Tasks.Tasks.OfType<KillFishTask>().Single();

        sim.WalkPlayerTo(TankCell);
        sim.Run(new PlayerInteract(tank));
        Assert.Equal(sim.Player.Id, sim.Tasks.ClaimedBy(task.Id));
        Assert.False(sim.Tasks.TryClaim(task.Id, 99));
        TickUntil(sim, () => sim.Player.Step == WorkStep.Carrying);
        Assert.Equal(2, sim.Inventory.Count(new StockLocation(tank, 0), Fish));

        sim.WalkPlayerTo(CounterWorkCell);
        sim.Run(new PlayerInteract());
        Assert.Equal(WorkStep.Killing, sim.Player.Step);
        TickUntil(sim, () => sim.Player.Step == WorkStep.Idle);

        var customer = sim.Customers.Single(c => c.Id == task.CustomerId);
        Assert.Equal(Fish, Assert.Single(customer.Basket).Lot.ProductId);

        sim.WalkPlayerTo(CashierCell);
        TickUntil(sim, () => sim.Customers.All(c => c.Id != task.CustomerId));
        Assert.Equal(1, sim.Economy.Today.Sales.Single(s => s.ProductId == Fish).Units);
    }

    [Fact]
    public void Lobster_is_killed_to_order_too()
    {
        var (sim, _, _) = Shop(product: Lobster);
        Open(sim);

        TickUntil(sim, () => sim.Tasks.Tasks.OfType<KillFishTask>().Any());

        Assert.Equal(Lobster, sim.Tasks.Tasks.OfType<KillFishTask>().Single().ProductId);
    }

    [Fact]
    public void Customers_never_help_themselves_to_fish_from_the_tank()
    {
        var (sim, tank, _) = Shop();
        Open(sim);

        for (var i = 0; i < 2000; i++) sim.Tick();

        Assert.Equal(3, sim.Inventory.Count(new StockLocation(tank, 0), Fish));
    }

    [Fact]
    public void A_customer_left_waiting_too_long_at_the_counter_leaves_unhappy()
    {
        var (sim, _, _) = Shop(perHour: 6, patienceMinutes: 5);
        Open(sim);
        TickUntil(sim, () => sim.Customers.Any(c => c.State == CustomerState.AtCounter));
        var waiting = sim.Customers.First(c => c.State == CustomerState.AtCounter).Id;
        sim.DrainEvents();

        TickUntil(sim, () => sim.Customers.All(c => c.Id != waiting || c.State == CustomerState.Leaving));

        Assert.Contains(sim.DrainEvents().OfType<CustomerReaction>(), r => r.CustomerId == waiting && r.Kind == CustomerReactionKind.GaveUp);
        Assert.True(sim.Economy.Today.AngryLeaves >= 1);
        Assert.DoesNotContain(sim.Tasks.Tasks.OfType<KillFishTask>(), t => t.CustomerId == waiting);
    }

    [Fact]
    public void Nobody_queues_for_fish_when_there_is_no_counter()
    {
        var sim = Store(ScenarioData(perHour: 60, wants: Department.Seafood));
        var tank = Tank(sim, 7, Fish);
        Stock(sim, tank, 0, Fish, 3);
        Open(sim);

        TickUntil(sim, () => sim.Economy.Today.ItemsNotFound > 0);

        Assert.DoesNotContain(sim.Customers, c => c.State is CustomerState.ToCounter or CustomerState.AtCounter);
    }

    [Fact]
    public void The_fishmonger_kills_to_order_on_their_own()
    {
        var (sim, _, _) = Shop(perHour: 30);
        sim.Run(new HireEmployee("fishmonger"));
        sim.WalkPlayerTo(CashierCell);
        Open(sim);

        TickUntil(sim, () => sim.Economy.Today.Sales.Any(s => s.ProductId == Fish));
    }

    [Fact]
    public void A_better_knife_kills_faster()
    {
        var sim = SimTestKit.NewSim();
        Worker Hand(int knife) => new(50 + knife, isPlayer: false, new GridPoint(0, 0), 3, 1000, knife);

        Assert.True(sim.KillTicks(Hand(5)) < sim.KillTicks(Hand(1)));
    }

    [Fact]
    public void A_killed_fish_that_is_not_paid_for_goes_on_as_chilled_fish()
    {
        var (sim, tank, _) = Shop(perHour: 6, patienceMinutes: 5);
        var cold = Place(sim, "cold_storage", 9, 4);
        Open(sim);
        TickUntil(sim, () => sim.Tasks.OpenTasks.OfType<KillFishTask>().Any());
        var buyer = sim.Tasks.Tasks.OfType<KillFishTask>().Single().CustomerId;
        sim.WalkPlayerTo(TankCell);
        sim.Run(new PlayerInteract(tank));
        TickUntil(sim, () => sim.Player.Step == WorkStep.Carrying);
        sim.WalkPlayerTo(CounterWorkCell);
        sim.Run(new PlayerInteract());
        TickUntil(sim, () => sim.Player.Step == WorkStep.Idle);

        // Nobody is at the checkout, so the buyer gives up there.
        TickUntil(sim, () => sim.Customers.All(c => c.Id != buyer));

        Assert.Equal(1, sim.Inventory.Count(StockLocation.Storage(cold), ChilledFish));
        Assert.Equal(0, sim.Inventory.Count(StockLocation.Storage(sim.InstanceOf("backroom")), Fish));
    }

    [Fact]
    public void Lobster_is_rarely_on_a_shopping_list()
    {
        var sim = Store(SimTestKit.Data);
        var tank = Tank(sim, 7, Fish);
        var shellfish = ShellfishTank(sim, 8, Lobster);
        Stock(sim, tank, 0, Fish, 3);
        Stock(sim, shellfish, 0, Lobster, 3);
        var family = sim.Data.CustomerTypes.Single(t => t.Id == "chinese_family");

        var picks = Enumerable.Range(0, 300).SelectMany(_ => sim.BuildShoppingList(family)).ToList();

        Assert.True(picks.Count(p => p == Lobster) * 5 < picks.Count(p => p == Fish), $"lobster {picks.Count(p => p == Lobster)}, fish {picks.Count(p => p == Fish)}");
    }
}
