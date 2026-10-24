using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Tests.Grid;

/// <summary>
/// Starting layout (12x8 store; x right, y down):
/// shelves at (1,1) and (4,1) facing down, checkout at (2,5) used from (2,4) and (2,6),
/// backroom 3x2 at (9,0) used from (10,2), entrance at (5,7) used from (5,6).
/// </summary>
public class FixturePlacementTests
{
    private static readonly long ShelfPrice = SimTestKit.Data.Fixtures.Single(f => f.Id == "shelf").PriceCents;
    private static readonly int RefundPermille = SimTestKit.Data.Economy.FixtureSellRefundPermille;

    private static (GameSim Sim, IReadOnlyList<SimEvent> Events) RunOn(GameSim sim, SimCommand command)
    {
        sim.DrainEvents();
        sim.Run(command);
        return (sim, sim.DrainEvents());
    }

    private static CommandRejected Rejected(SimCommand command, GameData? data = null)
    {
        var sim = SimTestKit.NewSim(data: data);
        var before = SimSnapshot.Describe(sim);
        var (_, events) = RunOn(sim, command);
        var rejected = events.Single<CommandRejected>();
        Assert.Equal(before.Replace("\"tickCount\":0", "\"tickCount\":1"), SimSnapshot.Describe(sim));
        return rejected;
    }

    [Fact]
    public void New_game_starts_with_the_starting_layout_for_free()
    {
        var sim = SimTestKit.NewSim();
        var expected = SimTestKit.Data.Economy.StartingLayout;

        Assert.Equal(expected.Count, sim.Layout.Fixtures.Count);
        Assert.Equal(expected.Select((e, i) => new PlacedFixture(i + 1, e.FixtureId, e.Origin, 0, e.Rotation)), sim.Layout.Fixtures);
        Assert.Equal(SimTestKit.Data.Stages[0].StoreSize, sim.Layout.Size);
        Assert.Equal(SimTestKit.Data.Economy.StartingCashCents, sim.Economy.CashCents);
    }

    [Fact]
    public void Placing_a_fixture_pays_for_it()
    {
        var (sim, events) = RunOn(SimTestKit.NewSim(), new PlaceFixture("shelf", new GridPoint(7, 3)));

        var placed = events.Single<FixturePlaced>();
        Assert.Equal(new PlacedFixture(placed.InstanceId, "shelf", new GridPoint(7, 3), ShelfPrice), sim.Layout.Find(placed.InstanceId));
        Assert.Equal(SimTestKit.Data.Economy.StartingCashCents - ShelfPrice, sim.Economy.CashCents);
        Assert.Equal(ShelfPrice, sim.Economy.Today.FixtureSpendCents);
    }

    [Fact]
    public void Instance_ids_are_never_reused()
    {
        var sim = SimTestKit.NewSim();
        sim.Run(new PlaceFixture("shelf", new GridPoint(7, 3)));
        var first = sim.Layout.Fixtures[^1].InstanceId;
        sim.Run(new SellFixture(first));

        sim.Run(new PlaceFixture("shelf", new GridPoint(7, 3)));

        Assert.Equal(first + 1, sim.Layout.Fixtures[^1].InstanceId);
    }

    [Theory]
    [InlineData(-1, 3)]
    [InlineData(11, 3)]
    [InlineData(5, -1)]
    public void Fixture_partly_outside_the_store_is_rejected(int x, int y)
    {
        Assert.Equal(RejectReason.OutOfBounds, Rejected(new PlaceFixture("shelf", new GridPoint(x, y))).Reason);
    }

    [Fact]
    public void Overlapping_fixture_is_rejected()
    {
        Assert.Equal(RejectReason.Overlap, Rejected(new PlaceFixture("shelf", new GridPoint(2, 1))).Reason);
    }

    [Fact]
    public void Fixture_whose_own_interaction_cell_is_outside_is_rejected()
    {
        var rejected = Rejected(new PlaceFixture("shelf", new GridPoint(7, 7)));

        Assert.Equal(RejectReason.PathBlocked, rejected.Reason);
        Assert.Equal(SimTestKit.NewSim().Layout.NextInstanceId, rejected.FixtureInstanceId);
    }

    [Fact]
    public void Fixture_covering_another_fixtures_interaction_cell_is_rejected_naming_that_fixture()
    {
        var sim = SimTestKit.NewSim();

        // (2,2) is the front of the first shelf.
        var rejected = Rejected(new PlaceFixture("shelf", new GridPoint(2, 2)));

        Assert.Equal(RejectReason.PathBlocked, rejected.Reason);
        Assert.Equal(sim.InstanceOf("shelf"), rejected.FixtureInstanceId);
    }

    [Fact]
    public void Fixture_cutting_off_part_of_the_store_is_rejected()
    {
        var divider = new FixtureDef("divider", "fixture.divider.name", FixtureKind.Shelf, new GridSize(12, 1), [], [], 0, [], 0, 1);
        var data = SimTestKit.Data with { Fixtures = [.. SimTestKit.Data.Fixtures, divider] };

        var rejected = Rejected(new PlaceFixture("divider", new GridPoint(0, 3)), data);

        Assert.Equal(RejectReason.PathBlocked, rejected.Reason);
    }

    [Fact]
    public void Fixture_from_a_later_stage_is_rejected()
    {
        Assert.Equal(RejectReason.FixtureLocked, Rejected(new PlaceFixture("meat_display", new GridPoint(7, 3))).Reason);
    }

    [Fact]
    public void Unknown_fixture_is_rejected()
    {
        Assert.Equal(RejectReason.UnknownFixture, Rejected(new PlaceFixture("no_such_fixture", new GridPoint(7, 3))).Reason);
    }

    [Fact]
    public void Fixture_costing_more_than_cash_is_rejected()
    {
        var data = SimTestKit.Data with { Economy = SimTestKit.Data.Economy with { StartingCashCents = ShelfPrice - 1 } };

        Assert.Equal(RejectReason.InsufficientCash, Rejected(new PlaceFixture("shelf", new GridPoint(7, 3)), data).Reason);
    }

    [Fact]
    public void Layout_cannot_change_while_open()
    {
        var sim = SimTestKit.NewSim();
        sim.Run(new OpenStore());
        var shelf = sim.InstanceOf("shelf");

        foreach (SimCommand c in new SimCommand[] { new PlaceFixture("shelf", new GridPoint(7, 3)), new MoveFixture(shelf, new GridPoint(7, 3), Rotation.None), new SellFixture(shelf) })
        {
            var (_, events) = RunOn(sim, c);
            Assert.Equal(RejectReason.WrongPhase, events.Single<CommandRejected>().Reason);
        }
    }

    [Fact]
    public void Moving_is_free_and_keeps_the_instance()
    {
        var sim = SimTestKit.NewSim();
        var shelf = sim.InstanceOf("shelf");

        var (_, events) = RunOn(sim, new MoveFixture(shelf, new GridPoint(7, 3), Rotation.None));

        Assert.Equal(new FixtureMoved(shelf, new GridPoint(1, 1), new GridPoint(7, 3), Rotation.None), events.Single<FixtureMoved>());
        Assert.Equal(new GridPoint(7, 3), sim.Layout.Find(shelf)!.Origin);
        Assert.Equal(SimTestKit.Data.Economy.StartingCashCents, sim.Economy.CashCents);
    }

    [Fact]
    public void Moving_onto_its_own_old_cells_is_allowed()
    {
        var sim = SimTestKit.NewSim();
        var shelf = sim.InstanceOf("shelf");

        sim.Run(new MoveFixture(shelf, new GridPoint(0, 1), Rotation.None));

        Assert.Equal(new GridPoint(0, 1), sim.Layout.Find(shelf)!.Origin);
    }

    [Fact]
    public void Invalid_move_is_rejected_and_reported_against_the_moved_fixture()
    {
        var sim = SimTestKit.NewSim();
        var shelf = sim.InstanceOf("shelf");

        var rejected = Rejected(new MoveFixture(shelf, new GridPoint(4, 1), Rotation.None));

        Assert.Equal(RejectReason.Overlap, rejected.Reason);
        Assert.Equal(shelf, rejected.FixtureInstanceId);
    }

    [Fact]
    public void Unknown_instance_is_rejected()
    {
        Assert.Equal(RejectReason.UnknownFixtureInstance, Rejected(new MoveFixture(999, new GridPoint(7, 3), Rotation.None)).Reason);
        Assert.Equal(RejectReason.UnknownFixtureInstance, Rejected(new SellFixture(999)).Reason);
    }

    [Fact]
    public void Selling_refunds_a_share_of_what_was_paid()
    {
        var sim = SimTestKit.NewSim();
        sim.Run(new PlaceFixture("shelf", new GridPoint(7, 3)));
        var bought = sim.Layout.Fixtures[^1].InstanceId;

        var (_, events) = RunOn(sim, new SellFixture(bought));

        var refund = ShelfPrice * RefundPermille / 1000;
        Assert.Equal(new FixtureSold(bought, "shelf", refund), events.Single<FixtureSold>());
        Assert.Null(sim.Layout.Find(bought));
        Assert.Equal(SimTestKit.Data.Economy.StartingCashCents - ShelfPrice + refund, sim.Economy.CashCents);
        Assert.Equal(ShelfPrice - refund, sim.Economy.Today.FixtureSpendCents);
    }

    [Fact]
    public void Selling_a_starting_fixture_refunds_nothing()
    {
        var sim = SimTestKit.NewSim();

        var (_, events) = RunOn(sim, new SellFixture(sim.InstanceOf("shelf")));

        Assert.Equal(0, events.Single<FixtureSold>().RefundCents);
        Assert.Equal(SimTestKit.Data.Economy.StartingCashCents, sim.Economy.CashCents);
    }

    [Fact]
    public void The_last_entrance_cannot_be_sold()
    {
        var sim = SimTestKit.NewSim();

        Assert.Equal(RejectReason.LastEntrance, Rejected(new SellFixture(sim.InstanceOf("entrance"))).Reason);
    }

    [Fact]
    public void An_entrance_can_be_sold_when_another_remains()
    {
        var sim = SimTestKit.NewSim();
        var original = sim.InstanceOf("entrance");
        sim.Run(new PlaceFixture("entrance", new GridPoint(8, 7)));

        var (_, events) = RunOn(sim, new SellFixture(original));

        Assert.Equal(original, events.Single<FixtureSold>().InstanceId);
    }

    [Fact]
    public void A_storage_area_holding_stock_cannot_be_sold()
    {
        var sim = SimTestKit.NewSim();
        sim.Run(new PlaceOrder("rice_jasmine", 1));
        sim.RunFullDay();

        var (_, events) = RunOn(sim, new SellFixture(sim.InstanceOf("backroom")));

        Assert.Equal(RejectReason.FixtureNotEmpty, events.Single<CommandRejected>().Reason);
    }

    [Fact]
    public void The_storage_area_awaiting_a_delivery_cannot_be_sold()
    {
        var sim = SimTestKit.NewSim();
        sim.Run(new PlaceOrder("rice_jasmine", 1));

        var (_, events) = RunOn(sim, new SellFixture(sim.InstanceOf("backroom")));

        Assert.Equal(RejectReason.StorageNeededForOrders, events.Single<CommandRejected>().Reason);
    }

    [Fact]
    public void Without_a_backroom_nothing_can_be_ordered()
    {
        var sim = SimTestKit.NewSim();
        sim.Run(new SellFixture(sim.InstanceOf("backroom")));

        var (_, events) = RunOn(sim, new PlaceOrder("rice_jasmine", 1));

        Assert.Equal(RejectReason.NoStorageForProduct, events.Single<CommandRejected>().Reason);
    }

    [Fact]
    public void Moved_backroom_keeps_its_stock()
    {
        var sim = SimTestKit.NewSim();
        sim.Run(new PlaceOrder("rice_jasmine", 4));
        sim.RunFullDay();
        var backroom = sim.InstanceOf("backroom");

        sim.Run(new MoveFixture(backroom, new GridPoint(7, 3), Rotation.None));

        Assert.Equal(new GridPoint(7, 3), sim.Layout.Find(backroom)!.Origin);
        Assert.Equal(4, sim.Inventory.Get(StockLocation.Storage(backroom)).Count("rice_jasmine"));
    }

    [Fact]
    public void Rotated_shelf_can_stand_on_the_bottom_row_facing_up()
    {
        var (sim, events) = RunOn(SimTestKit.NewSim(), new PlaceFixture("shelf", new GridPoint(7, 7), Rotation.Cw180));

        var placed = events.Single<FixturePlaced>();
        Assert.Equal(new FixturePlaced(placed.InstanceId, "shelf", new GridPoint(7, 7), Rotation.Cw180, ShelfPrice), placed);
        Assert.Equal(Rotation.Cw180, sim.Layout.Find(placed.InstanceId)!.Rotation);
    }

    [Fact]
    public void Rotation_changes_the_footprint()
    {
        // Turned a quarter, the 2x1 shelf is 1 wide and 2 tall: (11,3)-(11,4), used from (10,3) and (10,4).
        var (sim, events) = RunOn(SimTestKit.NewSim(), new PlaceFixture("shelf", new GridPoint(11, 3), Rotation.Cw90));

        Assert.NotNull(sim.Layout.Find(events.Single<FixturePlaced>().InstanceId));
        Assert.Equal(RejectReason.OutOfBounds, Rejected(new PlaceFixture("shelf", new GridPoint(11, 3), Rotation.None)).Reason);
    }

    [Fact]
    public void Shelf_facing_the_wall_is_rejected()
    {
        Assert.Equal(RejectReason.PathBlocked, Rejected(new PlaceFixture("shelf", new GridPoint(0, 3), Rotation.Cw90)).Reason);
    }

    [Fact]
    public void Move_can_rotate_in_place()
    {
        var sim = SimTestKit.NewSim();
        sim.Run(new PlaceFixture("shelf", new GridPoint(7, 3)));
        var shelf = sim.Layout.Fixtures[^1].InstanceId;
        sim.DrainEvents();

        sim.Run(new MoveFixture(shelf, new GridPoint(7, 3), Rotation.Cw180));

        Assert.Equal(new FixtureMoved(shelf, new GridPoint(7, 3), new GridPoint(7, 3), Rotation.Cw180), sim.DrainEvents().Single<FixtureMoved>());
        Assert.Equal(Rotation.Cw180, sim.Layout.Find(shelf)!.Rotation);
    }
}
