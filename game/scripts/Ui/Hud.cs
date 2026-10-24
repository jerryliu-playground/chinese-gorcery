using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Customers;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Staff;
using ChineseGrocery.Sim.Tasks;
using ChineseGrocery.Game.View;
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
    private readonly Button _restockAll = new() { Text = "ui.hud.restock_all" };
    private readonly Button _mode = new();
    private readonly Button _language = new() { Text = "ui.hud.language" };
    private readonly List<Button> _speedButtons = [];
    private readonly List<PanelBase> _panels = [];
    private readonly Queue<string> _messages = new();
    private double _messageAge;
    private bool _showingHint = true;

    private SimHost _host = null!;
    private UiState _ui = null!;
    private FeelConfig _feel = null!;
    private readonly Celebration _celebration = new();
    private double _shownCash = double.NaN;
    private BuildPanel _build = null!;
    private ShelfPanel _shelf = null!;
    private ReportPanel _report = null!;

    public void Init(SimHost host, UiState ui, FeelConfig feel)
    {
        _host = host;
        _ui = ui;
        _feel = feel;
        Theme = UiTheme.Create();
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        BuildTopBar();
        BuildBottomBar();

        _message.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        _message.Position = new Vector2(ToolColumnWidth + 4, 342);

        // On a big floor the message line sits over the bottom row, so give it a backdrop.
        _message.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.1f, 0.75f),
            ContentMarginLeft = 3, ContentMarginRight = 3,
        });
        AddChild(_message);

        AddChild(_celebration);
        host.EventsReady += OnEvents;
        host.Ticked += OnTicked;
        host.GameReplaced += OnGameReplaced;
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

        _restockAll.Pressed += () => _host.Send(new RestockAll());
        bar.AddChild(_restockAll);
        _open.Pressed += () => _host.Send(new OpenStore());
        bar.AddChild(_open);
        _language.Pressed += () =>
        {
            Language.Toggle();
            Retranslate();
        };
        bar.AddChild(_language);
        UpdateSpeedButtons();
    }

    /// <summary>Text set from translated strings does not follow a locale change by itself, so redo it.</summary>
    private void Retranslate()
    {
        for (var step = 0; step < _speedButtons.Count; step++)
        {
            _speedButtons[step].Text = step == 0
                ? UiText.T("ui.hud.pause")
                : UiText.F("ui.hud.speed", ("x", _host.Data.Economy.SpeedMultipliers[step - 1]));
        }

        OnTicked();
        foreach (var panel in _panels.Where(p => p.Visible)) panel.Open();
        _messages.Clear();
        _showingHint = true;
    }

    private void OnGameReplaced()
    {
        _shownCash = double.NaN;
        foreach (var p in _panels.Where(p => p.Visible)) p.Close();
        _ui.Mode = ToolMode.Play;
        _ui.ClearTool();
        _ui.SelectedInstanceId = null;
        _ui.Notify();
        OnTicked();
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

        var save = new Button { Text = "ui.hud.save" };
        save.Pressed += () => _messages.Enqueue(UiText.T(_host.SaveToSlot() ? "ui.event.saved" : "ui.event.save_failed"));
        bar.AddChild(save);
        var load = new Button { Text = "ui.hud.load" };
        load.Pressed += () =>
        {
            var key = !_host.HasSave ? "ui.event.no_save" : _host.LoadFromSlot() ? "ui.event.loaded" : "ui.event.load_failed";
            _messages.Enqueue(UiText.T(key));
        };
        bar.AddChild(load);
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

    public void CloseAllPanels()
    {
        foreach (var p in _panels.Where(p => p.Visible)) p.Close();
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
        UpdateCashLabel(0);
        _stage.Text = UiText.T(_host.Data.Stages[sim.Economy.Stage - 1].NameKey);
        _open.Visible = sim.Clock.Phase == DayPhase.PreOpen;
        _restockAll.Visible = sim.Clock.Phase == DayPhase.PreOpen;
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
            if (e is StageUpgraded up)
            {
                var stage = UiText.T(_host.Data.Stages[up.Stage - 1].NameKey);
                _celebration.Play(UiText.F("ui.celebrate.upgraded", ("stage", stage)), _feel.CelebrationSeconds, _feel.ConfettiCount);
            }
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
            ShelvesRestocked s => UiText.F("ui.event.restocked", ("units", s.Units)),
            GoodsReturned g => UiText.F("ui.event.returned", ("product", ProductName(g.ProductId)), ("qty", g.Quantity)),
            _ => null,
        };
    }

    /// <summary>What the player carries and where it should go, or null when their hands are empty.</summary>
    private string? CarryHint()
    {
        var sim = _host.Sim;
        if (sim.Player.Step != WorkStep.Carrying || sim.Player.TaskId is not { } taskId || sim.Tasks.Find(taskId) is not { } task) return null;

        var lot = sim.Player.Carrying[0];
        var product = UiText.T(_host.Data.Products.Single(p => p.Id == lot.ProductId).NameKey);
        string FixtureName(int id) => UiText.T(_host.Data.Fixtures.Single(f => f.Id == sim.Layout.Find(id)!.FixtureId).NameKey);
        return task switch
        {
            RestockTask r => UiText.F("ui.carry.restock",
                ("qty", sim.Player.Carrying.Sum(l => l.Quantity)), ("product", product), ("fixture", FixtureName(r.Target.FixtureInstanceId)), ("n", r.Target.Slot + 1)),
            CutMeatTask c => UiText.F("ui.carry.cut", ("product", product), ("fixture", FixtureName(c.CuttingTableInstanceId))),
            _ => null,
        };
    }

    /// <summary>The cash figure counts toward the real amount over a moment instead of jumping.</summary>
    private void UpdateCashLabel(double delta)
    {
        var target = (double)_host.Sim.Economy.CashCents;
        if (double.IsNaN(_shownCash) || delta <= 0 && Math.Abs(target - _shownCash) > 1e6) _shownCash = target;

        var rate = _feel.CashRollSeconds <= 0 ? 1 : 1 - Math.Exp(-delta * 5 / _feel.CashRollSeconds);
        _shownCash += (target - _shownCash) * rate;
        if (Math.Abs(target - _shownCash) < 1) _shownCash = target;
        _cash.Text = UiText.F("ui.hud.cash", ("cash", UiText.Money((long)Math.Round(_shownCash))));
    }

    public override void _Process(double delta)
    {
        UpdateCashLabel(delta);
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
            // Nothing new to say: show what the player is carrying, or the controls for the current mode, dimmed.
            _message.Text = CarryHint() ?? UiText.T(_ui.Mode == ToolMode.Build ? "ui.build.hint" : "ui.hud.controls");
            _message.Modulate = new Color(1, 1, 1, 0.6f);
            _showingHint = true;
        }
    }
}
