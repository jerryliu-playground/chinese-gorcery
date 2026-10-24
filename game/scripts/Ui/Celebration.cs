using Godot;

namespace ChineseGrocery.Game.Ui;

/// <summary>A short full-screen flash and confetti burst with a big line of text (used for stage upgrades).</summary>
public partial class Celebration : Control
{
    private static readonly Color[] Colours =
    [
        new(0.9f, 0.2f, 0.18f), new(0.96f, 0.76f, 0.3f), new(0.35f, 0.75f, 0.4f), new(0.4f, 0.6f, 0.95f), new(1f, 0.96f, 0.9f),
    ];

    private readonly List<(Vector2 Pos, Vector2 Vel, Color Color, float Spin)> _bits = [];
    private readonly Label _text = new() { AutoTranslateMode = AutoTranslateModeEnum.Disabled, HorizontalAlignment = HorizontalAlignment.Center };
    private float _age;
    private float _life;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        _text.SetAnchorsPreset(LayoutPreset.Center);
        _text.AddThemeFontSizeOverride("font_size", 24);
        _text.AddThemeColorOverride("font_outline_color", new Color(0.3f, 0.05f, 0.05f));
        _text.AddThemeConstantOverride("outline_size", 4);
        AddChild(_text);
        Visible = false;
    }

    public void Play(string text, float seconds, int confetti)
    {
        var rng = new RandomNumberGenerator();
        var size = GetViewportRect().Size;
        _bits.Clear();
        for (var i = 0; i < confetti; i++)
        {
            _bits.Add((new Vector2(rng.RandfRange(0, size.X), rng.RandfRange(-60, 0)),
                new Vector2(rng.RandfRange(-30, 30), rng.RandfRange(60, 160)), Colours[rng.RandiRange(0, Colours.Length - 1)], rng.RandfRange(0, 6)));
        }

        _text.Text = text;
        _text.Position = new Vector2((size.X - 400) / 2, size.Y / 2 - 40);
        _text.Size = new Vector2(400, 40);
        _age = 0;
        _life = seconds;
        Visible = true;
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;

        _age += (float)delta;
        if (_age >= _life)
        {
            Visible = false;
            return;
        }

        for (var i = 0; i < _bits.Count; i++)
        {
            var b = _bits[i];
            _bits[i] = (b.Pos + b.Vel * (float)delta, b.Vel, b.Color, b.Spin + (float)delta * 8);
        }

        _text.Modulate = Colors.White with { A = Mathf.Clamp((_life - _age) * 2, 0, 1) };
        QueueRedraw();
    }

    public override void _Draw()
    {
        var flash = Mathf.Clamp(1 - _age / 0.3f, 0, 1) * 0.5f;
        if (flash > 0) DrawRect(GetViewportRect(), new Color(1, 0.95f, 0.8f, flash));

        foreach (var (pos, _, color, spin) in _bits)
        {
            var w = 2 + Mathf.Abs(Mathf.Sin(spin)) * 3;
            DrawRect(new Rect2(pos, new Vector2(w, 3)), color);
        }
    }
}
