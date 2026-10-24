namespace ChineseGrocery.Sim.Grid;

/// <summary>Breadth-first search over walkable cells, moving in the four cardinal directions.</summary>
public static class GridPaths
{
    private static readonly GridPoint[] Steps = [new(0, -1), new(1, 0), new(0, 1), new(-1, 0)];

    /// <summary>Shortest path from <paramref name="start"/> to <paramref name="goal"/>, excluding the start; null when unreachable.</summary>
    public static List<GridPoint>? FindPath(Func<GridPoint, bool> walkable, GridPoint start, GridPoint goal)
    {
        if (!walkable(goal)) return null;
        if (start == goal) return [];

        var cameFrom = new Dictionary<GridPoint, GridPoint>();
        var queue = new Queue<GridPoint>();
        queue.Enqueue(start);
        cameFrom[start] = start;
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            if (c == goal) break;
            foreach (var step in Steps)
            {
                var n = new GridPoint(c.X + step.X, c.Y + step.Y);
                if (walkable(n) && cameFrom.TryAdd(n, c)) queue.Enqueue(n);
            }
        }

        if (!cameFrom.ContainsKey(goal)) return null;

        var path = new List<GridPoint>();
        for (var p = goal; p != start; p = cameFrom[p]) path.Add(p);
        path.Reverse();
        return path;
    }

    /// <summary>Walking distance from <paramref name="start"/> to every reachable cell.</summary>
    public static Dictionary<GridPoint, int> Distances(Func<GridPoint, bool> walkable, GridPoint start)
    {
        var dist = new Dictionary<GridPoint, int> { [start] = 0 };
        var queue = new Queue<GridPoint>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            foreach (var step in Steps)
            {
                var n = new GridPoint(c.X + step.X, c.Y + step.Y);
                if (walkable(n) && dist.TryAdd(n, dist[c] + 1)) queue.Enqueue(n);
            }
        }

        return dist;
    }

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
