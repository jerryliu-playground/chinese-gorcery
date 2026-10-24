using ChineseGrocery.Sim.Customers;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Tasks;
using static ChineseGrocery.Sim.Tests.Customers.CustomerScenario;

namespace ChineseGrocery.Sim.Tests.Customers;

/// <summary>Standing behind the register serves the queue without pressing anything (playtest feedback, 2026-10-24).</summary>
public class AutoCheckoutTests
{
    [Fact]
    public void Player_behind_the_register_serves_customer_after_customer()
    {
        var sim = SimTestKit.NewSim(data: ScenarioData(perHour: 360, patienceMinutes: 60));
        ShelveDirectly(sim, Rice, 4);
        sim.WalkPlayerTo(CashierCell);
        Open(sim);
        sim.DrainEvents();

        var served = 0;
        TickUntil(sim, () => (served += sim.DrainEvents().OfType<CheckoutCompleted>().Count()) >= 3);

        Assert.True(served >= 3);
    }

    [Fact]
    public void No_serving_from_elsewhere()
    {
        var sim = SimTestKit.NewSim(data: ScenarioData(perHour: 360, patienceMinutes: 60));
        ShelveDirectly(sim, Rice, 4);
        Open(sim);
        TickUntil(sim, () => AnyQueued(sim));

        for (var i = 0; i < 30; i++) sim.Tick();

        Assert.Equal(WorkStep.Idle, sim.Player.Step);
        Assert.Equal(0, sim.Economy.Today.RevenueCents);
    }

    [Fact]
    public void Carrying_player_keeps_carrying_instead_of_serving()
    {
        var sim = SimTestKit.NewSim(data: ScenarioData(perHour: 360, patienceMinutes: 60));
        sim.Run(new PlaceOrder(Rice, 20));
        sim.RunFullDay();
        var shelf = sim.InstanceOf("shelf");
        sim.Run(new AssignShelfSlot(shelf, 0, Rice), new AssignShelfSlot(shelf, 1, "noodles_instant"));
        sim.Inventory.GetOrCreate(new StockLocation(shelf, 1)).Add(new StockLot("noodles_instant", 20, 999, 80));
        sim.WalkPlayerTo(new GridPoint(10, 2));
        sim.Run(new PlayerInteract());
        TickUntil(sim, () => sim.Player.Step == WorkStep.Carrying);
        sim.WalkPlayerTo(CashierCell);
        Open(sim);

        TickUntil(sim, () => AnyQueued(sim));
        for (var i = 0; i < 10; i++) sim.Tick();

        Assert.Equal(WorkStep.Carrying, sim.Player.Step);
    }
}
