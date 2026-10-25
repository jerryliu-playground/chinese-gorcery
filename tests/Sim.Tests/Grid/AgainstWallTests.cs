using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Grid;
using static ChineseGrocery.Sim.Tests.Deli.DeliScenario;

namespace ChineseGrocery.Sim.Tests.Grid;

/// <summary>Meat and fish stations and the entrance go against the wall (Jerry 2026-10-25); the rest go anywhere.</summary>
public class AgainstWallTests
{
    private static RejectReason? Send(GameSim sim, SimCommand command)
    {
        sim.DrainEvents();
        sim.Run(command);
        return sim.DrainEvents().OfType<CommandRejected>().SingleOrDefault()?.Reason;
    }

    [Theory]
    [InlineData("meat_display", 6, 4, 6, 0)]
    [InlineData("cutting_table", 6, 4, 6, 0)]
    [InlineData("fish_tank", 7, 4, 7, 0)]
    [InlineData("shellfish_tank", 7, 4, 7, 0)]
    [InlineData("seafood_counter", 7, 4, 0, 4)]
    [InlineData("entrance", 8, 4, 8, 7)]
    public void Meat_and_fish_stations_and_the_entrance_must_touch_the_wall(string fixtureId, int midX, int midY, int wallX, int wallY)
    {
        var sim = DeliStore();

        Assert.Equal(RejectReason.NotAgainstWall, Send(sim, new PlaceFixture(fixtureId, new GridPoint(midX, midY))));
        Assert.Null(Send(sim, new PlaceFixture(fixtureId, new GridPoint(wallX, wallY))));
    }

    [Theory]
    [InlineData("ice_table", 7, 4)]
    [InlineData("produce_stand", 7, 4)]
    [InlineData("cold_storage", 6, 3)]
    [InlineData("steam_table", 7, 4)]
    public void Other_fixtures_go_anywhere(string fixtureId, int x, int y)
    {
        var sim = DeliStore();

        Assert.Null(Send(sim, new PlaceFixture(fixtureId, new GridPoint(x, y))));
    }

    [Fact]
    public void A_station_against_the_wall_cannot_be_moved_away_from_it()
    {
        var sim = DeliStore();
        sim.Run(new PlaceFixture("fish_tank", new GridPoint(7, 0)));
        var tank = sim.Layout.Fixtures[^1].InstanceId;

        Assert.Equal(RejectReason.NotAgainstWall, Send(sim, new MoveFixture(tank, new GridPoint(7, 4), Rotation.None)));
        Assert.Null(Send(sim, new MoveFixture(tank, new GridPoint(8, 0), Rotation.None)));
    }

    [Fact]
    public void Fixtures_left_off_the_wall_by_an_expansion_stay_until_moved()
    {
        var sim = SimTestKit.NewSim();
        sim.Economy.Earn("rice_jasmine", 1, SimTestKit.Data.Stages[1].RequiredCumulativeRevenueCents);
        sim.Run(new UpgradeStage());
        var entrance = sim.InstanceOf("entrance");
        Assert.NotEqual(sim.Layout.Size.H - 1, sim.Layout.Find(entrance)!.Origin.Y);

        // The old entrance is now inside the floor, yet other changes are still allowed.
        Assert.Null(Send(sim, new PlaceFixture("shelf", new GridPoint(7, 3))));
        Assert.Equal(RejectReason.NotAgainstWall, Send(sim, new MoveFixture(entrance, new GridPoint(6, 7), Rotation.None)));
    }
}
