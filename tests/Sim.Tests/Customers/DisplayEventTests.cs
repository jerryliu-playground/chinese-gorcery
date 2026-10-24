using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Customers;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Meat;
using ChineseGrocery.Sim.Staff;
using ChineseGrocery.Sim.Tasks;
using static ChineseGrocery.Sim.Tests.Customers.CustomerScenario;

namespace ChineseGrocery.Sim.Tests.Customers;

/// <summary>Display-only events for M7 feedback: they report what happened and change nothing.</summary>
public class DisplayEventTests
{
    private static List<SimEvent> Collect(GameSim sim, int ticks)
    {
        var all = new List<SimEvent>();
        for (var i = 0; i < ticks; i++)
        {
            sim.Tick();
            all.AddRange(sim.DrainEvents());
        }

        return all;
    }

    [Fact]
    public void Customers_report_taking_items_and_giving_up()
    {
        var sim = SimTestKit.NewSim(data: ScenarioData(perHour: 360, patienceMinutes: 3));
        ShelveDirectly(sim, Rice, 4);
        Open(sim);

        var events = Collect(sim, 300).OfType<CustomerReaction>().ToList();

        Assert.Contains(events, e => e.Kind == CustomerReactionKind.TookItem && e.ProductId == Rice);
        Assert.Contains(events, e => e.Kind == CustomerReactionKind.GaveUp);
        Assert.Contains(events, e => e.Kind == CustomerReactionKind.NotFound);
    }

    [Fact]
    public void Customers_report_prices_too_high()
    {
        var sim = SimTestKit.NewSim(data: ScenarioData(perHour: 360));
        sim.Run(new SetPrice(Rice, 999_99));
        ShelveDirectly(sim, Rice, 4);
        Open(sim);

        Assert.Contains(Collect(sim, 200).OfType<CustomerReaction>(), e => e.Kind == CustomerReactionKind.TooExpensive && e.ProductId == Rice);
    }

    [Fact]
    public void Shelving_reports_the_slot_and_whether_it_was_empty()
    {
        var sim = SimTestKit.NewSim();
        sim.Run(new PlaceOrder(Rice, 20));
        sim.RunFullDay();
        var slot = new StockLocation(sim.InstanceOf("shelf"), 0);
        sim.Run(new AssignShelfSlot(slot.FixtureInstanceId, 0, Rice));
        sim.DrainEvents();

        sim.Run(new RestockAll());

        var perShelf = SimTestKit.Data.Products.Single(p => p.Id == Rice).UnitsPerShelfSlot;
        Assert.Equal(new ItemsShelved(slot, Rice, perShelf, WasEmpty: true), sim.DrainEvents().Single<ItemsShelved>());
    }

    [Fact]
    public void Checkout_and_cutting_say_where_they_happened()
    {
        var sim = SimTestKit.NewSim(data: ScenarioData(perHour: 360, patienceMinutes: 60));
        ShelveDirectly(sim, Rice, 4);
        sim.WalkPlayerTo(CashierCell);
        Open(sim);

        var paid = Collect(sim, 300).OfType<CheckoutCompleted>().First();
        Assert.Equal(sim.InstanceOf("checkout"), paid.CheckoutInstanceId);

        var meat = Meat.MeatScenario.Stage2(primals: 1);
        meat.Run(new HireEmployee("butcher"));
        var cut = Collect(meat, 3000).OfType<MeatCut>().First();
        Assert.Equal(meat.InstanceOf("cutting_table"), cut.CuttingTableInstanceId);
    }
}
