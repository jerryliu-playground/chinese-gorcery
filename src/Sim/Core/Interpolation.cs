namespace ChineseGrocery.Sim.Core;

/// <summary>Reading values off data curves given as (x, y) points in rising x order.</summary>
public static class Interpolation
{
    /// <summary>y at <paramref name="x"/>, on straight lines between points and flat beyond the ends.</summary>
    public static int Linear(IReadOnlyList<(int X, int Y)> points, int x)
    {
        if (x <= points[0].X) return points[0].Y;

        for (var i = 1; i < points.Count; i++)
        {
            var (a, b) = (points[i - 1], points[i]);
            if (x <= b.X) return a.Y + (b.Y - a.Y) * (x - a.X) / (b.X - a.X);
        }

        return points[^1].Y;
    }
}
