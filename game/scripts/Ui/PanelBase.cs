using Godot;

namespace ChineseGrocery.Game.Ui;

/// <summary>A titled, closable, scrollable window centred on screen. Subclasses fill <see cref="Body"/> in <see cref="Rebuild"/>.</summary>
public abstract partial class PanelBase : PanelContainer
{
    private const float MaxBodyHeight = 250;

    private readonly Label _title = new();

    protected PanelBase()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.12f, 0.15f, 0.97f),
            BorderColor = new Color(0.45f, 0.42f, 0.36f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            ContentMarginLeft = 6, ContentMarginTop = 4, ContentMarginRight = 6, ContentMarginBottom = 6,
        });

        var outer = new VBoxContainer();
        AddChild(outer);

        var header = new HBoxContainer();
        outer.AddChild(header);
        _title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(_title);
        var close = new Button { Text = "ui.panel.close" };
        close.Pressed += Close;
        header.AddChild(close);

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(300, 0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        outer.AddChild(scroll);
        Body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(Body);
        Scroll = scroll;

        // Actions that must stay in reach however long the list above gets.
        Footer = new HBoxContainer();
        outer.AddChild(Footer);
    }

    protected SimHost Host { get; private set; } = null!;
    protected UiState Ui { get; private set; } = null!;
    protected VBoxContainer Body { get; }
    protected HBoxContainer Footer { get; }
    private ScrollContainer Scroll { get; }

    public event Action? Closed;

    public void Init(SimHost host, UiState ui)
    {
        Host = host;
        Ui = ui;
    }

    public void Open()
    {
        Rebuild();
        Visible = true;
    }

    public void Close()
    {
        Visible = false;
        Closed?.Invoke();
    }

    /// <summary>Refreshes contents if the panel is open (called after ticks and events).</summary>
    public void RefreshIfOpen()
    {
        if (Visible) Refresh();
    }

    /// <summary>Rebuilds everything. Default refresh does the same; panels with inputs override to keep what the player typed.</summary>
    protected abstract void Rebuild();

    protected virtual void Refresh() { }

    protected void SetTitle(string text) => _title.Text = text;

    protected void ClearBody()
    {
        foreach (var child in Body.GetChildren().Concat(Footer.GetChildren()))
        {
            child.GetParent().RemoveChild(child);
            child.QueueFree();
        }
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;

        var viewport = GetViewportRect().Size;
        Scroll.CustomMinimumSize = new Vector2(Scroll.CustomMinimumSize.X, Math.Min(MaxBodyHeight, Body.GetCombinedMinimumSize().Y));
        Size = GetCombinedMinimumSize();
        Position = ((viewport - Size) / 2).Floor();
    }

    protected static HBoxContainer Row(params Control[] cells)
    {
        var row = new HBoxContainer();
        foreach (var c in cells) row.AddChild(c);
        return row;
    }

    /// <summary>A label that keeps its text as-is (already translated or a number).</summary>
    protected static Label Text(string text, float minWidth = 0) =>
        new() { Text = text, AutoTranslateMode = AutoTranslateModeEnum.Disabled, CustomMinimumSize = new Vector2(minWidth, 0) };

    protected static Button ActionButton(string key, Action onPressed, bool enabled = true)
    {
        var b = new Button { Text = key, Disabled = !enabled };
        b.Pressed += onPressed;
        return b;
    }
}
