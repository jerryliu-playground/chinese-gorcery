using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Customers;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Staff;
using ChineseGrocery.Sim.Time;
using Godot;

namespace ChineseGrocery.Game.Ui;

/// <summary>
/// Top bar (day, time, cash, stage, speed, open button), a left column of tool buttons and a message line under the
/// store (showing the controls when there is nothing to say). Turns Sim
/// events into short translated messages; opens the report when the day closes.
/// </summary>
public partial class Hud : Control
{
    private const double MessageSeconds = 4;

    /// <summary>Width reserved on the left for the tool buttons; the store is drawn to the right of it.</summary>
    public const int ToolColumnWidth = 58;

    private readonly Label _day = new();
    private readonly Label _clock = new();
    private readonly Label _cash = new();
    private readonly Label _stage = new();
    private readonly Label _message = new();
    private readonly Button _open = new() { Text = "ui.hud.open_store" };
    private readonly Button _mode = new();
    private readonly List<Button> _speedButtons = [];
    private readonly List<PanelBase> _panels = [];
    private readonly Queue<string> _messages = new();
    private double _messageAge;
    private bool _showingHint = true;

    private SimHost _host = null!;
    private UiState _ui = null!;
    private BuildPanel _build = null!;
    private ShelfPanel _shelf = null!;
    private ReportPanel _report = null!;

    public void Init(SimHost host, UiState ui)
    {
        _host = host;
        _ui = ui;
        Theme = UiTheme.Create();
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        BuildTopBar();
        BuildBottomBar();

        _message.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        _message.Position = new Vector2(ToolColumnWidth + 4, 342);
        AddChild(_message);

        host.EventsReady += OnEvents;
        host.Ticked += OnTicked;
        ui.Changed += OnUiChanged;
        OnTicked();
    }

    private T AddPanel<T>() where T : PanelBase, new()
    {
        var panel = new T();
        panel.Init(_host, _ui);
        _panels.Add(panel);
        AddChild(panel);
        return panel;
    }

    private void BuildTopBar()
    {
        var bar = new HBoxContainer { Position = new Vector2(4, 0), CustomMinimumSize = new Vector2(632, 18) };
        bar.AddThemeConstantOverride("separation", 10);
        AddChild(bar);
        foreach (var label in new[] { _day, _clock, _cash, _stage })
        {
            label.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
            bar.AddChild(label);
        }

        bar.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        for (var step = 0; step <= _host.Data.Economy.SpeedMultipliers.Count; step++)
        {
            var s = step;
            var b = new Button
            {
                Text = s == 0 ? UiText.T("ui.hud.pause") : UiText.F("ui.hud.speed", ("x", _host.Data.Economy.SpeedMultipliers[s - 1])),
                AutoTranslateMode = AutoTranslateModeEnum.Disabled,
                ToggleMode = true,
            };
            b.Pressed += () =>
            {
                _host.SetSpeedStep(s);
                UpdateSpeedButtons();
            };
            _speedButtons.Add(b);
            bar.AddChild(b);
        }

        _open.Pressed += () => _host.Send(new OpenStore());
        bar.AddChild(_open);
        UpdateSpeedButtons();
    }

    private void BuildBottomBar()
    {
        _build = AddPanel<BuildPanel>();
        _shelf = AddPanel<ShelfPanel>();
        _report = AddPanel<ReportPanel>();
        var order = AddPanel<OrderPanel>();
        var price = AddPanel<PricePanel>();
        var staff = AddPanel<StaffPanel>();
        var upgrade = AddPanel<UpgradePanel>();

        _build.Closed += () =>
        {
            _ui.Mode = ToolMode.Play;
            _ui.ClearTool();
            _ui.Notify();
        };

        var bar = new VBoxContainer { Position = new Vector2(2, 22), CustomMinimumSize = new Vector2(ToolColumnWidth - 4, 0) };
        AddChild(bar);
        _mode.Pressed += () =>
        {
            if (_ui.Mode == ToolMode.Build)
            {
                _build.Close();
                return;
            }

            ShowOnly(_build);
            _ui.Mode = ToolMode.Build;
            _ui.Notify();
        };
        bar.AddChild(_mode);
        bar.AddChild(PanelButton("ui.hud.order", order));
        bar.AddChild(PanelButton("ui.hud.prices", price));
        bar.AddChild(PanelButton("ui.hud.staff", staff));
        bar.AddChild(PanelButton("ui.hud.upgrade", upgrade));
        bar.AddChild(PanelButton("ui.hud.report", _report));
        OnUiChanged();
    }

    private Button PanelButton(string key, PanelBase panel)
    {
        var b = new Button { Text = key };
        b.Pressed += () => ShowOnly(panel);
        return b;
    }

    private void ShowOnly(PanelBase panel)
    {
        foreach (var p in _panels.Where(p => p != panel && p.Visible)) p.Close();
        panel.Open();
    }

    /// <summary>Opens the slots of a clicked shelf or display.</summary>
    public void ShowShelf(int instanceId)
    {
        _ui.SelectedInstanceId = instanceId;
        ShowOnly(_shelf);
    }

    /// <summary>In build mode the panel steps aside while a fixture is being placed or moved, so the floor stays visible.</summary>
    private void OnUiChanged()
    {
        _mode.Text = _ui.Mode == ToolMode.Build ? "ui.hud.play" : "ui.hud.build";
        if (_ui.Mode != ToolMode.Build) return;

        var placing = _ui.PlacingFixtureId is not null || _ui.MovingInstanceId is not null;
        if (placing) _build.Visible = false;
        else _build.Open();
    }

    private void UpdateSpeedButtons()
    {
        for (var i = 0; i < _speedButtons.Count; i++) _speedButtons[i].ButtonPressed = i == _host.SpeedStep;
    }

    private void OnTicked()
    {
        var sim = _host.Sim;
        _day.Text = UiText.F("ui.hud.day", ("day", sim.Clock.Day)) + "  " + UiText.T("ui.phase." + UiText.Snake(sim.Clock.Phase));
        _clock.Text = UiText.ClockTime(sim.Clock.MinuteOfDay);
        _cash.Text = UiText.F("ui.hud.cash", ("cash", UiText.Money(sim.Economy.CashCents)));
        _stage.Text = UiText.T(_host.Data.Stages[sim.Economy.Stage - 1].NameKey);
        _open.Visible = sim.Clock.Phase == DayPhase.PreOpen;
        foreach (var p in _panels) p.RefreshIfOpen();
    }

    private void OnEvents(IReadOnlyList<SimEvent> events)
    {
        foreach (var e in events)
        {
            if (Describe(e) is { } text) _messages.Enqueue(text);

            if (e is DailyReportReady ready)
            {
                _report.Report = ready.Report;
                ShowOnly(_report);
            }

            if (e is StageUpgraded or EmployeeHired && _build.Visible) _build.Open();
        }
    }

    private string? Describe(SimEvent e)
    {
        string ProductName(string id) => UiText.T(_host.Data.Products.Single(p => p.Id == id).NameKey);
        return e switch
        {
            CommandRejected r => UiText.T("ui.reject." + UiText.Snake(r.Reason)),
            ProductSoldOut s => UiText.F("ui.event.sold_out", ("product", ProductName(s.ProductId))),
            CustomerLeft { Exit: CustomerExit.Angry } => UiText.T("ui.event.customer_angry"),
            OrderDelivered d => UiText.F("ui.event.delivered", ("product", ProductName(d.ProductId)), ("qty", d.Quantity)),
            ProductExpired x => UiText.F("ui.event.expired", ("product", ProductName(x.ProductId)), ("qty", x.Quantity)),
            StageUpgraded s => UiText.F("ui.event.stage_upgraded", ("stage", UiText.T(_host.Data.Stages[s.Stage - 1].NameKey))),
            EmployeeHired h => UiText.F("ui.event.hired", ("role", UiText.T(_host.Data.Employees.Roles.Single(r => r.Id == h.RoleId).NameKey))),
            EmployeeLeveledUp l => UiText.F("ui.event.leveled_up", ("level", l.Level)),
            StoreOpened => UiText.T("ui.event.store_opened"),
            _ => null,
        };
    }

    public override void _Process(double delta)
    {
        _messageAge += delta;
        if (_messages.Count > 0 && (_showingHint || _messageAge > 1))
        {
            _message.Text = _messages.Dequeue();
            _message.Modulate = Colors.White;
            _messageAge = 0;
            _showingHint = false;
        }
        else if (_showingHint || _messageAge > MessageSeconds)
        {
            // Nothing new to say: show the controls for the current mode, dimmed.
            _message.Text = UiText.T(_ui.Mode == ToolMode.Build ? "ui.build.hint" : "ui.hud.controls");
            _message.Modulate = new Color(1, 1, 1, 0.6f);
            _showingHint = true;
        }
    }
}
