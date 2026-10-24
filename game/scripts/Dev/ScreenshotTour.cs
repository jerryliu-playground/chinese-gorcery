using ChineseGrocery.Game.Ui;
using ChineseGrocery.Game.View;
using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Customers;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Meat;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Produce;
using ChineseGrocery.Sim.Save;
using ChineseGrocery.Sim.Staff;
using ChineseGrocery.Sim.Tasks;
using ChineseGrocery.Sim.Time;
using Godot;

namespace ChineseGrocery.Game.Dev;

/// <summary>
/// Developer tool, not part of play: launched with <c>--screenshot-tour=DIR</c>, it walks through scripted scenes using
/// ordinary Sim commands and saves a screenshot of each to DIR, then quits. Used to check visual changes (CLAUDE.md).
/// Needs a rendering run (not --headless); run it with Godot's movie mode at 30 fps so short effects are caught, e.g.
/// <c>godot --path game --write-movie /tmp/x.png --fixed-fps 30 -- --screenshot-tour=DIR</c>. Add <c>--tour=m8</c> for the
/// produce department scenes instead of the M7 ones.
/// </summary>
public partial class ScreenshotTour : Node
{
    private const int MaxWaitFrames = 1800;

    /// <summary>The tour always plays the same store so every scene is reachable run after run.</summary>
    public const ulong Seed = 20261024;

    private readonly Queue<(string Name, Action Setup, Func<bool>? Until, int Frames)> _steps = new();
    private readonly List<SimEvent> _seen = [];
    private string _dir = "";
    private int _wait;
    private int _waitedFor;
    private string? _pending;
    private Func<bool>? _until;

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
        Host.EventsReady += events => _seen.AddRange(events);
        Plan();
    }

    /// <summary>Sets the scene up (skipping ahead freely), then shoots a still a few frames later.</summary>
    private void Step(string name, Action setup, int frames = 6) => _steps.Enqueue((name, setup, null, frames));

    /// <summary>Sets the scene up, lets the game play normally until <paramref name="until"/>, then shoots after a few frames.</summary>
    private void Live(string name, Action setup, Func<bool> until, int frames = 2) => _steps.Enqueue((name, setup, until, frames));

    private bool Seen<T>(Func<T, bool>? match = null) where T : SimEvent => _seen.OfType<T>().Any(e => match?.Invoke(e) ?? true);

    private GameSim Sim => Host.Sim;

    private void Plan()
    {
        if (OS.GetCmdlineUserArgs().Contains("--tour=m8"))
        {
            PlanProduce();
            return;
        }

        // Art
        Step("01-day1-opening-stock", () => { });
        Step("02-report-with-order-button", () =>
        {
            WalkTo(new GridPoint(2, 4));
            Send(new OpenStore());
            for (var i = 0; i < 100_000 && Sim.Clock.Phase != DayPhase.Closed; i++) Sim.Tick();
            Hud.ShowReport(Sim.Reports[^1]);
        });
        Step("02-day2-backroom-full", PrepareDayTwo);
        Step("03-build-ghost-fits", () => Ghost("shelf", new GridPoint(7, 3)));
        Step("04-build-ghost-blocked", () => Ghost("shelf", new GridPoint(2, 1)));

        // Feedback (10.2)
        Live("05-place-landing-bounce", () => Host.Send(new PlaceFixture("shelf", new GridPoint(7, 3))), () => Seen<Sim.Grid.FixturePlaced>(), frames: 3);
        Live("06-shelf-bounce-and-flash", () =>
        {
            // Snacks priced far above what anyone pays, so someone frowns later.
            Send(new SetPrice("shrimp_chips", 9999));
            Host.Send(new RestockAll());
        }, () => Seen<ItemsShelved>(), frames: 3);
        Live("07-checkout-money-pops-up", () =>
        {
            WalkTo(new GridPoint(2, 4));
            Host.Send(new OpenStore());
        }, () => Seen<CheckoutCompleted>(), frames: 3);
        Live("08-bubble-too-expensive", () => { }, () => Seen<CustomerReaction>(r => r.Kind == CustomerReactionKind.TooExpensive), frames: 3);
        Live("09-bubble-gave-up-angry", () => WalkTo(new GridPoint(7, 6)), () => Seen<CustomerReaction>(r => r.Kind == CustomerReactionKind.GaveUp), frames: 3);
        Live("10-bubble-not-found", EmptyTheShelves, () => Seen<CustomerReaction>(r => r.Kind == CustomerReactionKind.NotFound), frames: 3);
        Live("11-upgrade-celebration", PrepareUpgrade, () => Seen<StageUpgraded>(), frames: 4);
        Live("12-chopping", PrepareMeatCorner, () => Sim.Workers.Any(w => w.Step == WorkStep.Cutting), frames: 4);
        Live("13-cut-meat-pops-up", () => { }, () => Seen<MeatCut>(), frames: 3);

        // Sound (10.3)
        Step("14-settings-volume-sliders", () => Hud.ShowPanel<SettingsPanel>());

        // Atmosphere (10.4): price tags and chatter, in Chinese and then English.
        Live("15-price-tag-and-chatter-zh", OpenWithChatter, () => _chatty.Any(), frames: 30);
        Live("16-price-tag-and-chatter-en", () =>
        {
            TranslationServer.SetLocale("en");
            Hud.Retranslate();
            Store.DebugHover = ShelfCell();
        }, () => true, frames: 20);
        Live("17-family-customer", () =>
        {
            TranslationServer.SetLocale("zh");
            Hud.Retranslate();
        }, () => Sim.Customers.Any(c => c.TypeId == "chinese_family"), frames: 3);
    }

    // ---------- M8: produce ----------

    private static readonly GridPoint[] StandCells = [new(12, 7), new(13, 7), new(14, 7), new(15, 7)];

    /// <summary>Two products per stand, neighbours in different colours.</summary>
    private static readonly string[][] StandProducts =
        [["napa_cabbage", "bok_choy"], ["orange", "lychee"], ["daikon", "scallion"], ["ginger", "lychee"]];

    private List<int> Stands() => Sim.Layout.Fixtures.Where(f => f.FixtureId == "produce_stand").Select(f => f.InstanceId).ToList();

    /// <summary>Spec 11.7 item 4: stage 2, place stands, order, shelve, freshness and markdowns, tidy, hover the score.</summary>
    private void PlanProduce()
    {
        Step("01-stage2-place-produce-stand", () =>
        {
            ReachStageTwo();
            Ghost("produce_stand", StandCells[0]);
        });
        Step("02-stands-placed-and-assigned", () =>
        {
            // A second backroom, so the produce has somewhere to wait next to the opening stock.
            Send(new PlaceFixture("backroom", new GridPoint(14, 0)));
            for (var i = 0; i < StandCells.Length; i++)
            {
                Send(new PlaceFixture("produce_stand", StandCells[i]));
                var stand = Sim.Layout.Fixtures[^1].InstanceId;
                for (var slot = 0; slot < 2; slot++) Send(new AssignShelfSlot(stand, slot, StandProducts[i][slot]));
            }

            Ui.Mode = ToolMode.Build;
        });
        Step("03-order-produce", () =>
        {
            // Lychees priced far above what shoppers pay, so they sit until they ripen and get marked down.
            Send(new SetPrice("lychee", 1199));
            OrderProduce(1);
            Hud.ShowPanel<OrderPanel>();
        });
        Live("04-shelved-fresh-hover-score", () =>
        {
            WalkTo(new GridPoint(2, 4));
            RunToNextDay();
            Host.Send(new RestockAll());
            Store.DebugHover = StandCells[1];
        }, () => Seen<ItemsShelved>(), frames: 4);
        Step("05-days-later-ripe-sale-card-messy", () =>
        {
            // Two more days of trade, with a fresh delivery on top of what is left (but no more lychees), so stands hold
            // old and new batches.
            WalkTo(new GridPoint(2, 4));
            RunToNextDay();
            OrderProduce(0.5, except: "lychee");
            RunToNextDay();
            Send(new RestockAll());
            Store.DebugHover = Sim.Layout.Find(Stands().FirstOrDefault(HasRipe, Stands()[1]))!.Origin;
        });
        Live("06-player-tidying", () =>
        {
            var stand = Sim.Layout.Find(Sim.Tasks.Tasks.OfType<TidyTask>().First().StandInstanceId)!;
            Store.DebugHover = stand.Origin;
            WalkTo(new GridPoint(stand.Origin.X, stand.Origin.Y + 1));
            Host.Send(new PlayerInteract(stand.InstanceId));
        }, () => Sim.Player.Step == WorkStep.Tidying, frames: 4);
        Live("07-tidied-hover-score", () => { }, () => Seen<StandTidied>(), frames: 3);
        Live("08-shoppers-mess-it-up-again", () =>
        {
            Store.DebugHover = null;
            TidyEverything();
            WalkTo(new GridPoint(2, 4));
            Send(new OpenStore());
            // Skip ahead to a stand one rummage away from needing a tidy, then watch it happen.
            var nearly = (Sim.Data.Economy.Display?.TidyBelow ?? 0) + (Sim.Data.Economy.Display?.MessPerItemTaken ?? 0);
            for (var i = 0; i < 20_000 && Sim.Clock.Phase == DayPhase.Open && !Stands().Any(s => Sim.TidinessOf(s) < nearly); i++) Sim.Tick();
        }, () => Sim.Tasks.Tasks.OfType<TidyTask>().Any(), frames: 3);
        Live("09-good-display-sparkles", () =>
        {
            // A tidy, freshly topped-up row, before the doors open.
            WalkTo(new GridPoint(2, 4));
            RunToNextDay();
            OrderProduce(1);
            RunToNextDay();
            Send(new RestockAll());
            TidyEverything();
        }, () => Stands().Any(Store.IsSparkling), frames: 4);
        Step("10-hover-score-en", () =>
        {
            TranslationServer.SetLocale("en");
            Hud.Retranslate();
            Store.DebugHover = StandCells[1];
        });
    }

    /// <summary>Orders each stand product, the given share of what its slots hold.</summary>
    private void OrderProduce(double share, string? except = null)
    {
        var products = StandProducts.SelectMany(p => p).ToList();
        foreach (var product in products.Distinct().Where(p => p != except)) Send(new PlaceOrder(product, (int)(Full(product) * products.Count(p => p == product) * share)));
    }

    /// <summary>The player works every Tidy task on the board, one stand after another.</summary>
    private void TidyEverything()
    {
        foreach (var task in Sim.Tasks.Tasks.OfType<TidyTask>().ToList())
        {
            var stand = Sim.Layout.Find(task.StandInstanceId)!;
            WalkTo(new GridPoint(stand.Origin.X, stand.Origin.Y + 1));
            Send(new PlayerInteract(stand.InstanceId));
            for (var i = 0; i < 500 && Sim.Player.Step != WorkStep.Idle; i++) Sim.Tick();
        }
    }

    private int Full(string productId) => Sim.Data.Products.Single(p => p.Id == productId).UnitsPerShelfSlot;

    private bool HasRipe(int stand) =>
        Enumerable.Range(0, 2).Any(slot => Sim.Inventory.TryGet(new StockLocation(stand, slot)) is { } pile
            && pile.Lots.Any(l => Freshness.TierOf(Sim.Data.Products.Single(p => p.Id == l.ProductId), l, Sim.Clock.Day) >= FreshnessTier.Ripe));

    /// <summary>Upgrades to stage 2 straight away, by editing a save so the revenue target and the cost are covered.</summary>
    private void ReachStageTwo()
    {
        var json = SaveGame.Write(Sim)
            .Replace($"\"cumulativeRevenueCents\":{Sim.Economy.CumulativeRevenueCents}", "\"cumulativeRevenueCents\":1000000")
            .Replace($"\"cashCents\":{Sim.Economy.CashCents},", "\"cashCents\":1000000,");
        Host.LoadFromJson(json);
        Send(new UpgradeStage());
    }

    public override void _Process(double delta)
    {
        if (_pending is not null && _until is { } condition)
        {
            if (!condition() && ++_waitedFor < MaxWaitFrames) return;
            if (_waitedFor >= MaxWaitFrames) GD.PushWarning($"Screenshot {_pending}: waited {MaxWaitFrames} frames, shooting anyway.");
            _until = null;
        }

        if (_pending is { } name && --_wait <= 0)
        {
            GetViewport().GetTexture().GetImage().SavePng(_dir.PathJoin(name + ".png"));
            GD.Print($"Screenshot: {name}  (HUD shows {Hud.CashText}, actual cash {UiText.Money(Sim.Economy.CashCents)})");
            _pending = null;
        }

        if (_pending is not null) return;
        if (_steps.Count == 0)
        {
            GetTree().Quit();
            return;
        }

        var (next, setup, until, frames) = _steps.Dequeue();
        Ui.ClearTool();
        Ui.Mode = ToolMode.Play;
        Store.DebugHover = null;
        _seen.Clear();
        Hud.CloseAllPanels();
        setup();
        Ui.Notify();

        // Set-up skips ahead by ticking the Sim directly; drop that backlog of events (e.g. day-closed reports)
        // so pop-ups from skipped time do not cover the shot. Live steps then play on normally.
        Sim.DrainEvents();
        _pending = next;
        _wait = frames;
        _until = until;
        _waitedFor = 0;
    }

    private void Send(SimCommand command)
    {
        Sim.Enqueue(command);
        Sim.Tick();
        foreach (var rejected in Sim.DrainEvents().OfType<CommandRejected>()) GD.PushWarning($"Tour: {rejected.Command} rejected: {rejected.Reason}");
    }

    private void Ticks(int count)
    {
        for (var i = 0; i < count; i++) Sim.Tick();
    }

    private readonly List<int> _chatty = [];

    private GridPoint ShelfCell() => Sim.Layout.Fixtures.First(f => f.FixtureId == "shelf").Origin;

    /// <summary>Next morning: shelves filled, doors open, a tag shown on the first shelf, and everyone chattier than usual.</summary>
    private void OpenWithChatter()
    {
        RunToNextDay();
        Send(new RestockAll());
        WalkTo(new GridPoint(2, 4));
        Send(new OpenStore());
        Ticks(300);
        FeelConfig.Load().SpeechChancePerGameMinute = 1;
        Host.Ticked += () => _chatty.AddRange(Sim.Customers.Select(c => c.Id));
        Store.DebugHover = ShelfCell();
    }

    /// <summary>Next morning, prices back to normal and no restocking: serve until nothing is left on display.</summary>
    private void EmptyTheShelves()
    {
        RunToNextDay();
        Send(new SetPrice("shrimp_chips", Sim.Data.Products.Single(p => p.Id == "shrimp_chips").SuggestedPriceCents));
        WalkTo(new GridPoint(2, 4));
        Send(new OpenStore());
        for (var i = 0; i < 20_000 && Sim.Clock.Phase == DayPhase.Open && Sim.Shelves.Assigned.Any(a => Sim.Inventory.Count(a.Key, a.Value) > 0); i++) Sim.Tick();
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

    /// <summary>Skips to the next morning with the stage 2 target met by editing a save (revenue and cash), then upgrades live.</summary>
    private void PrepareUpgrade()
    {
        RunToNextDay();
        var json = SaveGame.Write(Sim)
            .Replace($"\"cumulativeRevenueCents\":{Sim.Economy.CumulativeRevenueCents}", "\"cumulativeRevenueCents\":1000000")
            .Replace($"\"cashCents\":{Sim.Economy.CashCents},", "\"cashCents\":1000000,");
        Host.LoadFromJson(json);
        Host.Send(new UpgradeStage());
    }

    /// <summary>Builds the meat corner, hires the butcher and gets a primal delivered and waiting to be cut.</summary>
    private void PrepareMeatCorner()
    {
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
    }
}
