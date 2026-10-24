using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Customers;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Tasks;
using ChineseGrocery.Sim.Time;
using static ChineseGrocery.Sim.Tests.Customers.CustomerScenario;

namespace ChineseGrocery.Sim.Tests.Customers;

public class CustomerTests
{
    private static readonly long RicePrice = SimTestKit.Data.Products.Single(p => p.Id == Rice).SuggestedPriceCents;

    private static void ServeOne(GameSim sim)
    {
        sim.Run(new MovePlayer(CashierCell));
        sim.Run(new PlayerInteract());
        TickUntil(sim, () => sim.Player.Step == WorkStep.Idle);
    }

    [Fact]
    public void Customers_only_arrive_while_open()
    {
        var sim = SimTestKit.NewSim(data: ScenarioData(perHour: 60));

        for (var i = 0; i < 10; i++) sim.Tick();

        Assert.Empty(sim.Customers);
    }

    [Fact]
    public void Sixty_an_hour_is_one_a_minute()
    {
        var sim = SimTestKit.NewSim(data: ScenarioData(perHour: 60));
        Open(sim);
        sim.DrainEvents();

        for (var i = 0; i < 9; i++) sim.Tick();

        Assert.Equal(10, sim.Economy.Today.Customers);
        Assert.Equal(10, sim.DrainEvents().OfType<CustomerEntered>().Count() + 1);
    }

    [Fact]
    public void Fractional_rates_average_out()
    {
        var sim = SimTestKit.NewSim(data: ScenarioData(perHour: 30));

        sim.OpenAndRunToClose();

        var openMinutes = SimTestKit.Data.Economy.CloseMinuteOfDay - SimTestKit.Data.Economy.OpenMinuteOfDay;
        Assert.InRange(sim.Reports[0].Customers, openMinutes / 2 * 8 / 10, openMinutes / 2 * 12 / 10);
    }

    [Fact]
    public void Arrival_weights_pick_the_type()
    {
        var sim = SimTestKit.NewSim(data: ScenarioData());
        Open(sim);

        for (var i = 0; i < 20; i++) sim.Tick();

        Assert.All(sim.Customers, c => Assert.Equal(SimTestKit.Data.CustomerTypes[0].Id, c.TypeId));
    }

    [Fact]
    public void Customer_with_nothing_to_buy_leaves_counting_not_found()
    {
        var sim = SimTestKit.NewSim(data: ScenarioData(perHour: 60));
        Open(sim);
        sim.DrainEvents();

        TickUntil(sim, () => sim.DrainEvents().OfType<CustomerLeft>().Any(l => l.Exit == CustomerExit.NothingBought));

        Assert.True(sim.Economy.Today.ItemsNotFound >= 1);
        Assert.Equal(0, sim.Economy.Today.AngryLeaves);
    }

    [Fact]
    public void Customer_only_wants_departments_on_the_shelves()
    {
        var sim = SimTestKit.NewSim(data: ScenarioData(wants: Department.Drinks));
        ShelveDirectly(sim, Rice, 4);
        Open(sim);

        for (var i = 0; i < 30; i++) sim.Tick();

        Assert.Equal(4, sim.Inventory.Total(Rice));
    }

    [Fact]
    public void Customer_takes_goods_queues_and_pays_when_served()
    {
        var sim = SimTestKit.NewSim(data: ScenarioData(perHour: 60));
        var slot = ShelveDirectly(sim, Rice, 4);
        Open(sim);
        TickUntil(sim, () => AnyQueued(sim));
        var customer = sim.Customers.First(c => c.State == CustomerState.Queued);
        Assert.Equal(Rice, Assert.Single(customer.Basket).Lot.ProductId);
        Assert.True(sim.Inventory.Count(slot, Rice) < 4);
        Assert.Contains(sim.Tasks.OpenTasks, t => t is CheckoutTask);
        var cashBefore = sim.Economy.CashCents;
        sim.DrainEvents();

        ServeOne(sim);
        var events = sim.DrainEvents();

        Assert.Equal(new CheckoutCompleted(customer.Id, RicePrice, sim.Player.Id), events.Single<CheckoutCompleted>());
        Assert.Equal(cashBefore + RicePrice, sim.Economy.CashCents);
        Assert.Equal(RicePrice, sim.Economy.Today.RevenueCents);
        Assert.Equal(RicePrice, sim.Economy.CumulativeRevenueCents);
        var walkedOut = events.OfType<CustomerLeft>().Any(l => l.CustomerId == customer.Id && l.Exit == CustomerExit.Paid);
        Assert.True(walkedOut || sim.Customers.Single(c => c.Id == customer.Id).State == CustomerState.Leaving);
    }

    [Fact]
    public void One_interaction_serves_one_customer()
    {
        var sim = SimTestKit.NewSim(data: ScenarioData(perHour: 60));
        ShelveDirectly(sim, Rice, 4);
        Open(sim);
        TickUntil(sim, () => sim.Customers.Count(c => c.State == CustomerState.Queued) >= 2);
        sim.DrainEvents();

        ServeOne(sim);

        Assert.Single(sim.DrainEvents().OfType<CheckoutCompleted>());
        Assert.Contains(sim.Customers, c => c.State == CustomerState.Queued);
    }

    [Fact]
    public void Checkout_follows_the_set_price()
    {
        var sim = SimTestKit.NewSim(data: ScenarioData(perHour: 60));
        sim.Run(new SetPrice(Rice, RicePrice - 100));
        ShelveDirectly(sim, Rice, 4);
        Open(sim);
        TickUntil(sim, () => AnyQueued(sim));

        ServeOne(sim);

        Assert.Equal(RicePrice - 100, sim.Economy.Today.RevenueCents);
    }

    [Fact]
    public void Price_above_the_ceiling_is_never_bought()
    {
        var sim = SimTestKit.NewSim(data: ScenarioData(perHour: 60));
        sim.Run(new SetPrice(Rice, RicePrice * 1550 / 1000));
        ShelveDirectly(sim, Rice, 4);
        Open(sim);

        for (var i = 0; i < 60; i++) sim.Tick();

        Assert.Equal(4, sim.Inventory.Total(Rice));
        Assert.True(sim.Economy.Today.ItemsTooExpensive > 0);
    }

    [Fact]
    public void Unserved_customer_runs_out_of_patience_and_puts_goods_back()
    {
        var sim = SimTestKit.NewSim(data: ScenarioData(perHour: 60, patienceMinutes: 5));
        var slot = ShelveDirectly(sim, Rice, 1);
        Open(sim);
        sim.DrainEvents();

        TickUntil(sim, () => sim.DrainEvents().OfType<CustomerLeft>().Any(l => l.Exit == CustomerExit.Angry));

        Assert.Equal(1, sim.Economy.Today.AngryLeaves);
        Assert.Equal(1, sim.Inventory.Count(slot, Rice));
    }

    [Fact]
    public void Store_closes_only_after_the_last_customer_leaves()
    {
        var sim = SimTestKit.NewSim(data: ScenarioData(perHour: 60, patienceMinutes: 1000));
        ShelveDirectly(sim, Rice, 4);
        Open(sim);
        TickUntil(sim, () => sim.Clock.MinuteOfDay >= SimTestKit.Data.Economy.CloseMinuteOfDay);

        Assert.Equal(DayPhase.Open, sim.Clock.Phase);
        var stillInside = sim.Customers.Count;
        Assert.True(stillInside > 0);

        while (sim.Customers.Any(c => c.State == CustomerState.Queued)) ServeOne(sim);
        TickUntil(sim, () => sim.Clock.Phase == DayPhase.Closed);

        Assert.Empty(sim.Customers);
        Assert.Equal(sim.Economy.Today.Customers, sim.Reports[0].Customers);
    }

    [Fact]
    public void No_one_enters_after_closing_time()
    {
        var sim = SimTestKit.NewSim(data: ScenarioData(perHour: 60, patienceMinutes: 1000));
        ShelveDirectly(sim, Rice, 4);
        Open(sim);
        TickUntil(sim, () => sim.Clock.MinuteOfDay >= SimTestKit.Data.Economy.CloseMinuteOfDay);
        var entered = sim.Economy.Today.Customers;

        for (var i = 0; i < 10; i++) sim.Tick();

        Assert.Equal(entered, sim.Economy.Today.Customers);
    }

    [Fact]
    public void Report_lists_units_sold_per_product()
    {
        var sim = SimTestKit.NewSim(data: ScenarioData(perHour: 60));
        ShelveDirectly(sim, Rice, 4);
        Open(sim);
        TickUntil(sim, () => AnyQueued(sim));
        ServeOne(sim);
        TickUntil(sim, () => AnyQueued(sim));
        ServeOne(sim);

        Assert.Equal([new ProductSales(Rice, 2, 2 * RicePrice)], sim.Economy.Today.Sales);
    }
}
