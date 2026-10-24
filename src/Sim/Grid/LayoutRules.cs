using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;

namespace ChineseGrocery.Sim.Grid;

/// <summary>
/// Placement rules: fixtures stay inside the store, never overlap, there is an entrance, and every
/// interaction cell is a free cell reachable on foot from an entrance.
/// </summary>
public static class LayoutRules
{
    public static IEnumerable<GridPoint> Footprint(FixtureDef def, GridPoint origin)
    {
        for (var y = 0; y < def.Size.H; y++)
        for (var x = 0; x < def.Size.W; x++)
            yield return new GridPoint(origin.X + x, origin.Y + y);
    }

    public static IEnumerable<GridPoint> InteractionCells(FixtureDef def, GridPoint origin) =>
        def.InteractionCells.Select(c => new GridPoint(origin.X + c.X, origin.Y + c.Y));

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
            foreach (var cell in Footprint(defs(f.FixtureId), f.Origin))
            {
                if (!InBounds(size, cell)) return new(RejectReason.OutOfBounds, f.InstanceId);
                if (!occupied.Add(cell)) return new(RejectReason.Overlap, f.InstanceId);
            }
        }

        var entrances = fixtures.Where(f => defs(f.FixtureId).Kind == FixtureKind.Entrance).ToList();
        if (entrances.Count == 0) return new(RejectReason.MissingEntrance, 0);

        bool Walkable(GridPoint p) => InBounds(size, p) && !occupied.Contains(p);
        var reachable = GridPaths.Reachable(Walkable, entrances.SelectMany(e => InteractionCells(defs(e.FixtureId), e.Origin)));

        foreach (var f in fixtures.OrderBy(f => f.InstanceId != focusId))
        {
            if (InteractionCells(defs(f.FixtureId), f.Origin).Any(c => !reachable.Contains(c)))
            {
                return new(RejectReason.PathBlocked, f.InstanceId);
            }
        }

        return null;
    }
}
