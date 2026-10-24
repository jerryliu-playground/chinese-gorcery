using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Tests.Inventory;

public class OrderingTests
{
    private const string Rice = "rice_jasmine";
    private static readonly ProductDef RiceDef = SimTestKit.Data.Products.Single(p => p.Id == Rice);

    [Fact]
    public void Placing_an_order_pays_immediately()
    {
        var sim = SimTestKit.NewSim();
        sim.DrainEvents();

        sim.Run(new PlaceOrder(Rice, 10));

        Assert.Equal(SimTestKit.Data.Economy.StartingCashCents - 10 * RiceDef.CostCents, sim.Economy.CashCents);
        Assert.Equal(new PendingOrder(Rice, 10, RiceDef.CostCents, 1), Assert.Single(sim.PendingOrders));
        Assert.Equal(new OrderPlaced(1, Rice, 10, 10 * RiceDef.CostCents), sim.DrainEvents().Single<OrderPlaced>());
    }

    [Fact]
    public void Order_arrives_in_the_backroom_before_next_opening_not_same_day()
    {
        var sim = SimTestKit.NewSim();
        sim.Run(new PlaceOrder(Rice, 10));

        sim.OpenAndRunToClose();
        Assert.Equal(0, sim.Inventory.Total(Rice));

        sim.DrainEvents();
        sim.Run(new StartNextDay());

        var backroom = sim.InstanceOf("backroom");
        Assert.Equal(10, sim.Inventory.Get(StockLocation.Storage(backroom)).Count(Rice));
        Assert.Empty(sim.PendingOrders);
        Assert.Equal(new OrderDelivered(2, Rice, 10, StockLocation.Storage(backroom)), sim.DrainEvents().Single<OrderDelivered>());
    }

    [Fact]
    public void Delivered_stock_is_good_through_its_shelf_life_counting_arrival_day()
    {
        var sim = SimTestKit.NewSim();
        sim.Run(new PlaceOrder(Rice, 1));
        sim.RunFullDay();

        var lot = Assert.Single(sim.Inventory.Get(StockLocation.Storage(sim.InstanceOf("backroom"))).Lots);
        Assert.Equal(2 + RiceDef.ShelfLifeDays - 1, lot.BestBeforeDay);
        Assert.Equal(RiceDef.CostCents, lot.UnitCostCents);
    }

    public static TheoryData<string, int, RejectReason> BadOrders => new()
    {
        { "seafood_crab", 1, RejectReason.UnknownProduct },
        { "pork_belly", 1, RejectReason.NotOrderable },
        { "pork_primal", 1, RejectReason.NoStorageForProduct },
        { Rice, 0, RejectReason.InvalidQuantity },
        { Rice, -3, RejectReason.InvalidQuantity },
        { Rice, 1_000_000, RejectReason.InsufficientCash },
    };

    [Theory]
    [MemberData(nameof(BadOrders))]
    public void Invalid_orders_are_rejected_without_side_effects(string productId, int quantity, RejectReason reason)
    {
        var sim = SimTestKit.NewSim();
        sim.DrainEvents();

        sim.Run(new PlaceOrder(productId, quantity));

        Assert.Equal(reason, sim.DrainEvents().Single<CommandRejected>().Reason);
        Assert.Equal(SimTestKit.Data.Economy.StartingCashCents, sim.Economy.CashCents);
        Assert.Empty(sim.PendingOrders);
    }

    private static int SlotsOf(string productId) =>
        SimTestKit.Data.Products.Single(p => p.Id == productId).UnitsPerStorageSlot;

    private static int BackroomSlots => SimTestKit.Data.Fixtures.Single(f => f.Id == "backroom").Capacity;

    private static GameSim RichSim() => SimTestKit.NewSim(data: SimTestKit.Data with
    {
        Economy = SimTestKit.Data.Economy with { StartingCashCents = 100_000_000 },
    });

    [Fact]
    public void Backroom_takes_orders_up_to_its_slot_capacity()
    {
        var sim = RichSim();
        sim.DrainEvents();

        sim.Run(new PlaceOrder(Rice, BackroomSlots * SlotsOf(Rice)));
        Assert.Single(sim.PendingOrders);

        sim.Run(new PlaceOrder(Rice, 1));
        Assert.Equal(RejectReason.StorageFull, sim.DrainEvents().Single<CommandRejected>().Reason);
    }

    [Fact]
    public void Each_product_rounds_up_to_whole_slots()
    {
        var sim = RichSim();
        sim.Run(new PlaceOrder(Rice, (BackroomSlots - 1) * SlotsOf(Rice) + 1));
        sim.DrainEvents();

        // Rice now fills every slot (the last one partly), so even one pack of noodles does not fit.
        sim.Run(new PlaceOrder("noodles_instant", 1));

        Assert.Equal(RejectReason.StorageFull, sim.DrainEvents().Single<CommandRejected>().Reason);
    }

    [Fact]
    public void Delivered_stock_and_pending_orders_both_count_against_capacity()
    {
        var sim = RichSim();
        sim.Run(new PlaceOrder(Rice, (BackroomSlots - 1) * SlotsOf(Rice)));
        sim.RunFullDay();
        sim.DrainEvents();

        sim.Run(new PlaceOrder("noodles_instant", SlotsOf("noodles_instant")));
        sim.Run(new PlaceOrder("soy_milk", 1));

        Assert.Single(sim.PendingOrders);
        Assert.Equal(RejectReason.StorageFull, sim.DrainEvents().Single<CommandRejected>().Reason);
    }

    [Fact]
    public void Orders_are_only_accepted_before_opening()
    {
        var sim = SimTestKit.NewSim();
        sim.Run(new OpenStore());
        sim.DrainEvents();

        sim.Run(new PlaceOrder(Rice, 1));

        Assert.Equal(RejectReason.WrongPhase, sim.DrainEvents().Single<CommandRejected>().Reason);
        Assert.Empty(sim.PendingOrders);
    }

    [Fact]
    public void Order_costing_exactly_all_cash_is_accepted()
    {
        var cash = SimTestKit.Data.Economy.StartingCashCents;
        var data = SimTestKit.Data with
        {
            Products = SimTestKit.Data.Products.Select(p => p.Id == Rice ? p with { CostCents = cash } : p).ToList(),
        };
        var sim = SimTestKit.NewSim(data: data);

        sim.Run(new PlaceOrder(Rice, 1));

        Assert.Equal(0, sim.Economy.CashCents);
        Assert.Single(sim.PendingOrders);
    }

    [Fact]
    public void Goods_route_to_the_first_placed_storage_that_can_hold_them()
    {
        var primal = SimTestKit.Data.Products.Single(p => p.Id == "pork_primal");
        PlacedFixture[] placed =
        [
            new(1, "shelf", new GridPoint(0, 0), 0),
            new(2, "backroom", new GridPoint(0, 0), 0),
            new(3, "cold_storage", new GridPoint(0, 0), 0),
            new(4, "cold_storage", new GridPoint(0, 0), 0),
        ];

        Assert.Equal(3, StorageRouting.FindDestination(SimTestKit.Data, placed, primal)?.InstanceId);
        Assert.Equal(2, StorageRouting.FindDestination(SimTestKit.Data, placed, RiceDef)?.InstanceId);
        Assert.Null(StorageRouting.FindDestination(SimTestKit.Data, placed[..2], primal));
    }
}
