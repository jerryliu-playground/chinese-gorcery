using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Inventory;

namespace ChineseGrocery.Sim.Tests.Inventory;

public class ExpiryTests
{
    private const string Milk = "soy_milk";

    private static GameSim SimWithMilkShelfLife(int days) => SimTestKit.NewSim(data: SimTestKit.Data with
    {
        Products = SimTestKit.Data.Products.Select(p => p.Id == Milk ? p with { ShelfLifeDays = days } : p).ToList(),
    });

    private static long MilkCost => SimTestKit.Data.Products.Single(p => p.Id == Milk).CostCents;

    [Fact]
    public void One_day_goods_are_discarded_at_closing_on_arrival_day()
    {
        var sim = SimWithMilkShelfLife(1);
        sim.Run(new PlaceOrder(Milk, 6));
        sim.RunFullDay();
        sim.DrainEvents();

        sim.OpenAndRunToClose();
        var events = sim.DrainEvents();

        Assert.Equal(0, sim.Inventory.Total(Milk));
        Assert.Equal(new ProductExpired(2, Milk, 6, sim.InstanceOf("backroom"), 6 * MilkCost), events.Single<ProductExpired>());
        Assert.Equal(6 * MilkCost, events.Single<DailyReportReady>().Report.LossCents);
    }

    [Fact]
    public void Goods_last_exactly_their_shelf_life()
    {
        var sim = SimWithMilkShelfLife(2);
        sim.Run(new PlaceOrder(Milk, 6));
        sim.RunFullDay();

        sim.RunFullDay();
        Assert.Equal(6, sim.Inventory.Total(Milk));

        sim.OpenAndRunToClose();
        Assert.Equal(0, sim.Inventory.Total(Milk));
        Assert.Equal(6 * MilkCost, sim.Reports[^1].LossCents);
    }

    [Fact]
    public void Loss_is_not_charged_to_cash_again()
    {
        var sim = SimWithMilkShelfLife(1);
        sim.Run(new PlaceOrder(Milk, 6));
        sim.RunFullDay();
        var cashBefore = sim.Economy.CashCents;

        sim.OpenAndRunToClose();

        Assert.Equal(cashBefore, sim.Economy.CashCents);
    }
}
