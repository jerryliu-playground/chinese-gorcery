using ChineseGrocery.Game.Ui;
using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Customers;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Meat;
using Godot;

namespace ChineseGrocery.Game.View;

public enum BubbleKind
{
    Question,
    Frown,
    Angry,
    Heart,
    Speech,
}

/// <summary>
/// Short-lived feedback drawn over the store: floating money, customer bubbles, popping cuts, and the squash /
/// bounce / flash state the store view applies to fixtures. Driven by Sim events; never touches Sim state.
/// Time runs with the game speed and stops while paused.
/// </summary>
public partial class Effects : Node2D
{
    private const int Cell = StoreView.Cell;

    private readonly List<Pop> _pops = [];
    private readonly Dictionary<int, (float Age, float Life, bool Placed, bool Flash)> _fixtures = [];
    private SimHost _host = null!;
    private FeelConfig _feel = null!;
    private StoreView _store = null!;
    private Font _font = null!;

    /// <summary>Game-speed-scaled seconds since start; drives looping animations such as chopping.</summary>
    public float Clock { get; private set; }

    private abstract record Pop(float Life)
    {
        public float Age { get; set; }
    }

    private sealed record FloatingText(Vector2 Start, string Text, Color Color, float Life) : Pop(Life);

    private sealed record Bubble(int CustomerId, BubbleKind Kind, string? Text, float Life) : Pop(Life);

    private sealed record CutIcons(Vector2 Start, IReadOnlyList<string> ProductIds, float Life) : Pop(Life);

    public void Init(SimHost host, FeelConfig feel, StoreView store)
    {
        _host = host;
        _feel = feel;
        _store = store;
        _font = UiTheme.Font();
        ZIndex = 10;
        host.EventsReady += OnEvents;
        host.GameReplaced += () =>
        {
            _pops.Clear();
            _fixtures.Clear();
        };
    }

    private float Speed => _host.SpeedMultiplier;

    /// <summary>Hearts, chatter and shelf flashes are left out at high speed.</summary>
    public bool MinorEffectsOn => Speed <= _feel.MinorEffectsMaxSpeed;

    public void Say(int customerId, string text) => _pops.Add(new Bubble(customerId, BubbleKind.Speech, text, _feel.SpeechSeconds));

    public bool IsSpeaking(int customerId) => _pops.OfType<Bubble>().Any(b => b.CustomerId == customerId);

    private void OnEvents(IReadOnlyList<SimEvent> events)
    {
        foreach (var e in events)
        {
            switch (e)
            {
                case CheckoutCompleted c:
                    _pops.Add(new FloatingText(_store.FixtureTop(c.CheckoutInstanceId), "+" + UiText.Money(c.RevenueCents), new Color(0.35f, 0.95f, 0.4f), _feel.FloatTextSeconds));
                    break;
                case ItemsShelved s:
                    _fixtures[s.Slot.FixtureInstanceId] = (0, _feel.ShelfBounceSeconds, false, s.WasEmpty && MinorEffectsOn);
                    break;
                case Sim.Grid.FixturePlaced p:
                    _fixtures[p.InstanceId] = (0, _feel.PlaceBounceSeconds, true, false);
                    break;
                case MeatCut m:
                    _pops.Add(new CutIcons(_store.FixtureTop(m.CuttingTableInstanceId), m.Outputs.Select(o => o.ProductId).ToList(), _feel.CutPopSeconds));
                    break;
                case CustomerReaction r:
                    var kind = r.Kind switch
                    {
                        CustomerReactionKind.NotFound => BubbleKind.Question,
                        CustomerReactionKind.TooExpensive => BubbleKind.Frown,
                        CustomerReactionKind.GaveUp => BubbleKind.Angry,
                        _ => BubbleKind.Heart,
                    };
                    if (kind == BubbleKind.Heart && (!MinorEffectsOn || GD.Randf() >= _feel.HeartChance)) break;

                    _pops.RemoveAll(p => p is Bubble b && b.CustomerId == r.CustomerId);
                    _pops.Add(new Bubble(r.CustomerId, kind, null, _feel.BubbleSeconds));
                    break;
            }
        }
    }

    /// <summary>Scale, vertical offset and flash (0-1) to draw a fixture with right now.</summary>
    public (Vector2 Scale, float Lift, float Flash) FixtureFeel(int instanceId)
    {
        if (!_fixtures.TryGetValue(instanceId, out var f)) return (Vector2.One, 0, 0);

        var t = Mathf.Clamp(f.Age / f.Life, 0, 1);
        if (f.Placed)
        {
            // Drops in from a little above, then squashes on landing.
            var lift = t < 0.5f ? -(1 - t * 2) * 10 : 0;
            var squash = t < 0.5f ? 0 : Mathf.Sin((t - 0.5f) * 2 * Mathf.Pi) * _feel.ShelfBounceSquash;
            return (new Vector2(1 + squash, 1 - squash), lift, 0);
        }

        var s = Mathf.Sin(t * Mathf.Pi) * _feel.ShelfBounceSquash;
        var flash = f.Flash ? Mathf.Clamp(1 - f.Age / Mathf.Max(0.01f, _feel.ShelfFlashSeconds), 0, 1) : 0;
        return (new Vector2(1 + s, 1 - s), 0, flash);
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta * Speed;
        Clock += dt;
        foreach (var p in _pops) p.Age += dt;
        _pops.RemoveAll(p => p.Age >= p.Life);
        foreach (var id in _fixtures.Keys.ToList())
        {
            var f = _fixtures[id];
            f.Age += dt;
            if (f.Age >= Mathf.Max(f.Life, f.Flash ? _feel.ShelfFlashSeconds : 0)) _fixtures.Remove(id);
            else _fixtures[id] = f;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var pop in _pops)
        {
            var t = pop.Age / pop.Life;
            switch (pop)
            {
                case FloatingText f:
                    var at = f.Start + new Vector2(0, -t * _feel.FloatTextRise);
                    DrawOutlinedText(at, f.Text, f.Color with { A = 1 - t * t });
                    break;
                case CutIcons c:
                    for (var i = 0; i < c.ProductIds.Count; i++)
                    {
                        var x = (i - (c.ProductIds.Count - 1) / 2f) * 12;
                        _store.DrawIconAt(this, c.ProductIds[i], c.Start + new Vector2(x, -t * 16 - 4), 12, 1 - t * t);
                    }

                    break;
                case Bubble b when _store.CustomerHead(b.CustomerId) is { } head:
                    DrawBubble(head, b, Mathf.Clamp(Mathf.Min(t * 6, (1 - t) * 6), 0, 1));
                    break;
            }
        }
    }

    private void DrawOutlinedText(Vector2 at, string text, Color color)
    {
        var size = _font.GetStringSize(text, HorizontalAlignment.Left, -1, UiTheme.FontSize);
        var origin = at - new Vector2(size.X / 2, 0);
        var outline = new Color(0.1f, 0.08f, 0.08f, color.A);
        foreach (var o in new[] { new Vector2(-1, 0), new Vector2(1, 0), new Vector2(0, -1), new Vector2(0, 1) })
        {
            DrawString(_font, origin + o, text, HorizontalAlignment.Left, -1, UiTheme.FontSize, outline);
        }

        DrawString(_font, origin, text, HorizontalAlignment.Left, -1, UiTheme.FontSize, color);
    }

    private void DrawBubble(Vector2 head, Bubble b, float alpha)
    {
        var paper = new Color(1, 0.98f, 0.92f, alpha);
        var ink = new Color(0.2f, 0.14f, 0.12f, alpha);
        var width = b.Kind == BubbleKind.Speech && b.Text is { } text
            ? _font.GetStringSize(text, HorizontalAlignment.Left, -1, UiTheme.FontSize).X + 8
            : 16f;
        var box = new Rect2(head + new Vector2(-width / 2, -16), new Vector2(width, 14));
        DrawRect(box, b.Kind == BubbleKind.Angry ? new Color(0.85f, 0.2f, 0.18f, alpha) : paper);
        DrawRect(box, ink, filled: false, width: 1);
        DrawColoredPolygon([head + new Vector2(-2, -2.5f), head + new Vector2(3, -2.5f), head + new Vector2(0, 1)], paper);

        var c = box.GetCenter();
        switch (b.Kind)
        {
            case BubbleKind.Question:
                DrawString(_font, c + new Vector2(-3, 5), "?", HorizontalAlignment.Left, -1, UiTheme.FontSize, ink);
                break;
            case BubbleKind.Frown:
                DrawCircle(c, 5, new Color(0.98f, 0.82f, 0.35f, alpha));
                DrawRect(new Rect2(c + new Vector2(-3, -2), new Vector2(1, 2)), ink);
                DrawRect(new Rect2(c + new Vector2(2, -2), new Vector2(1, 2)), ink);
                DrawArc(c + new Vector2(0, 4), 2.5f, Mathf.Pi * 1.15f, Mathf.Pi * 1.85f, 6, ink, 1);
                break;
            case BubbleKind.Angry:
                DrawString(_font, c + new Vector2(-5, 5), "!!", HorizontalAlignment.Left, -1, UiTheme.FontSize, Colors.White with { A = alpha });
                break;
            case BubbleKind.Heart:
                var red = new Color(0.9f, 0.2f, 0.3f, alpha);
                DrawCircle(c + new Vector2(-2, -1), 2.5f, red);
                DrawCircle(c + new Vector2(2, -1), 2.5f, red);
                DrawColoredPolygon([c + new Vector2(-4.5f, 0), c + new Vector2(4.5f, 0), c + new Vector2(0, 4.5f)], red);
                break;
            case BubbleKind.Speech:
                DrawString(_font, new Vector2(box.Position.X + 4, c.Y + 5), b.Text ?? "", HorizontalAlignment.Left, -1, UiTheme.FontSize, ink);
                break;
        }
    }
}
