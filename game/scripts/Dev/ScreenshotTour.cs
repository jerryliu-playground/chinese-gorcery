using ChineseGrocery.Game.Ui;
using ChineseGrocery.Game.View;
using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Customers;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Deli;
using ChineseGrocery.Sim.Meat;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Produce;
using ChineseGrocery.Sim.Reputation;
using ChineseGrocery.Sim.Save;
using ChineseGrocery.Sim.Seafood;
using ChineseGrocery.Sim.Staff;
using ChineseGrocery.Sim.Tasks;
using ChineseGrocery.Sim.Time;
using Godot;

namespace ChineseGrocery.Game.Dev;

/// <summary>
/// Developer tool, not part of play: launched with <c>--screenshot-tour=DIR</c>, it walks through scripted scenes using
/// ordinary Sim commands and saves a screenshot of each to DIR, then quits. Used to check visual changes (CLAUDE.md).
/// Needs a rendering run (not --headless); run it with Godot's movie mode at 30 fps so short effects are caught, e.g.
/// <c>godot --path game --write-movie /tmp/x.png --fixed-fps 30 -- --screenshot-tour=DIR</c>. Add <c>--tour=m8</c> or
/// <c>--tour=m9</c>, <c>--tour=m10</c>, <c>--tour=fixes</c> or <c>--tour=m15</c> for the produce, seafood or deli
/// department scenes, the playtest fixes of 2026-10-25, or the story and tutorial, instead of the M7 ones.
/// Dialogues are hidden (and do not hold time) in every tour but the story one.
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
    public StoreCamera Camera { get; set; } = null!;

    /// <summary>When set, the view stays centred on this cell (for corners a big floor would otherwise scroll past).</summary>
    private GridPoint? _focus;
    public Hud Hud { get; set; } = null!;

    public static string? RequestedDir() =>
        OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--screenshot-tour=", StringComparison.Ordinal))?.Split('=', 2)[1];

    public override void _Ready()
    {
        _dir = RequestedDir()!;
        DirAccess.MakeDirRecursiveAbsolute(_dir);
        TranslationServer.SetLocale("zh");
        Host.EventsReady += events => _seen.AddRange(events);

        // Only the story tour shows dialogues; the others play on as if there were none.
        Hud.Dialogue.Suppressed = !OS.GetCmdlineUserArgs().Contains("--tour=m15");
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

        if (OS.GetCmdlineUserArgs().Contains("--tour=m9"))
        {
            PlanSeafood();
            return;
        }

        if (OS.GetCmdlineUserArgs().Contains("--tour=m10"))
        {
            PlanDeli();
            return;
        }

        if (OS.GetCmdlineUserArgs().Contains("--tour=m11"))
        {
            PlanStaff();
            return;
        }

        if (OS.GetCmdlineUserArgs().Contains("--tour=m13"))
        {
            PlanReputation();
            return;
        }

        if (OS.GetCmdlineUserArgs().Contains("--tour=m14"))
        {
            PlanImports();
            return;
        }

        if (OS.GetCmdlineUserArgs().Contains("--tour=m12"))
        {
            PlanCalendar();
            return;
        }

        if (OS.GetCmdlineUserArgs().Contains("--tour=fixes"))
        {
            PlanFixes();
            return;
        }

        if (OS.GetCmdlineUserArgs().Contains("--tour=m15"))
        {
            PlanStory();
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

    // ---------- M15: story and tutorial ----------

    private DialogueBox Dialogue => Hud.Dialogue;

    /// <summary>Finishes the dialogue on screen (first answer where there is a choice).</summary>
    private void Finish(string? choice = null) => Send(new Sim.Story.FinishDialogue(Sim.Story.Current!, choice));

    /// <summary>Shows a line of the current dialogue with its text already typed out.</summary>
    private void ShowLine(int line)
    {
        Dialogue.Sync();
        Dialogue.DebugShowLine(line);
        Hud.RefreshNow();
    }

    /// <summary>Reads every queued dialogue (first answers).</summary>
    private void ReadAll()
    {
        while (Sim.Story.Current is { } id) Finish(Sim.Data.Story!.Dialogue(id).Choices?.FirstOrDefault()?.Id);
        Dialogue.Sync();
    }

    /// <summary>Spec 18.7 items 4 and 5: the whole opening, Dad's six calls, a chapter, the loan and the settings toggle.</summary>
    private void PlanStory()
    {
        Step("01-opening-black-screen-captions", () => ShowLine(1));
        Step("02-opening-mom-calls", () =>
        {
            Finish();
            ShowLine(1);
        }, frames: 4);
        Step("02b-a-click-goes-to-the-next-line", () =>
        {
            // A real mouse click on the dialogue (not a debug call): the line is typed out, so it moves on.
            var click = new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = new Vector2(320, 200) };
            GetViewport().PushInput(click, inLocalCoords: true);
            GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = new Vector2(320, 200) }, inLocalCoords: true);
            GD.Print($"Tour: pushed a click on the dialogue at {Dialogue.GetGlobalRect()}");
        }, frames: 40);
        Step("03-opening-parents-retire-choice", () =>
        {
            Finish();
            Dialogue.Sync();
            Dialogue.DebugEnd();
        });
        Step("04-opening-name-the-shop", () =>
        {
            Finish("accept");
            Finish();
            Dialogue.Sync();
            Dialogue.DebugEnd();
        });
        Step("05-opening-walk-in-with-shop-name", () =>
        {
            // A player-typed name, in Chinese (escaped: code holds no Chinese text).
            Send(new Sim.Story.NameShop("\u83b2\u82b1\u8d85\u5e02"));
            Finish();
            ShowLine(0);
        });
        Step("06-tutorial1-dad-calls-about-orders", () =>
        {
            Finish();
            ShowLine(1);
        }, frames: 4);
        Step("07-tutorial-reminder-and-loan-corner", () =>
        {
            Finish();
            Dialogue.Sync();
            Hud.RefreshNow();
        });
        Step("08-tutorial3-checkout-call", () =>
        {
            Send(new PlaceOrder("rice_jasmine", 10));
            WalkTo(new GridPoint(2, 4));
            Send(new OpenStore());
            ShowLine(1);
        });
        Step("09-tutorial4-report-call", () =>
        {
            ReadAll();
            for (var i = 0; i < 100_000 && Sim.Clock.Phase != DayPhase.Closed; i++) Sim.Tick();
            Hud.ShowReport(Sim.Reports[^1]);
            ShowLine(1);
        });
        Step("10-tutorial2-shelving-call", () =>
        {
            ReadAll();
            Send(new Sim.Story.ReportViewed());
            Send(new StartNextDay());
            ShowLine(1);
        });
        Step("11-chapter1-auntie-remembers", () =>
        {
            ReadAll();
            Send(new RestockAll());
            Send(new OpenStore());
            ShowLine(1);
        });
        Step("12-tutorial5-prices-call", () =>
        {
            ReadAll();
            for (var i = 0; i < 100_000 && Sim.Clock.Phase != DayPhase.Closed; i++) Sim.Tick();
            Send(new StartNextDay());
            Dialogue.Sync();
            while (Sim.Story.Current is { } id && id != "tutorial_5_price") Finish(Sim.Data.Story!.Dialogue(id).Choices?.FirstOrDefault()?.Id);
            ShowLine(2);
        });
        Step("13-loan-panel", () =>
        {
            ReadAll();
            Send(new SetPrice("rice_jasmine", 1299));
            ReadAll();
            Hud.ShowPanel<LoanPanel>();
        });
        Step("14-settings-story-prompts", () => Hud.ShowPanel<SettingsPanel>());
        Step("15-tutorial6-upgrade-call", () =>
        {
            var json = SaveGame.Write(Sim)
                .Replace($"\"cumulativeRevenueCents\":{Sim.Economy.CumulativeRevenueCents}", "\"cumulativeRevenueCents\":1000000")
                .Replace($"\"cashCents\":{Sim.Economy.CashCents},", "\"cashCents\":1000000,");
            Host.LoadFromJson(json);
            Ticks(1);
            ShowLine(1);
        }, frames: 4);
        Step("16-chapter2-dad-worries-choice", () =>
        {
            ReadAll();
            Send(new UpgradeStage());
            Dialogue.Sync();
            Dialogue.DebugEnd();
            Hud.RefreshNow();
        });
        Step("17-chapter2-follow-up-advice", () =>
        {
            Finish("ask");
            ShowLine(1);
        });
        Step("18-dept-meat-call-en", () =>
        {
            TranslationServer.SetLocale("en");
            Hud.Retranslate();
            Finish();
            ShowLine(1);
        }, frames: 4);
        Step("19-dept-produce-mom-en", () =>
        {
            Finish();
            ShowLine(2);
        });
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
    private void ReachStageTwo() => ReachStage(2);

    private void ReachStage(int stage)
    {
        var json = SaveGame.Write(Sim)
            .Replace($"\"cumulativeRevenueCents\":{Sim.Economy.CumulativeRevenueCents}", "\"cumulativeRevenueCents\":10000000")
            .Replace($"\"cashCents\":{Sim.Economy.CashCents},", "\"cashCents\":1000000,");
        Host.LoadFromJson(json);
        while (Sim.Economy.Stage < stage) Send(new UpgradeStage());
    }

    // ---------- M9: seafood ----------

    // Tanks against the back wall, the counter against the right wall.
    private static readonly GridPoint[] TankCells = [new(19, 0), new(20, 0)];
    private static readonly GridPoint CounterCell = new(23, 4);
    private static readonly GridPoint IceCell = new(21, 4);
    private static readonly GridPoint CounterWorkCell = new(23, 3);
    private static readonly GridPoint CashierCell = new(2, 4);
    private static readonly string[][] TankProducts = [["live_fish", "live_shrimp"], ["crab", "lobster"]];

    private int FirstOf(string fixtureId) => Sim.Layout.Fixtures.First(f => f.FixtureId == fixtureId).InstanceId;

    private List<int> FishTanks() => Sim.Layout.Fixtures.Where(f => f.FixtureId is "fish_tank" or "shellfish_tank").Select(f => f.InstanceId).ToList();

    private GridPoint Below(int instanceId)
    {
        var f = Sim.Layout.Find(instanceId)!;
        return new GridPoint(f.Origin.X, f.Origin.Y + 1);
    }

    /// <summary>Spec 12.10 item 4: stage 3, the seafood corner, stocking, dirty water and deaths, chilled fish, cleaning, killing to order, the fishmonger.</summary>
    private void PlanSeafood()
    {
        Step("01-stage3-place-fish-tank", () =>
        {
            ReachStage(3);
            Ghost("fish_tank", TankCells[0]);
        });
        Step("02-seafood-corner-placed", () =>
        {
            _focus = new GridPoint(18, 4);
            // Cold storage for chilled goods and a second backroom so live deliveries fit next to the opening stock.
            Send(new PlaceFixture("cold_storage", new GridPoint(16, 0)));
            Send(new PlaceFixture("backroom", new GridPoint(12, 0)));
            for (var i = 0; i < TankCells.Length; i++)
            {
                // Fish and shrimp in the fish tank; lobster and crab in a tank of their own.
                Send(new PlaceFixture(i == 0 ? "fish_tank" : "shellfish_tank", TankCells[i]));
                var tank = Sim.Layout.Fixtures[^1].InstanceId;
                for (var slot = 0; slot < 2; slot++) Send(new AssignShelfSlot(tank, slot, TankProducts[i][slot]));
            }

            Send(new PlaceFixture("seafood_counter", CounterCell));
            Send(new PlaceFixture("ice_table", IceCell));
            var ice = Sim.Layout.Fixtures[^1].InstanceId;
            Send(new AssignShelfSlot(ice, 0, "clams"));
            Send(new AssignShelfSlot(ice, 1, "chilled_fish"));
            Ui.Mode = ToolMode.Build;
        });
        Step("03-order-seafood", () =>
        {
            OrderSeafood();
            Hud.ShowPanel<OrderPanel>();
        });
        Step("04-tanks-stocked-hover-water", () =>
        {
            RunToNextDay();
            Send(new RestockAll());
            Store.DebugHover = TankCells[0];
        });
        Live("05-water-getting-murky", () =>
        {
            // Nobody at the till, so nothing sells and the tanks stay full; and nobody cleans.
            Sim.TankCleaningDisabled = true;
            Send(new OpenStore());
            var cleanBelow = Sim.Data.Economy.Seafood!.CleanBelow;
            for (var i = 0; i < 20_000 && Sim.Clock.Phase == DayPhase.Open && Sim.WaterQualityOf(FishTanks()[0]) > cleanBelow; i++) Sim.Tick();
            Store.DebugHover = TankCells[0];
        }, () => Sim.WaterQualityOf(FishTanks()[0]) < Sim.Data.Economy.Seafood!.CleanBelow, frames: 3);
        Step("06-dead-in-the-tank", () =>
        {
            // Dirty nights until a fish or lobster dies (those go on as chilled goods), restocking each morning so the
            // tanks stay full.
            for (var day = 0; day < 10 && !FishTanks().Any(HasDeadThatKeeps); day++)
            {
                RunToNextDay();
                Send(new RestockAll());
            }

            Store.DebugHover = Sim.Layout.Find(FishTanks().FirstOrDefault(HasDeadThatKeeps, FishTanks()[0]))!.Origin;
        });
        Live("07-taking-out-the-dead", () =>
        {
            var tank = FishTanks().FirstOrDefault(HasDeadThatKeeps, FishTanks().First(t => Sim.DeadIn(t).Count > 0));
            WalkTo(Below(tank));
            Host.Send(new PlayerInteract(tank));
        }, () => Sim.Player.Step == WorkStep.RemovingDead, frames: 4);
        Live("08-dead-fish-on-ice-as-chilled", () => Store.DebugHover = IceCell, () => Seen<DeadRemoved>(), frames: 3);
        Live("09-cleaning-the-tank", () =>
        {
            Sim.TankCleaningDisabled = false;
            Ticks(1);
            var tank = Sim.Tasks.Tasks.OfType<CleanTankTask>().First().TankInstanceId;
            Store.DebugHover = Sim.Layout.Find(tank)!.Origin;
            WalkTo(Below(tank));
            Host.Send(new PlayerInteract(tank));
        }, () => Sim.Player.Step == WorkStep.Cleaning, frames: 4);
        Live("10-clean-water-again", () => { }, () => Seen<TankCleaned>(), frames: 3);
        Live("11-customer-waits-at-counter", () =>
        {
            Store.DebugHover = null;
            foreach (var tank in FishTanks().Where(t => Sim.WaterQualityOf(t) < 100)) CleanTank(tank);
            Host.Send(new OpenStore());
        }, () => Sim.Tasks.Tasks.OfType<KillFishTask>().Any(), frames: 3);
        Live("12-player-kills-a-fish", () =>
        {
            var task = Sim.Tasks.Tasks.OfType<KillFishTask>().First();
            WalkTo(Below(task.Source.FixtureInstanceId));
            Send(new PlayerInteract(task.Source.FixtureInstanceId));
            for (var i = 0; i < 500 && Sim.Player.Step != WorkStep.Carrying; i++) Sim.Tick();
            WalkTo(CounterWorkCell);
            Host.Send(new PlayerInteract(FirstOf("seafood_counter")));
        }, () => Sim.Player.Step == WorkStep.Killing, frames: 6);
        Live("13-fish-handed-over", () => { }, () => Seen<FishKilled>(), frames: 3);
        Live("14-fishmonger-at-work", () =>
        {
            RunToNextDay();
            Send(new HireEmployee("fishmonger"));
            OrderSeafood();
            Send(new RestockAll());
            WalkTo(CashierCell);
            Host.Send(new OpenStore());
        }, () => Sim.Workers.Any(w => w.RoleId == "fishmonger" && w.Step is WorkStep.Killing or WorkStep.Carrying), frames: 6);
        Step("15-report-with-seafood", () =>
        {
            for (var i = 0; i < 100_000 && Sim.Clock.Phase != DayPhase.Closed; i++) Sim.Tick();
            Hud.ShowReport(Sim.Reports[^1]);
        });
        Step("16-hover-water-en", () =>
        {
            TranslationServer.SetLocale("en");
            Hud.Retranslate();
            Store.DebugHover = TankCells[0];
        });
        Step("17-camera-scrolled-to-far-corner", () =>
        {
            _focus = null;
            TranslationServer.SetLocale("zh");
            Hud.Retranslate();
            Hud.CloseAllPanels();
            var size = Sim.Layout.Size;
            Camera.CenterOn(new GridPoint(size.W - 1, size.H - 1));
        });
    }

    // ---------- M10: deli ----------

    private static readonly GridPoint[] StoveCells = [new(14, 8), new(15, 8)];
    private static readonly GridPoint SteamTableCell = new(17, 8);
    private static readonly GridPoint RoastStationCell = new(18, 8);

    /// <summary>
    /// Spec 13.10 item 4 and 13.11: from ordering to a hot dish sold. Groceries bought on day 1 and a pork primal on
    /// day 3 meet on day 4, when the bok choy, the ducks and the freshly cut trimmings are all on their last day, so
    /// the menu flags them and the kitchen cooks them first.
    /// </summary>
    private void PlanDeli()
    {
        Step("01-stage4-place-wok-stove", () =>
        {
            ReachStage(4);
            Ghost("wok_stove", StoveCells[0]);
        });
        Step("02-deli-corner-placed", () =>
        {
            foreach (var cell in StoveCells) Send(new PlaceFixture("wok_stove", cell));
            Send(new PlaceFixture("steam_table", SteamTableCell));
            Send(new PlaceFixture("roast_station", RoastStationCell));
            // The meat corner supplies pork belly and trimmings; a butcher cuts the primals.
            Send(new PlaceFixture("cold_storage", new GridPoint(20, 0)));
            Send(new PlaceFixture("cutting_table", new GridPoint(23, 0)));
            Send(new PlaceFixture("backroom", new GridPoint(12, 0)));
            Send(new HireEmployee("butcher"));
            Ui.Mode = ToolMode.Build;
        });
        Step("03-order-groceries-for-the-kitchen", () =>
        {
            Send(new PlaceOrder("bok_choy", 12));
            Send(new PlaceOrder("napa_cabbage", 8));
            Send(new PlaceOrder("whole_duck", 2));
            Send(new PlaceOrder("pork_shoulder", 1));
            Hud.ShowPanel<OrderPanel>();
        });
        Step("04-menu-flags-near-expiry-stock", () =>
        {
            RunToNextDay();
            RunToNextDay();
            Send(new PlaceOrder("pork_primal", 1));
            Send(new PlaceOrder("bok_choy", 6));
            RunToNextDay();
            // The butcher cuts the primal before opening: pork belly, ribs and trimmings.
            for (var i = 0; i < 2000 && (Sim.Tasks.Tasks.OfType<CutMeatTask>().Any() || Sim.Workers.Any(w => w.Step == WorkStep.Cutting)); i++) Sim.Tick();

            var table = FirstOf("steam_table");
            Send(new SetMenuItem(table, 0, "stir_fried_bok_choy", 12));
            Send(new SetMenuItem(table, 1, "dumplings", 10));
            Send(new SetMenuItem(table, 2, "braised_pork", 6));
            Send(new SetMenuItem(FirstOf("roast_station"), 0, "roast_duck", 16));
            Send(new SetMenuItem(FirstOf("roast_station"), 1, "char_siu", 8));
            Hud.ShowPanel<MenuPanel>();
        });
        Live("05-cooking-at-the-wok", () =>
        {
            var task = Sim.Tasks.Tasks.OfType<CookTask>().First();
            WalkTo(Below(task.StoveInstanceId));
            Host.Send(new PlayerInteract(task.StoveInstanceId));
        }, () => Sim.Player.Step == WorkStep.Cooking, frames: 8);
        Live("06-dish-carried-to-the-steam-table", () =>
        {
            for (var i = 0; i < 2000 && Sim.Player.Step != WorkStep.Carrying; i++) Sim.Tick();
            var target = Sim.Tasks.Find(Sim.Player.TaskId!.Value) is CookTask c ? c.Target.FixtureInstanceId : FirstOf("steam_table");
            WalkTo(Above(target));
            Host.Send(new PlayerInteract(target));
            Store.DebugHover = SteamTableCell;
        }, () => Seen<ItemsShelved>(), frames: 4);
        Step("07-everything-cooked-before-opening", () =>
        {
            CookEverything();
            Store.DebugHover = SteamTableCell;
        });
        Live("08-lunch-rush-queue", () =>
        {
            Store.DebugHover = null;
            WalkTo(Above(FirstOf("steam_table")));
            Send(new OpenStore());
            for (var i = 0; i < 20_000 && Sim.Clock.MinuteOfDay < 11 * 60 + 40; i++) Sim.Tick();
        }, () => Sim.Customers.Count(c => c.State == CustomerState.AtCounter) >= 2, frames: 4);
        Live("09-packing-takeaway", () =>
        {
            var station = Sim.Tasks.Tasks.OfType<ServeDeliTask>().Select(s => s.StationInstanceId).DefaultIfEmpty(FirstOf("steam_table")).First();
            WalkTo(Above(station));
            Host.Send(new PlayerInteract(station));
        }, () => Sim.Player.Step == WorkStep.Packing, frames: 4);
        Live("10-paying-for-hot-food", () =>
        {
            // Finish packing, then man the till so the buyer can pay.
            for (var i = 0; i < 500 && Sim.Player.Step != WorkStep.Idle; i++) Sim.Tick();
            WalkTo(CashierCell);
        }, () => Seen<CheckoutCompleted>(), frames: 3);
        Step("11-report-waste-saved", () =>
        {
            for (var i = 0; i < 100_000 && Sim.Clock.Phase != DayPhase.Closed; i++) Sim.Tick();
            Hud.ShowReport(Sim.Reports[^1]);
        });
        Live("12-chef-runs-the-kitchen", () =>
        {
            Send(new StartNextDay());
            Send(new HireEmployee("chef"));
            Send(new PlaceOrder("bok_choy", 12));
            RunToNextDay();
            WalkTo(CashierCell);
            Host.Send(new OpenStore());
        }, () => Sim.Workers.Any(w => w.RoleId == "chef" && w.Step is WorkStep.Cooking or WorkStep.Packing), frames: 6);
        Step("13-menu-en", () =>
        {
            TranslationServer.SetLocale("en");
            Hud.Retranslate();
            Hud.ShowPanel<MenuPanel>();
        });
    }

    // ---------- M11: staff management ----------

    /// <summary>Spec 14.9 item 4: hire a cashier and a stocker, set shifts, hire a manager with auto ordering, and the store runs itself.</summary>
    private void PlanStaff()
    {
        Step("01-staff-panel-applicants", () => Hud.ShowPanel<StaffPanel>());
        Step("02-cashier-and-stocker-hired-with-shifts", () =>
        {
            Send(new HireEmployee("cashier"));
            Send(new HireEmployee("stocker"));
            var stocker = Sim.Workers.Last(w => w.RoleId == "stocker");
            Send(new SetShift(stocker.Id, Shift.Morning));
            Send(new SetRestDays(stocker.Id, [6]));
            Hud.ShowPanel<StaffPanel>();
        });
        Live("03-store-runs-while-the-player-stands-still", () =>
        {
            WalkTo(new GridPoint(10, 6));
            Host.Send(new OpenStore());
        }, () => Sim.Workers.Any(w => w.RoleId == "cashier" && w.Step == WorkStep.Serving), frames: 6);
        Step("04-manager-with-auto-ordering", () =>
        {
            RunToNextDay();
            ReachStage(4);
            Send(new HireEmployee("manager"));
            var manager = Sim.Workers.Last(w => w.RoleId == "manager");
            Send(new AssignManager(manager.Id, Department.DryGoods));
            Send(new SetAutomation(Department.DryGoods, true, true));
            foreach (var id in new[] { "rice_jasmine", "noodles_instant", "soy_sauce_light", "chili_crisp", "shrimp_chips", "milk_candy", "soy_milk", "jasmine_tea" })
            {
                Send(new SetOrderTarget(id, 40));
            }

            Hud.ShowPanel<StaffPanel>();
        });
        Step("05-manager-ordered-overnight", () =>
        {
            RunToNextDay();
            Hud.ShowPanel<OrderPanel>();
        });
        Live("06-hands-off-day-with-morale-icons", () =>
        {
            // The manager's orders arrive the morning after they are placed.
            RunToNextDay();
            Send(new RestockAll());
            WalkTo(new GridPoint(10, 6));
            Host.Send(new OpenStore());
            for (var i = 0; i < 2000; i++) Sim.Tick();
        }, () => Sim.Workers.Count(w => w.IsPresent && w.RoleId is not null) >= 2, frames: 40);
    }

    // ---------- M13: reputation and more customers ----------

    /// <summary>
    /// Spec 16.7 item 4: stars and the season's trend in the HUD, reviews after a day, a teen's heart for the trend,
    /// three stars bringing locals (puzzled without English labels), a restaurant buyer's group order accepted, set
    /// aside and delivered, and the bilingual labels bought.
    /// </summary>
    private void PlanReputation()
    {
        Step("01-hud-stars-and-season-trend", () => { });
        Step("02-reviews-after-the-first-day", () =>
        {
            WalkTo(CashierCell);
            Send(new OpenStore());
            for (var i = 0; i < 100_000 && Sim.Clock.Phase != DayPhase.Closed; i++) Sim.Tick();
            Hud.ShowPanel<ReviewsPanel>();
        });
        Live("03-teen-finds-the-trend-heart", () =>
        {
            Send(new StartNextDay());
            Send(new RestockAll());
            WalkTo(CashierCell);
            Send(new OpenStore());
            SkipToJustBefore(e => e is CustomerReaction { Kind: CustomerReactionKind.Heart });
        }, () => Seen<CustomerReaction>(r => r.Kind == CustomerReactionKind.Heart), frames: 8);
        Live("04-three-stars-locals-puzzled-by-labels", () =>
        {
            RunToNextDayStocked();
            SetReputation(55);
            Send(new RestockAll());
            WalkTo(CashierCell);
            Send(new OpenStore());
            SkipToJustBefore(e => e is CustomerReaction { Kind: CustomerReactionKind.Confused });
        }, () => Seen<CustomerReaction>(r => r.Kind == CustomerReactionKind.Confused), frames: 8);
        Step("05-restaurant-buyer-offers-a-group-order", () =>
        {
            RunToNextDay();
            ReachStage(2);
            PrepareMeatCorner();
            ReachStage(3);
            Send(new PlaceFixture("fish_tank", TankCells[0]));
            SetReputation(60);
            WaitForOrderableOffer();
            Hud.ShowPanel<GroupOrderPanel>();
        });
        Step("06-accepted-goods-set-aside", () =>
        {
            // Goods can only be ordered once the doors close; the offer holds until the next morning.
            for (var i = 0; i < 100_000 && Sim.Clock.Phase != DayPhase.Closed; i++) Sim.Tick();
            var offer = Sim.GroupOrders.Single(o => o.Status == GroupOrderStatus.Offered);
            Send(new AcceptGroupOrder(offer.Id));
            foreach (var line in offer.Lines) Send(new PlaceOrder(line.ProductId, line.Units));
            Send(new StartNextDay());
            Hud.ShowPanel<GroupOrderPanel>();
        });
        Live("07-group-order-delivered-at-closing", () =>
        {
            Send(new RestockAll());
            WalkTo(CashierCell);
            Send(new OpenStore());
            for (var i = 0; i < 100_000 && Sim.Clock.MinuteOfDay < Sim.Data.Economy.CloseMinuteOfDay - 1; i++) Sim.Tick();
        }, () => Seen<GroupOrderClosed>(c => c.Outcome == GroupOrderOutcome.Delivered), frames: 10);
        Step("08-upgrade-panel-bilingual-labels", () =>
        {
            Send(new StartNextDay());
            Hud.ShowPanel<UpgradePanel>();
        });
        Step("09-labels-up-reviews-and-trend-chart", () =>
        {
            Send(new BuyBilingualLabels());
            for (var day = 0; day < 4; day++)
            {
                Send(new RestockAll());
                WalkTo(CashierCell);
                RunToNextDayStocked();
            }

            Hud.ShowPanel<ReviewsPanel>();
        });
    }

    /// <summary>Plays to closing, orders what the shelves sell for tomorrow, and starts the next day with the shelves filled.</summary>
    private void RunToNextDayStocked()
    {
        if (Sim.Clock.Phase == DayPhase.PreOpen) Send(new OpenStore());
        for (var i = 0; i < 100_000 && Sim.Clock.Phase != DayPhase.Closed; i++) Sim.Tick();
        OrderShelfGoods();
        Send(new StartNextDay());
        Send(new RestockAll());
    }

    /// <summary>After closing: a slot's worth of every orderable product assigned to a display, delivered next morning.</summary>
    private void OrderShelfGoods()
    {
        foreach (var id in Sim.Shelves.Assigned.Values.Distinct().Order(StringComparer.Ordinal))
        {
            var product = Sim.Data.Products.Single(p => p.Id == id);
            if (product.Orderable && Sim.Inventory.Total(id) < product.UnitsPerShelfSlot) Send(new PlaceOrder(id, product.UnitsPerShelfSlot));
        }
    }

    /// <summary>Sets reputation by editing a save (a developer shortcut, like <see cref="ReachStage"/>).</summary>
    private void SetReputation(int value)
    {
        var json = System.Text.RegularExpressions.Regex.Replace(SaveGame.Write(Sim), "\"reputationMilli\":\\d+", $"\"reputationMilli\":{value * 1000}");
        Host.LoadFromJson(json);
    }

    /// <summary>
    /// Plays a copy of the game ahead to find when an event happens, then ticks the real game to a few ticks before it,
    /// so a live shot catches the moment.
    /// </summary>
    private void SkipToJustBefore(Func<SimEvent, bool> match, int lead = 6)
    {
        var copy = SaveGame.Read(Sim.Data, SaveGame.Write(Sim));
        var ticks = 0;
        for (; ticks < 200_000; ticks++)
        {
            copy.Tick();
            if (copy.DrainEvents().Any(match)) break;
        }

        for (var i = 0; i < ticks - lead; i++) Sim.Tick();
    }

    /// <summary>Plays days until a restaurant buyer offers an order for goods the wholesaler sells; other offers are declined.</summary>
    private void WaitForOrderableOffer()
    {
        for (var i = 0; i < 2_000_000; i++)
        {
            if (Sim.GroupOrders.FirstOrDefault(o => o.Status == GroupOrderStatus.Offered) is { } offer)
            {
                if (offer.Lines.All(l => Sim.Data.Products.Single(p => p.Id == l.ProductId).Orderable)) return;
                Send(new DeclineGroupOrder(offer.Id));
            }

            if (Sim.Clock.Phase == DayPhase.Closed)
            {
                OrderShelfGoods();
                Send(new StartNextDay());
            }

            if (Sim.Clock.Phase == DayPhase.PreOpen)
            {
                // Keep the store at three stars while waiting (each closing pulls reputation toward the day's average).
                if (Sim.Stars < 3) SetReputation(60);
                Send(new RestockAll());
                WalkTo(CashierCell);
                Send(new OpenStore());
            }

            Sim.Tick();
        }
    }

    // ---------- M14: imports and new goods ----------

    /// <summary>Spec 17.6 item 4: an import order, the ship at sea, pallets arriving (one by the door), unloading, new goods out.</summary>
    private void PlanImports()
    {
        Step("01-import-tab", () =>
        {
            ReachStage(2);
            Hud.ShowPanel<OrderPanel>();
            Hud.Panel<OrderPanel>().ShowImports();
        });
        Step("02-ship-at-sea", () =>
        {
            var min = Sim.Data.Products.Single(p => p.Id == "doubanjiang").Import!.MinUnits;
            Send(new Sim.Imports.PlaceImportOrder("doubanjiang", min));
            // A second, huge order will not fit the backroom when it lands.
            Send(new Sim.Imports.PlaceImportOrder("seaweed", 12 * Sim.Data.Products.Single(p => p.Id == "seaweed").UnitsPerStorageSlot));
            Hud.ShowPanel<OrderPanel>();
            Hud.Panel<OrderPanel>().ShowImports();
        });
        Step("03-pallets-arrive-one-by-the-door", () =>
        {
            for (var day = 0; day < 20 && Sim.Shipments.Count > 0; day++) RunToNextDay();
            Store.DebugHover = Sim.Layout.Find(FirstOf("backroom"))!.Origin;
        });
        Live("04-stocker-unloads", () =>
        {
            Store.DebugHover = null;
            Send(new HireEmployee("stocker"));
        }, () => Sim.Workers.Any(w => w.Step == WorkStep.Unloading), frames: 6);
        Step("05-imported-goods-on-a-new-shelf", () =>
        {
            for (var i = 0; i < 3000 && Sim.Pallets.Any(p => p.FloorCell is null); i++) Sim.Tick();
            Send(new PlaceFixture("shelf", new GridPoint(7, 3)));
            var shelf = Sim.Layout.Fixtures[^1].InstanceId;
            Send(new AssignShelfSlot(shelf, 0, "doubanjiang"));
            Send(new AssignShelfSlot(shelf, 1, "seaweed"));
            Send(new RestockAll());
            // The stocker may have taken the restocking: let them finish.
            for (var i = 0; i < 1500 && Sim.Inventory.Count(new Sim.Inventory.StockLocation(shelf, 0), "doubanjiang") == 0; i++) Sim.Tick();
            Store.DebugHover = new GridPoint(7, 3);
        });
    }

    // ---------- M12: calendar, festivals, events ----------

    private IReadOnlyList<SimEvent> _morning = [];

    /// <summary>
    /// Skips the calendar ahead: the game is saved, moved to the close of the day before (with any given events added
    /// to the ones in progress), loaded, and the next morning started (its reminders and events kept in _morning).
    /// </summary>
    private void Jump(int toDay, params System.Text.Json.Nodes.JsonObject[] events)
    {
        if (Sim.Clock.Phase == DayPhase.Open) RunToNextDay();

        // Skipped days sold nothing, so the shelves' goods are reordered (and cash topped up) to keep the store stocked.
        var root = System.Text.Json.Nodes.JsonNode.Parse(SaveGame.Write(Sim))!.AsObject();
        root["cashCents"] = Math.Max(Sim.Economy.CashCents, 500_000);
        Host.LoadFromJson(root.ToJsonString());
        foreach (var id in Sim.Shelves.Assigned.Values.Distinct().Where(id => Sim.Data.Products.Single(p => p.Id == id).Orderable && Sim.InOrderWindow(id)))
        {
            var want = Math.Min(24 - Sim.Inventory.Total(id), Sim.RoomFor(id));
            if (want > 0) Send(new PlaceOrder(id, want));
        }

        root = System.Text.Json.Nodes.JsonNode.Parse(SaveGame.Write(Sim))!.AsObject();
        root["day"] = toDay - 1;
        root["phase"] = "closed";
        root["today"]!["day"] = toDay;
        foreach (var e in events) root["calendar"]!["events"]!.AsArray().Add(e);
        Host.LoadFromJson(root.ToJsonString());
        Sim.Enqueue(new StartNextDay());
        Sim.Tick();
        _morning = Sim.DrainEvents();
    }

    private static System.Text.Json.Nodes.JsonObject TourEvent(int id, string eventId, int day, string status = "announced", System.Text.Json.Nodes.JsonObject? extra = null)
    {
        var e = new System.Text.Json.Nodes.JsonObject
        {
            ["id"] = id, ["eventId"] = eventId, ["announcedDay"] = day, ["startDay"] = day, ["endDay"] = day, ["status"] = status,
        };
        foreach (var (k, v) in extra ?? []) e[k] = v?.DeepClone();
        return e;
    }

    private void ShowAnnounced(int id)
    {
        var e = Sim.StoreEvents.Single(x => x.Id == id);
        Hud.ShowNotice(new Sim.Calendar.StoreEventAnnounced(e));
    }

    private void PlanCalendar()
    {
        Step("01-hud-date-and-calendar-panel", () => Hud.ShowPanel<CalendarPanel>());
        Step("02-daily-report-with-season-report-button", () =>
        {
            RunToNextDay();
            Jump(28);
            Send(new RestockAll());
            WalkTo(new GridPoint(2, 4));
            RunToNextDay();
            Hud.ShowReport(Sim.Reports[^1]);
        });
        Step("03-season-report", () => Hud.ShowSeasonReport(Sim.QuarterReports()[^1]));
        Step("04-mid-autumn-reminder", () =>
        {
            Jump(64);
            foreach (var r in _morning.OfType<Sim.Calendar.FestivalReminder>()) Hud.ShowNotice(r);
        });
        Step("05-calendar-prep-days-and-festival-goods", () => Hud.ShowPanel<CalendarPanel>());
        Step("06-order-screen-with-mooncakes", () => Hud.ShowPanel<OrderPanel>());
        Step("07-lantern-hung-and-mooncakes-shelved", () =>
        {
            Send(new Sim.Calendar.BuyDecoration("rabbit_lantern"));
            Send(new Sim.Calendar.HangDecoration("rabbit_lantern", 5));
            Send(new PlaceFixture("shelf", new GridPoint(7, 3)));
            var shelf = Sim.Layout.Fixtures[^1].InstanceId;
            Send(new AssignShelfSlot(shelf, 0, "mooncake"));
            Send(new AssignShelfSlot(shelf, 1, "mooncake"));
            Send(new AssignShelfSlot(shelf, 2, "mooncake_box"));
            Send(new AssignShelfSlot(shelf, 3, "mooncake_box"));
            Send(new PlaceOrder("mooncake", 24));
            Send(new PlaceOrder("mooncake_box", 8));

            RunToNextDay();
            Send(new RestockAll());
            Hud.ShowPanel<CalendarPanel>();
        });
        Step("08-store-dressed-for-mid-autumn", () => Send(new RestockAll()));
        Live("09-mid-autumn-day-crowd", () =>
        {
            Jump(71);
            Send(new RestockAll());
            WalkTo(new GridPoint(2, 4));
            FeelConfig.Load().SpeechChancePerGameMinute = 1;
            Host.Send(new OpenStore());
        }, () => Sim.Customers.Count >= 2, frames: 30);
        Step("10-power-cut-notice", () =>
        {
            Jump(Sim.Clock.Day + 1, TourEvent(901, "power_cut", Sim.Clock.Day + 1));
            ShowAnnounced(901);
        });
        Live("11-power-out", () =>
        {
            Send(new RestockAll());
            WalkTo(new GridPoint(2, 4));
            Host.Send(new OpenStore());
            for (var i = 0; i < 20_000 && !Sim.PowerOut; i++) Sim.Tick();
        }, () => Sim.PowerOut, frames: 10);
        Step("12-inspection-notice", () =>
        {
            Jump(Sim.Clock.Day + 1, TourEvent(902, "hygiene_inspection", Sim.Clock.Day + 1));
            ShowAnnounced(902);
        });
        Live("13-inspector-in-the-store", () =>
        {
            Send(new RestockAll());
            WalkTo(new GridPoint(2, 4));
            Host.Send(new OpenStore());
            for (var i = 0; i < 20_000 && !Sim.InspectorPresent; i++) Sim.Tick();
        }, () => Sim.InspectorPresent, frames: 10);
        Step("14-clearance-offer", () =>
        {
            var day = Sim.Clock.Day + 1;
            Jump(day, TourEvent(903, "clearance_offer", day, "offered", new()
            {
                ["productId"] = "soy_milk", ["goods"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject { ["productId"] = "soy_milk", ["quantity"] = 20 }),
                ["unitCostCents"] = 36, ["bestBeforeDay"] = day + 2,
            }));
            ShowAnnounced(903);
        });
        Step("15-group-order", () =>
        {
            var day = Sim.Clock.Day;
            Jump(day + 1, TourEvent(904, "group_order", day + 1, "offered", new()
            {
                ["startDay"] = day + 2, ["endDay"] = day + 2,
                ["goods"] = new System.Text.Json.Nodes.JsonArray(
                    new System.Text.Json.Nodes.JsonObject { ["productId"] = "rice_jasmine", ["quantity"] = 8 },
                    new System.Text.Json.Nodes.JsonObject { ["productId"] = "jasmine_tea", ["quantity"] = 12 }),
            }), TourEvent(905, "supplier_stockout", day + 1, extra: new() { ["startDay"] = day + 2, ["endDay"] = day + 3, ["productId"] = "chili_crisp" }));
            ShowAnnounced(904);
        });
        Step("16-calendar-with-events", () => Hud.ShowPanel<CalendarPanel>());
    }

    // ---------- playtest fixes, 2026-10-25 ----------


    private void PlanFixes()
    {
        Step("01-order-screen-storage-and-slots", () =>
        {
            RunToNextDay();
            Hud.ShowPanel<OrderPanel>();
        });
        Step("02-backroom-shows-its-goods", () =>
        {
            Send(new PlaceOrder("noodles_instant", 30));
            Store.DebugHover = Sim.Layout.Find(FirstOf("backroom"))!.Origin;
        });
        Step("03-next-day-reminder-without-orders", () =>
        {
            Store.DebugHover = null;
            // Nothing on order at closing: "next day" asks first.
            RunToNextDay();
            WalkTo(CashierCell);
            Send(new OpenStore());
            for (var i = 0; i < 100_000 && Sim.Clock.Phase != DayPhase.Closed; i++) Sim.Tick();
            Hud.ShowReport(Sim.Reports[^1]);
            Hud.Panel<ReportPanel>().RequestNextDay();
        });
        Step("04-meat-counter-must-touch-the-wall", () =>
        {
            Send(new StartNextDay());
            ReachStage(2);
            Ghost("meat_display", new GridPoint(8, 4));
        });
        Step("05-meat-counter-against-the-wall-fits", () => Ghost("meat_display", new GridPoint(16, 4)));
    }

    private GridPoint Above(int instanceId)
    {
        var f = Sim.Layout.Find(instanceId)!;
        return new GridPoint(f.Origin.X, f.Origin.Y - 1);
    }

    /// <summary>The player works every Cook task, start to finish, until nothing is left to cook.</summary>
    private void CookEverything()
    {
        for (var guard = 0; guard < 20 && Sim.Tasks.Tasks.OfType<CookTask>().FirstOrDefault() is { } task; guard++)
        {
            WalkTo(Below(task.StoveInstanceId));
            Send(new PlayerInteract(task.StoveInstanceId));
            for (var i = 0; i < 2000 && Sim.Player.Step != WorkStep.Carrying; i++) Sim.Tick();
            WalkTo(Above(task.Target.FixtureInstanceId));
            Send(new PlayerInteract(task.Target.FixtureInstanceId));
            for (var i = 0; i < 500 && Sim.Player.Step != WorkStep.Idle; i++) Sim.Tick();
        }
    }

    private bool HasDeadThatKeeps(int tank) =>
        Sim.DeadIn(tank).Any(l => Sim.Data.Products.Single(p => p.Id == l.ProductId).Live?.DiesAs is not null);

    private void OrderSeafood()
    {
        foreach (var product in TankProducts.SelectMany(p => p).Append("clams"))
        {
            Send(new PlaceOrder(product, Sim.Data.Products.Single(p => p.Id == product).UnitsPerShelfSlot));
        }
    }

    /// <summary>The player cleans one tank, start to finish.</summary>
    private void CleanTank(int tank)
    {
        Ticks(1);
        if (!Sim.Tasks.Tasks.OfType<CleanTankTask>().Any(t => t.TankInstanceId == tank)) return;

        WalkTo(Below(tank));
        Send(new PlayerInteract(tank));
        for (var i = 0; i < 500 && Sim.Player.Step != WorkStep.Idle; i++) Sim.Tick();
    }

    public override void _Process(double delta)
    {
        if (_focus is { } focus) Camera.CenterOn(focus);

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
        Send(new PlaceFixture("meat_display", new GridPoint(16, 4)));
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
