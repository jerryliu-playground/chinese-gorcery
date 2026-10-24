namespace ChineseGrocery.Sim.Grid;

/// <summary>Breadth-first search over walkable cells, moving in the four cardinal directions.</summary>
public static class GridPaths
{
    private static readonly GridPoint[] Steps = [new(0, -1), new(1, 0), new(0, 1), new(-1, 0)];

    public static HashSet<GridPoint> Reachable(Func<GridPoint, bool> walkable, IEnumerable<GridPoint> starts)
    {
        var seen = new HashSet<GridPoint>();
        var queue = new Queue<GridPoint>();
        foreach (var s in starts.Where(walkable))
        {
            if (seen.Add(s)) queue.Enqueue(s);
        }

        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            foreach (var step in Steps)
            {
                var n = new GridPoint(c.X + step.X, c.Y + step.Y);
                if (walkable(n) && seen.Add(n)) queue.Enqueue(n);
            }
        }

        return seen;
    }
}
