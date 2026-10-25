using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Customers;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Deli;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Staff;
using ChineseGrocery.Sim.Tasks;
using static ChineseGrocery.Sim.Tests.Customers.CustomerScenario;
using static ChineseGrocery.Sim.Tests.Deli.DeliScenario;

namespace ChineseGrocery.Sim.Tests.Deli;

/// <summary>Spec 13.6: takeaway service at the deli stations, the mealtime rush, and leftovers.</summary>
public class ServiceTests
{
    private static readonly GridPoint SteamTableWorkCell = new(8, 4);

    /// <summary>Hot-food customers only, with braised pork on the steam table.</summary>
    private static (GameSim Sim, int Table) Lunch(int perHour = 30, int patienceMinutes = 30, int portions = 12)
    {
        var sim = DeliStore(ScenarioData(perHour: perHour, wants: Department.Deli, patienceMinutes: patienceMinutes));
        var table = SteamTable(sim);
        sim.Run(new SetMenuItem(table, 0, "braised_pork", 0));
        if (portions > 0) Put(sim, new StockLocation(table, 0), "braised_pork", portions);
        return (sim, table);
    }

    [Fact]
    public void A_customer_queues_at_the_station_and_a_serve_task_appears()
    {
        var (sim, table) = Lunch();
        Open(sim);

        TickUntil(sim, () => sim.Tasks.Tasks.OfType<ServeDeliTask>().Any());

        var task = sim.Tasks.Tasks.OfType<ServeDeliTask>().Single();
        var customer = sim.Customers.Single(c => c.Id == task.CustomerId);
        Assert.Equal((table, "braised_pork"), (task.StationInstanceId, task.DishId));
        Assert.Equal(CustomerState.AtCounter, customer.State);
        Assert.Equal(table, customer.CheckoutInstanceId);
    }

    [Fact]
    public void The_player_packs_a_portion_and_the_customer_pays_for_it_at_the_till()
    {
        var (sim, table) = Lunch(perHour: 6);
        Open(sim);
        TickUntil(sim, () => sim.Tasks.OpenTasks.OfType<ServeDeliTask>().Any());
        var task = sim.Tasks.Tasks.OfType<ServeDeliTask>().Single();

        sim.WalkPlayerTo(SteamTableWorkCell);
        sim.Run(new PlayerInteract(table));
        Assert.Equal(WorkStep.Packing, sim.Player.Step);
        Assert.False(sim.Tasks.TryClaim(task.Id, 99));
        TickUntil(sim, () => sim.Player.Step == WorkStep.Idle);

        var customer = sim.Customers.Single(c => c.Id == task.CustomerId);
        Assert.Equal("braised_pork", Assert.Single(customer.Basket).Lot.ProductId);
        Assert.Equal(11, sim.Inventory.Count(new StockLocation(table, 0), "braised_pork"));

        sim.WalkPlayerTo(CashierCell);
        TickUntil(sim, () => sim.Customers.All(c => c.Id != task.CustomerId));
        var sale = sim.Economy.Today.Sales.Single(s => s.ProductId == "braised_pork");
        Assert.Equal((1, sim.Prices.PriceOf("braised_pork")), (sale.Units, sale.RevenueCents));
    }

    [Fact]
    public void A_customer_left_waiting_too_long_leaves_unhappy_and_their_task_goes()
    {
        var (sim, _) = Lunch(perHour: 6, patienceMinutes: 5);
        Open(sim);
        TickUntil(sim, () => sim.Customers.Any(c => c.State == CustomerState.AtCounter));
        var waiting = sim.Customers.First(c => c.State == CustomerState.AtCounter).Id;
        sim.DrainEvents();

        TickUntil(sim, () => sim.Customers.All(c => c.Id != waiting || c.State == CustomerState.Leaving));

        Assert.Contains(sim.DrainEvents().OfType<CustomerReaction>(), r => r.CustomerId == waiting && r.Kind == CustomerReactionKind.GaveUp);
        Assert.DoesNotContain(sim.Tasks.Tasks.OfType<ServeDeliTask>(), t => t.CustomerId == waiting);
    }

    [Fact]
    public void Customers_never_help_themselves_from_a_station()
    {
        var (sim, table) = Lunch();
        Open(sim);

        for (var i = 0; i < 1000; i++) sim.Tick();

        Assert.Equal(12, sim.Inventory.Count(new StockLocation(table, 0), "braised_pork"));
    }

    [Fact]
    public void A_dish_a_customer_gives_up_on_goes_back_to_its_station()
    {
        var (sim, table) = Lunch(perHour: 6, patienceMinutes: 5);
        Open(sim);
        TickUntil(sim, () => sim.Tasks.OpenTasks.OfType<ServeDeliTask>().Any());
        var buyer = sim.Tasks.Tasks.OfType<ServeDeliTask>().Single().CustomerId;
        sim.WalkPlayerTo(SteamTableWorkCell);
        sim.Run(new PlayerInteract(table));
        TickUntil(sim, () => sim.Player.Step == WorkStep.Idle);

        // Nobody at the till: the buyer gives up there and the portion goes back.
        TickUntil(sim, () => sim.Customers.All(c => c.Id != buyer));

        Assert.True(sim.Inventory.Count(new StockLocation(table, 0), "braised_pork") >= 11);
        Assert.Equal(0, sim.Economy.Today.DeliLossCents);
    }

    [Fact]
    public void The_chef_packs_on_their_own()
    {
        var (sim, _) = Lunch(perHour: 30);
        sim.Run(new HireEmployee("chef"));
        sim.WalkPlayerTo(CashierCell);
        Open(sim);

        TickUntil(sim, () => sim.Economy.Today.Sales.Any(s => s.ProductId == "braised_pork"));
    }

    [Fact]
    public void A_skilled_cooks_dish_is_charged_at_its_higher_price()
    {
        var (sim, table) = Lunch(perHour: 6, portions: 0);
        sim.Inventory.GetOrCreate(new StockLocation(table, 0)).Add(new StockLot("braised_pork", 1, sim.Clock.Day, 100, PriceBonusPermille: 200));
        Open(sim);
        TickUntil(sim, () => sim.Tasks.OpenTasks.OfType<ServeDeliTask>().Any());
        sim.WalkPlayerTo(SteamTableWorkCell);
        sim.Run(new PlayerInteract(table));
        TickUntil(sim, () => sim.Player.Step == WorkStep.Idle);
        sim.WalkPlayerTo(CashierCell);

        TickUntil(sim, () => sim.Economy.Today.Sales.Any(s => s.ProductId == "braised_pork"));

        Assert.Equal(sim.Prices.PriceOf("braised_pork") * 1200 / 1000, sim.Economy.Today.Sales.Single(s => s.ProductId == "braised_pork").RevenueCents);
    }

    /// <summary>No regular customers at all; only the mealtime rush brings anyone in.</summary>
    private static GameData RushOnly(GameData data, int perHour) => data with
    {
        Economy = data.Economy with
        {
            CustomerArrivals = [new ArrivalBucket(data.Economy.OpenMinuteOfDay, 0)],
            Deli = new DeliSettingsDef([new RushBucket(data.Economy.OpenMinuteOfDay, perHour, [new TypeWeight(data.CustomerTypes[0].Id, 1)])]),
        },
    };

    [Fact]
    public void Mealtime_crowds_come_for_hot_food_only_while_a_station_has_some()
    {
        var sim = DeliStore(RushOnly(SimTestKit.Data, 60));
        var table = SteamTable(sim);
        sim.Run(new SetMenuItem(table, 0, "braised_pork", 0));
        Open(sim);
        for (var i = 0; i < 360; i++) sim.Tick();
        Assert.Equal(0, sim.Economy.Today.Customers);

        Put(sim, new StockLocation(table, 0), "braised_pork", 12);
        TickUntil(sim, () => sim.Economy.Today.Customers > 0);

        var rushCustomer = sim.Customers.First();
        Assert.All(rushCustomer.ShoppingList.Append(rushCustomer.WaitingFor ?? "braised_pork"), id => Assert.Equal(Department.Deli, sim.Data.Products.Single(p => p.Id == id).Department));
    }

    [Fact]
    public void The_shipped_rush_is_busier_at_lunch_and_dinner()
    {
        var rush = SimTestKit.Data.Economy.Deli!.RushArrivals;
        int RateAt(int minute) => rush.Last(b => b.StartMinuteOfDay <= minute).CustomersPerHour;

        Assert.True(RateAt(12 * 60) > RateAt(10 * 60));
        Assert.True(RateAt(18 * 60) > RateAt(15 * 60));
    }
}
