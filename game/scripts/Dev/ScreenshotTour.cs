using ChineseGrocery.Game.Ui;
using ChineseGrocery.Game.View;
using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Save;
using ChineseGrocery.Sim.Staff;
using ChineseGrocery.Sim.Tasks;
using ChineseGrocery.Sim.Time;
using Godot;

namespace ChineseGrocery.Game.Dev;

/// <summary>
/// Developer tool, not part of play: launched with <c>--screenshot-tour=DIR</c>, it walks through scripted scenes using
/// ordinary Sim commands and saves a screenshot of each to DIR, then quits. Used to check visual changes (CLAUDE.md).
/// Needs a rendering run (not --headless).
/// </summary>
public partial class ScreenshotTour : Node
{
    private readonly Queue<(string Name, Action Setup, int Frames)> _steps = new();
    private string _dir = "";
    private int _wait;
    private string? _pending;

    public SimHost Host { get; set; } = null!;
    public UiState Ui { get; set; } = null!;
    public StoreView Store { get; set; } = null!;
    public Hud Hud { get; set; } = null!;

    public static string? RequestedDir() =>
        OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--screenshot-tour=", StringComparison.Ordinal))?.Split('=', 2)[1];

    public override void _Ready()
    {
        _dir = RequestedDir()!;
        DirAccess.MakeDirRecursiveAbsolute(_dir);
        TranslationServer.SetLocale("zh");
        Plan();
    }

    private void Step(string name, Action setup, int frames = 6) => _steps.Enqueue((name, setup, frames));

    private GameSim Sim => Host.Sim;

    private void Plan()
    {
        Step("01-day1-empty-store", () => { });
        Step("02-day2-backroom-full", PrepareDayTwo);
        Step("03-one-click-shelving", () => Send(new RestockAll()));
        Step("04-open-with-customers", () =>
        {
            WalkTo(new GridPoint(2, 4));
            Send(new OpenStore());
            Ticks(500);
        }, frames: 12);
        Step("05-build-ghost-fits", () => Ghost("shelf", new GridPoint(7, 3)));
        Step("06-build-ghost-blocked", () => Ghost("shelf", new GridPoint(2, 1)));
        Step("07-stage-two-meat", PrepareStageTwo, frames: 12);
    }

    public override void _Process(double delta)
    {
        if (_pending is { } name && --_wait <= 0)
        {
            GetViewport().GetTexture().GetImage().SavePng(_dir.PathJoin(name + ".png"));
            GD.Print($"Screenshot: {name}");
            _pending = null;
        }

        if (_pending is not null) return;
        if (_steps.Count == 0)
        {
            GetTree().Quit();
            return;
        }

        var (next, setup, frames) = _steps.Dequeue();
        Ui.ClearTool();
        Ui.Mode = ToolMode.Play;
        setup();
        Ui.Notify();

        // The tour skips ahead by ticking the Sim directly; drop the backlog of events (e.g. day-closed reports)
        // so pop-ups from skipped days do not cover the shot.
        Sim.DrainEvents();
        Hud.CloseAllPanels();
        _pending = next;
        _wait = frames;
    }

    private void Send(SimCommand command)
    {
        Sim.Enqueue(command);
        Sim.Tick();
    }

    private void Ticks(int count)
    {
        for (var i = 0; i < count; i++) Sim.Tick();
    }

    private void WalkTo(GridPoint cell)
    {
        Send(new MovePlayer(cell));
        for (var i = 0; i < 500 && Sim.Player.Path.Count > 0; i++) Sim.Tick();
    }

    private void RunToNextDay()
    {
        if (Sim.Clock.Phase == DayPhase.PreOpen) Send(new OpenStore());
        for (var i = 0; i < 100_000 && Sim.Clock.Phase != DayPhase.Closed; i++) Sim.Tick();
        Send(new StartNextDay());
    }

    private void PrepareDayTwo()
    {
        var shelves = Sim.Layout.Fixtures.Where(f => f.FixtureId == "shelf").ToList();
        string[] products = ["rice_jasmine", "noodles_instant", "soy_sauce_light", "chili_crisp", "shrimp_chips", "milk_candy", "soy_milk", "jasmine_tea"];
        var i = 0;
        foreach (var s in shelves)
            for (var slot = 0; slot < 4; slot++)
                Send(new AssignShelfSlot(s.InstanceId, slot, products[i++ % products.Length]));
        foreach (var p in products) Send(new PlaceOrder(p, 16));
        RunToNextDay();
    }

    private void Ghost(string fixtureId, GridPoint cell)
    {
        Ui.Mode = ToolMode.Build;
        Ui.PlacingFixtureId = fixtureId;
        Store.DebugHover = cell;
    }

    /// <summary>Skips ahead to stage 2 by editing a save (revenue and cash), then sets up the meat corner.</summary>
    private void PrepareStageTwo()
    {
        Store.DebugHover = null;
        RunToNextDay();
        var json = SaveGame.Write(Sim)
            .Replace($"\"cumulativeRevenueCents\":{Sim.Economy.CumulativeRevenueCents}", "\"cumulativeRevenueCents\":1000000")
            .Replace($"\"cashCents\":{Sim.Economy.CashCents},", "\"cashCents\":1000000,");
        Host.LoadFromJson(json);
        Send(new UpgradeStage());
        Send(new PlaceFixture("cold_storage", new GridPoint(16, 0)));
        Send(new PlaceFixture("meat_display", new GridPoint(13, 4)));
        Send(new PlaceFixture("cutting_table", new GridPoint(16, 7)));
        Send(new HireEmployee("butcher"));
        var display = Sim.Layout.Fixtures.Single(f => f.FixtureId == "meat_display").InstanceId;
        string[] cuts = ["pork_belly", "pork_ribs", "pork_loin", "ground_pork"];
        for (var slot = 0; slot < 4; slot++) Send(new AssignShelfSlot(display, slot, cuts[slot]));
        Send(new PlaceOrder("pork_primal", 2));
        RunToNextDay();
        Send(new RestockAll());
        for (var i = 0; i < 2000 && !Sim.Workers.Any(w => w.Step == WorkStep.Cutting); i++) Sim.Tick();
        Ticks(60);
    }
}
