using ChineseGrocery.Sim.Reputation;
using Godot;

namespace ChineseGrocery.Game.Ui;

/// <summary>
/// Reputation after each closing as a line (0-100 up the side), over faint lines at each star threshold.
/// Shows the most recent days that fit.
/// </summary>
public partial class ReputationChart : Control
{
    private const int MaxReputation = 100;
    private const float DayWidth = 8;

    private static readonly Color Back = new(0.08f, 0.08f, 0.1f);
    private static readonly Color Grid = new(0.3f, 0.3f, 0.34f);
    private static readonly Color Line = new(0.98f, 0.78f, 0.3f);

    private IReadOnlyList<ReputationOfDay> _history = [];
    private IReadOnlyList<int> _thresholds = [];

    public ReputationChart() => CustomMinimumSize = new Vector2(300, 60);

    public void SetData(IReadOnlyList<ReputationOfDay> history, IReadOnlyList<int> starThresholds)
    {
        _history = history;
        _thresholds = starThresholds;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var size = CustomMinimumSize;
        DrawRect(new Rect2(Vector2.Zero, size), Back);
        float Y(int reputation) => size.Y - 2 - (size.Y - 4) * reputation / MaxReputation;

        foreach (var t in _thresholds.Skip(1)) DrawLine(new Vector2(0, Y(t)), new Vector2(size.X, Y(t)), Grid);

        var fits = (int)(size.X / DayWidth);
        var shown = _history.TakeLast(fits).ToList();
        if (shown.Count == 0) return;

        var points = shown.Select((d, i) => new Vector2(2 + i * DayWidth, Y(d.Reputation))).ToArray();
        if (points.Length > 1) DrawPolyline(points, Line, 1);
        foreach (var p in points) DrawRect(new Rect2(p - Vector2.One, new Vector2(2, 2)), Line);
    }
}
