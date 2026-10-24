using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;

namespace ChineseGrocery.Sim.Grid;

/// <summary>
/// Placement rules: fixtures stay inside the store, never overlap, there is an entrance, and every
/// interaction or customer cell is a free cell reachable on foot from an entrance.
/// </summary>
public static class LayoutRules
{
    public static GridSize RotatedSize(FixtureDef def, Rotation rotation) =>
        rotation is Rotation.Cw90 or Rotation.Cw270 ? new GridSize(def.Size.H, def.Size.W) : def.Size;

    public static IEnumerable<GridPoint> Footprint(FixtureDef def, GridPoint origin, Rotation rotation = Rotation.None)
    {
        var size = RotatedSize(def, rotation);
        for (var y = 0; y < size.H; y++)
        for (var x = 0; x < size.W; x++)
            yield return new GridPoint(origin.X + x, origin.Y + y);
    }

    /// <summary>Where the player or staff stand to work the fixture, in store coordinates.</summary>
    public static IEnumerable<GridPoint> InteractionCells(FixtureDef def, GridPoint origin, Rotation rotation = Rotation.None) =>
        Place(def.InteractionCells, def, origin, rotation);

    /// <summary>Where customers stand to use the fixture, in store coordinates.</summary>
    public static IEnumerable<GridPoint> CustomerCells(FixtureDef def, GridPoint origin, Rotation rotation = Rotation.None) =>
        Place(def.CustomerCells, def, origin, rotation);

    private static IEnumerable<GridPoint> Place(IEnumerable<GridPoint> cells, FixtureDef def, GridPoint origin, Rotation rotation) =>
        cells.Select(c => Rotate(c, def.Size, rotation)).Select(c => new GridPoint(origin.X + c.X, origin.Y + c.Y));

    private static IEnumerable<GridPoint> AllUseCells(FixtureDef def, PlacedFixture f) =>
        InteractionCells(def, f.Origin, f.Rotation).Concat(CustomerCells(def, f.Origin, f.Rotation));

    /// <summary>Turns a cell given relative to an unrotated W x H footprint clockwise, keeping the result relative to the rotated footprint's top-left.</summary>
    public static GridPoint Rotate(GridPoint c, GridSize size, Rotation rotation) => rotation switch
    {
        Rotation.None => c,
        Rotation.Cw90 => new GridPoint(size.H - 1 - c.Y, c.X),
        Rotation.Cw180 => new GridPoint(size.W - 1 - c.X, size.H - 1 - c.Y),
        Rotation.Cw270 => new GridPoint(c.Y, size.W - 1 - c.X),
        _ => throw new ArgumentOutOfRangeException(nameof(rotation), rotation, null),
    };

    public static bool InBounds(GridSize size, GridPoint p) => p.X >= 0 && p.Y >= 0 && p.X < size.W && p.Y < size.H;

    /// <summary>
    /// Checks a candidate layout. When a problem involves <paramref name="focusId"/> (the fixture being placed or moved),
    /// it is reported against that fixture first.
    /// </summary>
    public static Rejection? Check(GridSize size, IReadOnlyList<PlacedFixture> fixtures, Func<string, FixtureDef> defs, int focusId = 0)
    {
        var occupied = new HashSet<GridPoint>();
        foreach (var f in fixtures.OrderBy(f => f.InstanceId == focusId))
        {
            foreach (var cell in Footprint(defs(f.FixtureId), f.Origin, f.Rotation))
            {
                if (!InBounds(size, cell)) return new(RejectReason.OutOfBounds, f.InstanceId);
                if (!occupied.Add(cell)) return new(RejectReason.Overlap, f.InstanceId);
            }
        }

        var entrances = fixtures.Where(f => defs(f.FixtureId).Kind == FixtureKind.Entrance).ToList();
        if (entrances.Count == 0) return new(RejectReason.MissingEntrance, 0);

        bool Walkable(GridPoint p) => InBounds(size, p) && !occupied.Contains(p);
        var reachable = GridPaths.Reachable(Walkable, entrances.SelectMany(e => AllUseCells(defs(e.FixtureId), e)));

        foreach (var f in fixtures.OrderBy(f => f.InstanceId != focusId))
        {
            if (AllUseCells(defs(f.FixtureId), f).Any(c => !reachable.Contains(c)))
            {
                return new(RejectReason.PathBlocked, f.InstanceId);
            }
        }

        return null;
    }
}
