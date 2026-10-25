using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Customers;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Reputation;
using ChineseGrocery.Sim.Staff;
using ChineseGrocery.Sim.Story;
using ChineseGrocery.Sim.Tasks;
using ChineseGrocery.Game.View;
using ChineseGrocery.Sim.Time;
using Godot;

namespace ChineseGrocery.Game.Ui;

/// <summary>
/// Top bar (day, time, cash, stage, speed, open button), the reputation stars and this season's trend under it, a left
/// column of tool buttons and a message line under the store (showing the controls when there is nothing to say). Turns
/// Sim events into short translated messages; opens the report when the day closes.
/// </summary>
public partial class Hud : Control
{
    private const double MessageSeconds = 4;

    /// <summary>Width reserved on the left for the tool buttons; the store is drawn to the right of it.</summary>
    public const int ToolColumnWidth = 58;

    private readonly Label _day = new();
    private readonly Label _date = new();
    private readonly Button _calendarButton = new() { TooltipText = "ui.hud.calendar", Flat = true };
    private readonly Label _clock = new();
    private readonly Label _cash = new();
    private readonly Label _ships = new();
    private readonly Label _rating = new();
    private readonly Label _trend = new();
    private PanelContainer _infoPanel = null!;
    private readonly Button _groupOrders = new() { Text = "ui.hud.group_orders" };
    private readonly Label _message = new();
    private readonly Label _shopName = new() { AutoTranslateMode = AutoTranslateModeEnum.Disabled };
    private readonly Label _objective = new() { AutoTranslateMode = AutoTranslateModeEnum.Disabled };
    private readonly Label _loan = new() { AutoTranslateMode = AutoTranslateModeEnum.Disabled };
    private readonly HBoxContainer _loanBox = new();
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
    private MenuPanel _menu = null!;
    private ShelfPanel _shelf = null!;
    private ReportPanel _report = null!;
    private CalendarPanel _calendar = null!;
    private NoticePanel _notices = null!;
    private QuarterReportPanel _quarter = null!;

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

        // Just under the top bar, after the shop name: the reputation stars and score, then this season's trend.
        var info = new PanelContainer { Position = new Vector2(ToolColumnWidth + 4, 19), MouseFilter = MouseFilterEnum.Ignore };
        info.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.1f, 0.75f),
            ContentMarginLeft = 3, ContentMarginRight = 3,
        });
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        info.AddChild(row);
        row.AddChild(new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://assets/sprites/ui_star.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(12, 12),
            TextureFilter = TextureFilterEnum.Nearest,
        });
        foreach (var label in new[] { _rating, _trend })
        {
            label.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
            row.AddChild(label);
        }

        _rating.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.4f));
        _infoPanel = info;

        AddChild(_celebration);
        BuildStoryCorner();

        // Last, so it covers everything else.
        Dialogue.Init(host, feel, new Art());
        AddChild(Dialogue);
        host.EventsReady += OnEvents;
        host.Ticked += OnTicked;
        host.GameReplaced += OnGameReplaced;
        ui.Changed += OnUiChanged;
        OnTicked();
    }

    /// <summary>The conversation on screen, if any (story, tutorial calls).</summary>
    public DialogueBox Dialogue { get; } = new() { Name = "Dialogue" };

    /// <summary>
    /// Under the top bar: the shop's name, the tutorial reminder, reputation and trend, and imports at sea on the left; the
    /// student loan on the right.
    /// </summary>
    private void BuildStoryCorner()
    {
        var backdrop = new StyleBoxFlat { BgColor = new Color(0.08f, 0.08f, 0.1f, 0.75f), ContentMarginLeft = 3, ContentMarginRight = 3 };
        var left = new HBoxContainer { Position = new Vector2(ToolColumnWidth + 4, 20), MouseFilter = MouseFilterEnum.Ignore };
        left.AddThemeConstantOverride("separation", 6);
        _shopName.AddThemeStyleboxOverride("normal", backdrop);
        _shopName.AddThemeColorOverride("font_color", new Color(0.95f, 0.55f, 0.45f));
        left.AddChild(_shopName);
        _objective.AddThemeStyleboxOverride("normal", backdrop);
        _objective.AddThemeColorOverride("font_color", new Color(0.98f, 0.86f, 0.5f));
        left.AddChild(_objective);
        left.AddChild(_infoPanel);
        _ships.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        _ships.AddThemeStyleboxOverride("normal", backdrop);
        left.AddChild(_ships);
        AddChild(left);

        _loan.AddThemeStyleboxOverride("normal", backdrop);
        _loanBox.AddChild(_loan);
        var loanPanel = AddPanel<LoanPanel>();
        var pay = new Button { Text = "ui.hud.loan_pay" };
        pay.Pressed += () => ShowOnly(loanPanel);
        _loanBox.AddChild(pay);
        AddChild(_loanBox);
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
        _calendarButton.Icon = new Game.View.Art().Get("ui_calendar");
        _calendarButton.ExpandIcon = false;
        _calendarButton.Pressed += () => ShowOnly(_calendar);
        bar.AddChild(_calendarButton);
        foreach (var label in new[] { _date, _day, _clock, _cash })
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
    public void Retranslate()
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
        _notices.Clear();
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
        var settings = AddPanel<SettingsPanel>();
        var reviews = AddPanel<ReviewsPanel>();
        var groupOrders = AddPanel<GroupOrderPanel>();
        _menu = AddPanel<MenuPanel>();
        _calendar = AddPanel<CalendarPanel>();
        _quarter = AddPanel<QuarterReportPanel>();
        _notices = AddPanel<NoticePanel>();
        _report.OrderRequested += () => ShowOnly(order);
        _report.Closed += () => _host.Send(new ReportViewed());
        _report.SeasonReportRequested += q =>
        {
            _quarter.Report = q;
            _quarter.Open();
        };

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
        bar.AddChild(PanelButton("ui.hud.menu", _menu));
        bar.AddChild(PanelButton("ui.hud.upgrade", upgrade));
        bar.AddChild(PanelButton("ui.hud.report", _report));
        bar.AddChild(PanelButton("ui.hud.reviews", reviews));
        _groupOrders.Pressed += () => ShowOnly(groupOrders);
        bar.AddChild(_groupOrders);

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
        bar.AddChild(PanelButton("ui.hud.settings", settings));
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

    /// <summary>Refreshes the bars now (the screenshot tour ticks the Sim directly, without the host's tick event).</summary>
    public void RefreshNow() => OnTicked();

    /// <summary>The cash figure as currently shown (it counts toward the real amount).</summary>
    public string CashText => _cash.Text;

    /// <summary>Opens the report panel on a given day's report.</summary>
    public void ShowReport(DailyReport report)
    {
        _report.Report = report;
        ShowOnly(_report);
    }

    public void ShowPanel<T>() where T : PanelBase => ShowOnly(_panels.OfType<T>().Single());

    /// <summary>Pops up a festival reminder or event notice (the tour uses it for notices from skipped time).</summary>
    public void ShowNotice(SimEvent notice)
    {
        _notices.Add(notice);
        ShowOnly(_notices);
    }

    public void ShowSeasonReport(Sim.Economy.QuarterReport report)
    {
        _quarter.Report = report;
        ShowOnly(_quarter);
    }

    public T Panel<T>() where T : PanelBase => _panels.OfType<T>().Single();

    public void CloseAllPanels()
    {
        foreach (var p in _panels.Where(p => p.Visible)) p.Close();
    }

    /// <summary>Opens the slots of a clicked shelf or display, or the menu for a clicked deli station.</summary>
    public void ShowShelf(int instanceId)
    {
        _ui.SelectedInstanceId = instanceId;
        var fixture = _host.Sim.Layout.Find(instanceId);
        var isStation = fixture is not null && Sim.Data.DataValidator.IsDeliStation(_host.Data.Fixtures.Single(f => f.Id == fixture.FixtureId).Kind);
        ShowOnly(isStation ? _menu : _shelf);
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
        // With the calendar the date already says which day it is; the bar is too narrow for both.
        var phase = UiText.T("ui.phase." + UiText.Snake(sim.Clock.Phase));
        _day.Text = sim.Date is null ? UiText.F("ui.hud.day", ("day", sim.Clock.Day)) + "  " + phase : phase;
        _date.Text = sim.Date is { } date
            ? UiText.F("ui.hud.date", ("season", UiText.T(_host.Data.Calendar!.Seasons[date.SeasonIndex].NameKey)), ("day", date.DayOfSeason),
                ("weekday", UiText.T("ui.weekday.short." + sim.Clock.Weekday)))
            : "";
        _clock.Text = UiText.ClockTime(sim.Clock.MinuteOfDay);
        UpdateCashLabel(0);
        _ships.Visible = sim.Shipments.Count > 0 || sim.Pallets.Count > 0;
        _ships.Text = sim.Shipments.Count > 0
            ? UiText.F("ui.hud.ships", ("count", sim.Shipments.Count), ("days", sim.Shipments.Min(s => s.ArrivalDay) - sim.Clock.Day))
            : UiText.F("ui.hud.pallets", ("count", sim.Pallets.Count));
        UpdateStoryCorner();
        _rating.Text = UiText.F("ui.hud.rating", ("stars", ReviewsPanel.StarText(sim.Stars)), ("rep", sim.Reputation));
        _trend.Text = ReviewsPanel.TrendText(_host) ?? "";
        // The tutorial reminder needs the room while it shows; the trend is also in the reviews panel.
        _trend.Visible = _trend.Text.Length > 0 && !_objective.Visible;

        // A waiting offer lights up the group order button.
        _groupOrders.Modulate = sim.GroupOrders.Any(o => o.Status == GroupOrderStatus.Offered) ? new Color(1f, 0.85f, 0.4f) : Colors.White;
        _open.Visible = sim.Clock.Phase == DayPhase.PreOpen;
        _restockAll.Visible = sim.Clock.Phase == DayPhase.PreOpen;
        foreach (var p in _panels) p.RefreshIfOpen();
    }

    private void UpdateStoryCorner()
    {
        _shopName.Visible = _host.Data.Story is not null;
        if (_host.Data.Story is not null) _shopName.Text = Dialogue.ShopName();
        var step = _host.Sim.ActiveTutorialStep;
        _objective.Visible = step is not null;
        if (step is not null) _objective.Text = UiText.T(step.ObjectiveKey);

        _loanBox.Visible = _host.Sim.Loan is not null;
        if (_host.Sim.Loan is not { } loan) return;
        _loan.Text = loan.PaidOff ? UiText.T("ui.hud.loan_paid_off") : UiText.F("ui.hud.loan", ("amount", UiText.Money(loan.BalanceCents)));
        _loanBox.Size = _loanBox.GetCombinedMinimumSize();
        _loanBox.Position = new Vector2(640 - 4 - _loanBox.Size.X, 20);
    }

    private void OnEvents(IReadOnlyList<SimEvent> events)
    {
        foreach (var e in events)
        {
            if (Describe(e) is { } text) _messages.Enqueue(text);

            if (e is Sim.Calendar.FestivalReminder or Sim.Calendar.StoreEventAnnounced)
            {
                _notices.Add(e);
                if (!_notices.Visible) _notices.Open();
            }

            if (e is DailyReportReady ready)
            {
                _report.Report = ready.Report;
                ShowOnly(_report);
            }

            if (e is StageUpgraded or EmployeeHired && _build.Visible) _build.Open();
            if (e is LoanPaidOff) _celebration.Play(UiText.T("ui.celebrate.loan_paid_off"), _feel.CelebrationSeconds, _feel.ConfettiCount);
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
            LoanPaymentMade { ArrearsCents: > 0 } l => UiText.F("ui.event.loan_short", ("paid", UiText.Money(l.PaidCents)), ("due", UiText.Money(l.ArrearsCents))),
            LoanPaymentMade l => UiText.F(l.Scheduled ? "ui.event.loan_paid" : "ui.event.loan_paid_extra", ("paid", UiText.Money(l.PaidCents))),
            TutorialStepCompleted => UiText.T("ui.event.tutorial_step_done"),
            GroupOrderOffered => UiText.T("ui.event.group_offered"),
            GroupOrderAccepted a => UiText.F("ui.event.group_accepted", ("day", a.DueDay)),
            GroupOrderClosed { Outcome: GroupOrderOutcome.Delivered } c => UiText.F("ui.event.group_delivered", ("money", UiText.Money(c.PaymentCents))),
            GroupOrderClosed { Outcome: GroupOrderOutcome.Failed } => UiText.T("ui.event.group_failed"),
            GroupOrderClosed { Outcome: GroupOrderOutcome.Expired } => UiText.T("ui.event.group_expired"),
            BilingualLabelsBought => UiText.T("ui.event.labels_bought"),
            TrendsPicked => ReviewsPanel.TrendText(_host) is { } trend ? UiText.F("ui.event.new_season", ("trend", trend)) : null,
            ReviewsPosted p when p.Reviews.Count > 0 => UiText.F("ui.event.reviews", ("count", p.Reviews.Count)),
            ReputationChanged c when c.ReasonKey != "reputation.reason.customers" && c.After != c.Before =>
                UiText.F("ui.event.reputation", ("reason", UiText.T(c.ReasonKey)), ("change", (c.After - c.Before).ToString("+0;-0", System.Globalization.CultureInfo.InvariantCulture))),
            Sim.Calendar.InspectorArrived => UiText.T("ui.event.inspector_arrived"),
            Sim.Calendar.InspectionFinished { Findings.Passed: true, CashCents: 0 } => UiText.T("ui.event.inspection_passed_plain"),
            Sim.Calendar.InspectionFinished { Findings.Passed: true } i => UiText.F("ui.event.inspection_passed", ("cash", UiText.Money(i.CashCents))),
            Sim.Calendar.InspectionFinished i => UiText.F("ui.event.inspection_failed", ("cash", UiText.Money(-i.CashCents)), ("problems", string.Join(", ",
                new[] { (i.Findings.SpoiledGoods, "ui.inspection.spoiled"), (i.Findings.DirtyWater, "ui.inspection.water"), (i.Findings.MessyStands, "ui.inspection.messy") }
                    .Where(x => x.Item1).Select(x => UiText.T(x.Item2))))),
            Sim.Calendar.PowerCutStarted p => UiText.T(p.Generator ? "ui.event.power_out_generator" : "ui.event.power_out"),
            Sim.Calendar.PowerRestored => UiText.T("ui.event.power_back"),
            Sim.Calendar.ClearanceBought c => UiText.F("ui.event.clearance_bought", ("qty", c.Units), ("product", ProductName(c.ProductId))),
            Sim.Calendar.GroupOrderFinished { Delivered: true } g => UiText.F("ui.event.group_order_done", ("revenue", UiText.Money(g.RevenueCents))),
            Sim.Calendar.GroupOrderFinished => UiText.T("ui.event.group_order_missed"),
            Sim.Calendar.DecorationBought d => UiText.F("ui.event.decoration_bought", ("name", UiText.T(_host.Data.Festivals!.Decorations.Single(x => x.Id == d.DecorationId).NameKey))),

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
