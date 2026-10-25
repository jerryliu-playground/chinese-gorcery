using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Imports;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Save;
using ChineseGrocery.Sim.Staff;
using ChineseGrocery.Sim.Tasks;
using static ChineseGrocery.Sim.Tests.Customers.CustomerScenario;

namespace ChineseGrocery.Sim.Tests.Imports;

/// <summary>Spec 17: importing by sea, pallets and unloading, storage room and overflow at the door.</summary>
public class ImportTests
{
    private const string Doubanjiang = "doubanjiang";
    private static ImportSettingsDef Settings => SimTestKit.Data.Economy.Imports!;

    /// <summary>Imports unlocked from stage 1 (data shortcut) with plenty of cash.</summary>
    private static GameSim Store(ulong seed = 1) => SimTestKit.NewSim(seed, SimTestKit.Data with
    {
        Economy = SimTestKit.Data.Economy with { StartingCashCents = 1_000_000, Imports = Settings with { UnlockStage = 1 } },
    });

    private static ProductDef Def(GameSim sim, string id) => sim.Data.Products.Single(p => p.Id == id);

    private static RejectReason? Send(GameSim sim, SimCommand command)
    {
        sim.DrainEvents();
        sim.Run(command);
        return sim.DrainEvents().OfType<CommandRejected>().SingleOrDefault()?.Reason;
    }

    [Fact]
    public void Import_goods_are_cheaper_and_only_some_can_be_bought_locally()
    {
        var doubanjiang = Def(SimTestKit.NewSim(), Doubanjiang);
        var rice = Def(SimTestKit.NewSim(), "rice_jasmine");

        Assert.False(doubanjiang.Orderable);
        Assert.NotNull(doubanjiang.Import);
        Assert.True(rice.Import!.CostCents < rice.CostCents);
    }

    [Fact]
    public void An_import_order_is_paid_now_and_arrives_on_a_pallet_within_the_lead_time()
    {
        var sim = Store();
        var cash = sim.Economy.CashCents;
        var units = Def(sim, Doubanjiang).Import!.MinUnits;

        Assert.Null(Send(sim, new PlaceImportOrder(Doubanjiang, units)));

        var shipment = Assert.Single(sim.Shipments);
        Assert.Equal(cash - units * Def(sim, Doubanjiang).Import!.CostCents, sim.Economy.CashCents);
        Assert.InRange(shipment.ArrivalDay - sim.Clock.Day, Settings.MinDays, Settings.MaxDays);

        while (sim.Clock.Day < shipment.ArrivalDay - 1) sim.RunFullDay();
        Assert.Empty(sim.Pallets);
        sim.RunFullDay();

        Assert.Empty(sim.Shipments);
        var pallet = Assert.Single(sim.Pallets);
        Assert.Equal(units, pallet.Lots.Sum(l => l.Quantity));
        Assert.Equal(0, sim.Inventory.Total(Doubanjiang));
    }

    [Fact]
    public void Import_orders_need_the_minimum_quantity_cash_and_the_unlock_stage()
    {
        var sim = Store();
        var min = Def(sim, Doubanjiang).Import!.MinUnits;

        Assert.Equal(RejectReason.BelowMinimumOrder, Send(sim, new PlaceImportOrder(Doubanjiang, min - 1)));
        Assert.Equal(RejectReason.NotImportable, Send(sim, new PlaceImportOrder("pork_primal", 10)));
        Assert.Equal(RejectReason.FixtureLocked, Send(SimTestKit.NewSim(), new PlaceImportOrder(Doubanjiang, min)));

        sim.Economy.CashCents = 0;
        Assert.Equal(RejectReason.InsufficientCash, Send(sim, new PlaceImportOrder(Doubanjiang, min)));
    }

    [Fact]
    public void Same_seed_same_sailing_times()
    {
        int Arrival(ulong seed)
        {
            var sim = Store(seed);
            sim.Run(new PlaceImportOrder(Doubanjiang, Def(sim, Doubanjiang).Import!.MinUnits));
            return sim.Shipments.Single().ArrivalDay;
        }

        Assert.Equal(Arrival(3), Arrival(3));
    }

    [Fact]
    public void A_delayed_ship_arrives_later()
    {
        var sim = Store();
        sim.Run(new PlaceImportOrder(Doubanjiang, Def(sim, Doubanjiang).Import!.MinUnits));
        var before = sim.Shipments.Single().ArrivalDay;

        sim.DelayShipments(3);

        Assert.Equal(before + 3, sim.Shipments.Single().ArrivalDay);
    }

    /// <summary>A pallet of doubanjiang waiting in the backroom, with a shelf slot assigned to it.</summary>
    private static (GameSim Sim, StockLocation Slot) PalletWaiting()
    {
        var sim = Store();
        var slot = new StockLocation(sim.InstanceOf("shelf"), 0);
        sim.Run(new AssignShelfSlot(slot.FixtureInstanceId, 0, Doubanjiang));
        sim.Run(new PlaceImportOrder(Doubanjiang, Def(sim, Doubanjiang).Import!.MinUnits));
        while (sim.Pallets.Count == 0) sim.RunFullDay();
        return (sim, slot);
    }

    [Fact]
    public void Goods_on_a_pallet_cannot_be_shelved_until_unloaded()
    {
        var (sim, slot) = PalletWaiting();

        Assert.Equal(RejectReason.NothingToDo, Send(sim, new RestockAll()));
        Assert.DoesNotContain(sim.Tasks.Tasks, t => t is RestockTask);
        Assert.Single(sim.Tasks.Tasks.OfType<UnloadTask>());

        var backroom = sim.Layout.Find(sim.InstanceOf("backroom"))!;
        sim.WalkPlayerTo(new GridPoint(backroom.Origin.X + 1, backroom.Origin.Y + 2));
        sim.Run(new PlayerInteract(backroom.InstanceId));
        TickUntil(sim, () => sim.Pallets.Count == 0);

        Assert.True(sim.Inventory.Total(Doubanjiang) > 0);
        Assert.Null(Send(sim, new RestockAll()));
        Assert.True(sim.Inventory.Count(slot, Doubanjiang) > 0);
    }

    [Fact]
    public void Stockers_unload_but_cashiers_do_not()
    {
        var (sim, _) = PalletWaiting();
        sim.Run(new HireEmployee("cashier"));
        for (var i = 0; i < 300; i++) sim.Tick();
        Assert.Single(sim.Pallets);

        sim.Run(new HireEmployee("stocker"));
        TickUntil(sim, () => sim.Pallets.Count == 0);
    }

    [Fact]
    public void A_storage_upgrade_adds_room()
    {
        var sim = Store();
        var backroom = sim.InstanceOf("backroom");
        var before = sim.StorageCapacityOf(backroom);
        var cash = sim.Economy.CashCents;

        Assert.Null(Send(sim, new UpgradeStorage(backroom)));

        Assert.Equal(before + Settings.StorageUpgradeSlots, sim.StorageCapacityOf(backroom));
        Assert.Equal(cash - Settings.StorageUpgradeCostCents, sim.Economy.CashCents);
    }

    [Fact]
    public void A_shipment_that_does_not_fit_is_left_by_the_door_and_blocks_its_cell()
    {
        var sim = Store();
        var backroom = StockLocation.Storage(sim.InstanceOf("backroom"));
        var filler = Def(sim, "noodles_instant");
        sim.Inventory.GetOrCreate(backroom).Add(new StockLot(filler.Id, sim.StorageCapacityOf(backroom.FixtureInstanceId) * filler.UnitsPerStorageSlot, 999, 80));
        sim.Run(new PlaceImportOrder(Doubanjiang, Def(sim, Doubanjiang).Import!.MinUnits));

        while (sim.Pallets.Count == 0) sim.RunFullDay();

        var pallet = Assert.Single(sim.Pallets);
        Assert.NotNull(pallet.FloorCell);
        Assert.False(sim.IsFloorFree(pallet.FloorCell!.Value));
        Assert.Null(LayoutRules.Check(sim.Layout.Size, sim.Layout.Fixtures, id => sim.Data.Fixtures.Single(f => f.Id == id)));
    }

    [Fact]
    public void Shipments_and_pallets_survive_save_and_load()
    {
        var (sim, _) = PalletWaiting();
        sim.Run(new PlaceImportOrder(Doubanjiang, Def(sim, Doubanjiang).Import!.MinUnits));
        sim.Run(new UpgradeStorage(sim.InstanceOf("backroom")));

        var json = SaveGame.Write(sim);
        var loaded = SaveGame.Read(sim.Data, json);

        Assert.Equal(json, SaveGame.Write(loaded));
        Assert.Equal(sim.Shipments.Count, loaded.Shipments.Count);
        Assert.Equal(sim.Pallets.Count, loaded.Pallets.Count);
    }
}
