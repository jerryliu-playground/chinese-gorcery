using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Grid;

namespace ChineseGrocery.Sim.Tests.Grid;

public class LayoutRulesTests
{
    // Test-only 1x1 blocks: a post with no interaction cell, and a box used from the cell on its left.
    private static readonly FixtureDef Post = new("post", "fixture.post.name", FixtureKind.Shelf, new GridSize(1, 1), [], 0, [], 0, 1);
    private static readonly FixtureDef Box = new("box", "fixture.box.name", FixtureKind.Shelf, new GridSize(1, 1), [new GridPoint(-1, 0)], 0, [], 0, 1);

    private static FixtureDef Def(string id) =>
        id == Post.Id ? Post : id == Box.Id ? Box : SimTestKit.Data.Fixtures.Single(f => f.Id == id);

    private static Rejection? Check(params PlacedFixture[] fixtures) =>
        LayoutRules.Check(new GridSize(6, 4), fixtures, Def);

    private static PlacedFixture At(int id, string fixtureId, int x, int y) => new(id, fixtureId, new GridPoint(x, y), 0);

    [Fact]
    public void Layout_without_entrance_is_invalid()
    {
        Assert.Equal(new Rejection(RejectReason.MissingEntrance, 0), Check(At(1, "shelf", 0, 0)));
    }

    [Fact]
    public void Entrance_whose_step_inside_is_outside_the_store_is_invalid()
    {
        Assert.Equal(RejectReason.PathBlocked, Check(At(1, "entrance", 2, 0))?.Reason);
    }

    [Fact]
    public void Connected_layout_is_valid()
    {
        Assert.Null(Check(At(1, "entrance", 2, 3), At(2, "shelf", 0, 0), At(3, "checkout", 3, 1)));
    }

    [Fact]
    public void Several_entrances_are_allowed()
    {
        Assert.Null(Check(At(1, "entrance", 0, 3), At(2, "entrance", 5, 3), At(3, "shelf", 3, 1)));
    }

    [Fact]
    public void Reachability_is_four_directional()
    {
        // The box at (1,0) is used from (0,0); with a post at (0,1) that cell is only reachable diagonally via (1,1).
        var problem = Check(At(1, "entrance", 4, 3), At(2, "box", 1, 0), At(3, "post", 0, 1));

        Assert.Equal(new Rejection(RejectReason.PathBlocked, 2), problem);
    }
}
