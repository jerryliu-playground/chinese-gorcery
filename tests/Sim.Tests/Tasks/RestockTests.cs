using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Tasks;

namespace ChineseGrocery.Sim.Tests.Tasks;

public class RestockTests
{
    private const string Rice = "rice_jasmine";
    private static readonly GridPoint BackroomCell = new(10, 2);
    private static readonly GridPoint ShelfFront = new(1, 2);
    private static readonly int PerShelfSlot = SimTestKit.Data.Products.Single(p => p.Id == Rice).UnitsPerShelfSlot;

    /// <summary>Day 2 PreOpen with 20 rice in the backroom and the first shelf's slot 0 assigned to rice.</summary>
    private static (GameSim Sim, StockLocation Slot, StockLocation Backroom) Stocked(int rice = 20)
    {
        var sim = SimTestKit.NewSim();
        sim.Run(new PlaceOrder(Rice, rice));
        sim.RunFullDay();
        var slot = new StockLocation(sim.InstanceOf("shelf"), 0);
        sim.Run(new AssignShelfSlot(slot.FixtureInstanceId, slot.Slot, Rice));
        sim.DrainEvents();
        return (sim, slot, StockLocation.Storage(sim.InstanceOf("backroom")));
    }

    private static void TickUntil(GameSim sim, Func<bool> done, int max = 50)
    {
        for (var i = 0; !done(); i++)
        {
            if (i == max) throw new InvalidOperationException("Condition never met.");
            sim.Tick();
        }
    }

    private static void RestockByHand(GameSim sim)
    {
        sim.Run(new MovePlayer(BackroomCell));
        sim.Run(new PlayerInteract());
        TickUntil(sim, () => sim.Player.Step == WorkStep.Carrying);
        sim.Run(new MovePlayer(ShelfFront));
        sim.Run(new PlayerInteract());
        TickUntil(sim, () => sim.Player.Step == WorkStep.Idle);
    }

    [Fact]
    public void Empty_assigned_slot_with_stock_in_back_gets_one_restock_task()
    {
        var (sim, slot, backroom) = Stocked();

        sim.Tick();
        sim.Tick();

        var task = Assert.IsType<RestockTask>(Assert.Single(sim.Tasks.Tasks));
        Assert.Equal((backroom, slot, Rice), (task.Source, task.Target, task.ProductId));
        Assert.Equal(SimTestKit.Data.Economy.Tasks.PriorityOf(TaskType.Restock), task.Priority);
        Assert.Null(sim.Tasks.ClaimedBy(task.Id));
    }

    [Fact]
    public void No_restock_task_without_stock_in_back()
    {
        var sim = SimTestKit.NewSim();
        sim.Run(new AssignShelfSlot(sim.InstanceOf("shelf"), 0, Rice));

        sim.Tick();

        Assert.Empty(sim.Tasks.Tasks);
    }

    [Fact]
    public void Player_restocks_a_slot_with_the_same_task_logic()
    {
        var (sim, slot, backroom) = Stocked();
        sim.Tick();

        sim.Run(new MovePlayer(BackroomCell));
        sim.Run(new PlayerInteract());
        var taskId = sim.Player.TaskId!.Value;
        Assert.Equal(sim.Player.Id, sim.Tasks.ClaimedBy(taskId));
        Assert.Equal(TaskType.Restock, sim.DrainEvents().Single<TaskStarted>().TaskType);

        TickUntil(sim, () => sim.Player.Step == WorkStep.Carrying);
        Assert.Equal(PerShelfSlot, sim.Player.Carrying.Sum(l => l.Quantity));
        Assert.Equal(20 - PerShelfSlot, sim.Inventory.Count(backroom, Rice));

        sim.Run(new MovePlayer(ShelfFront));
        sim.Run(new PlayerInteract());
        TickUntil(sim, () => sim.Player.Step == WorkStep.Idle);

        Assert.Equal(PerShelfSlot, sim.Inventory.Count(slot, Rice));
        Assert.Empty(sim.Player.Carrying);
        Assert.DoesNotContain(sim.Tasks.Tasks, t => t.Id == taskId);
        Assert.Equal(new TaskCompleted(taskId, TaskType.Restock, sim.Player.Id), sim.DrainEvents().Single<TaskCompleted>());
    }

    [Fact]
    public void Restock_takes_only_what_the_back_has()
    {
        var (sim, slot, _) = Stocked(rice: 1);
        sim.Tick();

        RestockByHand(sim);

        Assert.Equal(1, sim.Inventory.Count(slot, Rice));
    }

    [Fact]
    public void Full_slot_needs_no_restock()
    {
        var (sim, _, _) = Stocked();
        sim.Tick();
        RestockByHand(sim);

        sim.Tick();

        Assert.Empty(sim.Tasks.Tasks);
    }

    [Theory]
    [InlineData(1000, 3)]
    [InlineData(2000, 2)]
    [InlineData(500, 6)]
    public void Work_takes_the_configured_minutes_scaled_by_work_speed(int speedPermille, int expectedTicks)
    {
        var data = SimTestKit.Data with
        {
            Economy = SimTestKit.Data.Economy with { Tasks = SimTestKit.Data.Economy.Tasks with { RestockPickupMinutes = 3 } },
            Employees = SimTestKit.Data.Employees with { Player = SimTestKit.Data.Employees.Player with { WorkSpeedPermille = speedPermille } },
        };
        var sim = SimTestKit.NewSim(data: data);
        sim.Run(new PlaceOrder(Rice, 20));
        sim.RunFullDay();
        sim.Run(new AssignShelfSlot(sim.InstanceOf("shelf"), 0, Rice));
        sim.Run(new MovePlayer(BackroomCell));

        // The interact tick is the first tick of work; at 2000 permille 3 minutes round up to 2 ticks.
        sim.Run(new PlayerInteract());
        var ticks = 1;
        while (sim.Player.Step == WorkStep.Picking)
        {
            sim.Tick();
            ticks++;
        }

        Assert.Equal(expectedTicks, ticks);
        Assert.Equal(WorkStep.Carrying, sim.Player.Step);
    }

    [Fact]
    public void Carrying_player_can_only_shelve_into_the_task_target()
    {
        var (sim, _, _) = Stocked();
        sim.Tick();
        sim.Run(new MovePlayer(BackroomCell));
        sim.Run(new PlayerInteract());
        TickUntil(sim, () => sim.Player.Step == WorkStep.Carrying);

        // The second shelf's front is not the target.
        sim.Run(new MovePlayer(new GridPoint(4, 2)));
        sim.DrainEvents();
        sim.Run(new PlayerInteract());

        Assert.Equal(RejectReason.NothingToDo, sim.DrainEvents().Single<CommandRejected>().Reason);
        Assert.Equal(WorkStep.Carrying, sim.Player.Step);
    }

    [Fact]
    public void Player_cannot_walk_away_mid_work()
    {
        var data = SimTestKit.Data with { Economy = SimTestKit.Data.Economy with { Tasks = SimTestKit.Data.Economy.Tasks with { RestockPickupMinutes = 5 } } };
        var sim = SimTestKit.NewSim(data: data);
        sim.Run(new PlaceOrder(Rice, 20));
        sim.RunFullDay();
        sim.Run(new AssignShelfSlot(sim.InstanceOf("shelf"), 0, Rice));
        sim.Run(new MovePlayer(BackroomCell));
        sim.Run(new PlayerInteract());
        sim.DrainEvents();

        sim.Run(new MovePlayer(ShelfFront));

        Assert.Equal(RejectReason.Busy, sim.DrainEvents().Single<CommandRejected>().Reason);
        Assert.Equal(BackroomCell, sim.Player.Position);
    }

    [Fact]
    public void Slot_and_fixtures_in_use_by_a_claimed_task_are_locked()
    {
        var (sim, slot, backroom) = Stocked();
        sim.Tick();
        sim.Run(new MovePlayer(BackroomCell));
        sim.Run(new PlayerInteract());
        TickUntil(sim, () => sim.Player.Step == WorkStep.Carrying);
        sim.DrainEvents();

        sim.Run(new AssignShelfSlot(slot.FixtureInstanceId, slot.Slot, "noodles_instant"));
        sim.Run(new SellFixture(slot.FixtureInstanceId));

        Assert.Equal([RejectReason.SlotInUse, RejectReason.FixtureInUse], sim.DrainEvents().OfType<CommandRejected>().Select(r => r.Reason));
    }

    [Fact]
    public void Open_task_disappears_when_its_slot_is_unassigned()
    {
        var (sim, slot, _) = Stocked();
        sim.Tick();

        sim.Run(new AssignShelfSlot(slot.FixtureInstanceId, slot.Slot, null));

        Assert.Empty(sim.Tasks.Tasks);
    }

    [Fact]
    public void Carried_goods_expire_too()
    {
        var data = SimTestKit.Data with
        {
            Products = SimTestKit.Data.Products.Select(p => p.Id == Rice ? p with { ShelfLifeDays = 1 } : p).ToList(),
        };
        var sim = SimTestKit.NewSim(data: data);
        sim.Run(new PlaceOrder(Rice, 20));
        sim.RunFullDay();
        sim.Run(new AssignShelfSlot(sim.InstanceOf("shelf"), 0, Rice));
        sim.Run(new MovePlayer(BackroomCell));
        sim.Run(new PlayerInteract());
        TickUntil(sim, () => sim.Player.Step == WorkStep.Carrying);

        sim.OpenAndRunToClose();

        Assert.Empty(sim.Player.Carrying);
        Assert.Equal(0, sim.Inventory.Total(Rice));
        Assert.Equal(20 * data.Products.Single(p => p.Id == Rice).CostCents, sim.Reports[^1].LossCents);
    }
}
