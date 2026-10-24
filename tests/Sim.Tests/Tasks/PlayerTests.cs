using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Tasks;

namespace ChineseGrocery.Sim.Tests.Tasks;

/// <summary>Player movement and interaction. Starting layout: see FixturePlacementTests.</summary>
public class PlayerTests
{
    private static readonly GridPoint BackroomCell = new(10, 2);

    private static RejectReason RejectionFor(GameSim sim, SimCommand command)
    {
        sim.DrainEvents();
        sim.Run(command);
        return sim.DrainEvents().Single<CommandRejected>().Reason;
    }

    [Fact]
    public void Player_starts_on_the_entrance_step_idle()
    {
        var player = SimTestKit.NewSim().Player;

        Assert.True(player.IsPlayer);
        Assert.Equal(new GridPoint(5, 6), player.Position);
        Assert.Equal(WorkStep.Idle, player.Step);
        Assert.Empty(player.Carrying);
    }

    [Fact]
    public void Player_walks_the_configured_cells_per_tick()
    {
        var data = SimTestKit.Data with { Employees = SimTestKit.Data.Employees with { Player = SimTestKit.Data.Employees.Player with { MoveCellsPerTick = 2 } } };
        var sim = SimTestKit.NewSim(data: data);

        sim.Run(new MovePlayer(new GridPoint(5, 2)));
        Assert.Equal(new GridPoint(5, 4), sim.Player.Position);

        sim.Tick();
        Assert.Equal(new GridPoint(5, 2), sim.Player.Position);
        Assert.Empty(sim.Player.Path);
    }

    [Fact]
    public void Player_walks_around_fixtures_one_cell_at_a_time()
    {
        var data = SimTestKit.Data with { Employees = SimTestKit.Data.Employees with { Player = SimTestKit.Data.Employees.Player with { MoveCellsPerTick = 1 } } };
        var sim = SimTestKit.NewSim(data: data);
        var occupied = sim.Layout.Fixtures
            .SelectMany(f => LayoutRules.Footprint(data.Fixtures.Single(d => d.Id == f.FixtureId), f.Origin, f.Rotation))
            .ToHashSet();
        var visited = new List<GridPoint> { sim.Player.Position };

        // (3,0) is behind the shelf row, so the route has to go around a shelf.
        sim.Run(new MovePlayer(new GridPoint(3, 0)));
        visited.Add(sim.Player.Position);
        while (sim.Player.Path.Count > 0)
        {
            sim.Tick();
            visited.Add(sim.Player.Position);
        }

        Assert.Equal(new GridPoint(3, 0), sim.Player.Position);
        Assert.DoesNotContain(visited, occupied.Contains);
        Assert.All(visited.Zip(visited.Skip(1)), step => Assert.Equal(1, Math.Abs(step.First.X - step.Second.X) + Math.Abs(step.First.Y - step.Second.Y)));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(-1, 3)]
    [InlineData(12, 3)]
    public void Player_cannot_walk_into_fixtures_or_outside(int x, int y)
    {
        Assert.Equal(RejectReason.Unreachable, RejectionFor(SimTestKit.NewSim(), new MovePlayer(new GridPoint(x, y))));
    }

    [Fact]
    public void Interacting_where_there_is_nothing_to_do_is_rejected()
    {
        var sim = SimTestKit.NewSim();

        Assert.Equal(RejectReason.NothingToDo, RejectionFor(sim, new PlayerInteract()));
        sim.Run(new MovePlayer(BackroomCell));
        Assert.Equal(RejectReason.NothingToDo, RejectionFor(sim, new PlayerInteract()));
    }

    [Fact]
    public void Interacting_with_a_fixture_you_are_not_at_is_rejected()
    {
        var sim = SimTestKit.NewSim();

        Assert.Equal(RejectReason.NotAtFixture, RejectionFor(sim, new PlayerInteract(sim.InstanceOf("backroom"))));
    }

    [Fact]
    public void Fixture_cannot_be_placed_on_the_player()
    {
        var sim = SimTestKit.NewSim();

        sim.Run(new MovePlayer(new GridPoint(7, 3)));

        Assert.Equal(RejectReason.BlocksWorker, RejectionFor(sim, new PlaceFixture("shelf", new GridPoint(7, 3))));
    }
}
