using ChineseGrocery.Sim.Calendar;
using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Time;
using ChineseGrocery.Sim.Tests.Seafood;
using static ChineseGrocery.Sim.Tests.Calendar.CalendarScenario;

namespace ChineseGrocery.Sim.Tests.Calendar;

/// <summary>Spec 15.3: random events from events.json, rolled each morning on their own seeded stream.</summary>
public class EventTests
{
    private const string Rice = "rice_jasmine";

    private static RejectReason? Send(GameSim sim, SimCommand command)
    {
        sim.DrainEvents();
        sim.Run(command);
        return sim.DrainEvents().OfType<CommandRejected>().SingleOrDefault()?.Reason;
    }

    private static StoreEvent Single(GameSim sim) => Assert.Single(sim.StoreEvents);

    private static GameSim Morning(GameData data, int day = 5, ulong seed = 1, params string[] selling)
    {
        var sim = Store(data, day - 1, seed);
        for (var i = 0; i < selling.Length; i++) Shelve(sim, selling[i], 1, slot: i);
        NextMorning(sim);
        return sim;
    }

    private static List<string> History(ulong seed, int days)
    {
        var sim = Store(Quiet(), seed: seed);
        Shelve(sim, Rice, 4);
        Shelve(sim, "soy_milk", 4, slot: 1);
        var seen = new List<string>();
        for (var day = 1; day < days; day++)
        {
            NextMorning(sim);
            seen.AddRange(sim.DrainEvents().OfType<StoreEventAnnounced>().Select(a => $"{sim.Clock.Day}:{a.Event.EventId}:{a.Event.ProductId}"));
            if (sim.StoreEvents.Any(e => e.Status == StoreEventStatus.Offered)) sim.Run(new OpenStore());
        }

        return seen;
    }

    [Fact]
    public void The_same_seed_gives_the_same_events_and_another_seed_others()
    {
        var a = History(1, 150);

        Assert.True(a.Count > 10, $"only {a.Count} events in 150 days");
        Assert.Equal(a, History(1, 150));
        Assert.NotEqual(a, History(2, 150));
    }

    [Fact]
    public void Rolling_events_leaves_the_customers_random_stream_alone()
    {
        var withEvents = Store(Quiet(SimTestKit.Data with { Events = SimTestKit.RealData.Events }));
        var without = Store(Quiet(SimTestKit.Data));
        var announced = 0;
        for (var day = 1; day < 30; day++)
        {
            NextMorning(withEvents);
            NextMorning(without);
            announced += withEvents.DrainEvents().OfType<StoreEventAnnounced>().Count();
        }

        Assert.True(announced > 0);
        Assert.Equal(without.Rng.State, withEvents.Rng.State);
    }

    [Fact]
    public void At_most_one_negative_event_is_known_or_running_at_a_time()
    {
        var data = Quiet() with { Events = SimTestKit.RealData.Events! with { FirstDay = 1, DailyChancePermille = 1000, Events = SimTestKit.RealData.Events.Events.Select(e => e with { MinStage = 1 }).ToList() } };
        var sim = Store(data);
        Shelve(sim, Rice, 4);
        var negatives = 0;
        for (var day = 1; day < 200; day++)
        {
            NextMorning(sim);
            var known = sim.StoreEvents.Count(e => sim.EventDefOf(e).Negative);
            Assert.True(known <= 1, $"day {sim.Clock.Day}: {known} negative events");
            negatives += sim.DrainEvents().OfType<StoreEventAnnounced>().Count(a => sim.EventDefOf(a.Event).Negative);
            if (sim.StoreEvents.Any(e => e.Status == StoreEventStatus.Offered)) sim.Run(new OpenStore());
        }

        Assert.True(negatives > 20);
    }

    [Fact]
    public void The_inspector_never_comes_on_a_festival_day()
    {
        var data = OnlyEvent("hygiene_inspection");
        foreach (var day in new[] { DragonBoat, MidAutumn, Thanksgiving, SpringFestival })
        {
            Assert.Empty(Morning(data, day).StoreEvents);
        }

        Assert.Equal("hygiene_inspection", Single(Morning(data, MidAutumn + 1)).EventId);
    }

    [Fact]
    public void A_stockout_is_announced_a_day_ahead_and_blocks_orders_while_it_runs()
    {
        var sim = Morning(OnlyEvent("supplier_stockout", e => e with { DurationDays = new IntRange(2, 2) }), selling: Rice);
        var stockout = Single(sim);

        Assert.Equal((Rice, 5, 6, 7), (stockout.ProductId, stockout.AnnouncedDay, stockout.StartDay, stockout.EndDay));
        Assert.Null(Send(sim, new PlaceOrder(Rice, 1)));
        sim.Clock.Day = 6;
        Assert.Equal(RejectReason.SupplierOutOfStock, Send(sim, new PlaceOrder(Rice, 1)));
        Assert.Null(Send(sim, new PlaceOrder("soy_milk", 1)));
        sim.Clock.Day = 8;
        Assert.Null(Send(sim, new PlaceOrder(Rice, 1)));
    }

    [Fact]
    public void A_cost_rise_raises_what_orders_in_that_department_cost()
    {
        var sim = Morning(OnlyEvent("cost_rise", e => e with { DurationDays = new IntRange(3, 3) }), selling: Rice);
        var rise = Single(sim);
        var rice = Product(sim, Rice);

        Assert.Equal(Department.DryGoods, rise.Department);
        Assert.Equal(rice.CostCents, sim.CostOf(rice));
        sim.Clock.Day = rise.StartDay;
        var cash = sim.Economy.CashCents;
        Assert.Null(Send(sim, new PlaceOrder(Rice, 10)));

        var raised = rice.CostCents * 1300 / 1000;
        Assert.Equal(raised, sim.CostOf(rice));
        Assert.Equal(cash - 10 * raised, sim.Economy.CashCents);
        Assert.All(sim.PendingOrders, o => Assert.Equal(raised, o.UnitCostCents));
        Assert.Equal(Product(sim, "soy_milk").CostCents, sim.CostOf(Product(sim, "soy_milk")));
    }

    [Fact]
    public void Rain_keeps_shoppers_away_and_an_influencer_brings_crowds_for_snacks_and_deli()
    {
        var rain = Morning(OnlyEvent("rainy_day"));
        Assert.Equal(600, rain.ArrivalPermille());

        var influencer = Morning(OnlyEvent("influencer_visit"));
        Assert.Equal(1800, influencer.ArrivalPermille());
        Assert.Equal(2000, influencer.DemandOf(Product(influencer, "shrimp_chips")).Permille);
        Assert.Equal(1000, influencer.DemandOf(Product(influencer, Rice)).Permille);
        influencer.Clock.Day++;
        Assert.Equal(1000, influencer.ArrivalPermille());
    }

    [Fact]
    public void Accepting_a_clearance_offer_puts_cheap_short_dated_stock_in_storage()
    {
        var sim = Morning(OnlyEvent("clearance_offer"), selling: "soy_milk");
        var offer = Single(sim);
        var units = offer.Goods![0].Quantity;
        var cash = sim.Economy.CashCents;

        Assert.Equal(StoreEventStatus.Offered, offer.Status);
        Assert.Equal(Product(sim, "soy_milk").CostCents * 400 / 1000, offer.UnitCostCents);
        Assert.Null(Send(sim, new AnswerOffer(offer.Id, true)));

        var storage = StockLocation.Storage(sim.InstanceOf("backroom"));
        Assert.Equal(units, sim.Inventory.Count(storage, "soy_milk"));
        Assert.All(sim.Inventory.Get(storage).Lots, l => Assert.Equal((sim.Clock.Day + 2, offer.UnitCostCents), (l.BestBeforeDay, l.UnitCostCents)));
        Assert.Equal(cash - units * offer.UnitCostCents, sim.Economy.CashCents);
        Assert.Equal(units * offer.UnitCostCents, sim.Economy.Today.PurchaseCostCents);
        Assert.Equal(RejectReason.OfferClosed, Send(sim, new AnswerOffer(offer.Id, true)));
    }

    [Fact]
    public void Declined_or_unanswered_offers_buy_nothing()
    {
        var declined = Morning(OnlyEvent("clearance_offer"), selling: "soy_milk");
        Assert.Null(Send(declined, new AnswerOffer(Single(declined).Id, false)));
        Assert.Equal(StoreEventStatus.Declined, Single(declined).Status);

        var ignored = Morning(OnlyEvent("clearance_offer"), selling: "soy_milk");
        ignored.Run(new OpenStore());
        Assert.Equal(StoreEventStatus.Declined, Single(ignored).Status);
        Assert.Equal(RejectReason.WrongPhase, Send(ignored, new AnswerOffer(Single(ignored).Id, true)));
        Assert.Equal(1, ignored.Inventory.Total("soy_milk"));
    }

    private static GameData OneProductGroupOrder() =>
        OnlyEvent("group_order", e => e with { GroupOrder = e.GroupOrder! with { Products = new IntRange(1, 1), Units = new IntRange(6, 6) } });

    [Fact]
    public void A_group_order_collected_on_time_pays_above_the_suggested_price()
    {
        var sim = Morning(OneProductGroupOrder(), selling: Rice);
        var order = Single(sim);
        Assert.Equal((5, 6, 6), (order.AnnouncedDay, order.StartDay, order.EndDay));
        Assert.Equal([new StockEntryDef(Rice, 6)], order.Goods);
        Assert.Null(Send(sim, new AnswerOffer(order.Id, true)));

        sim.OpenAndRunToClose();
        NextMorning(sim);
        var backroom = StockLocation.Storage(sim.InstanceOf("backroom"));
        sim.Inventory.GetOrCreate(backroom).Add(new StockLot(Rice, 10, 999, Product(sim, Rice).CostCents));
        sim.DrainEvents();
        OpenUntil(sim, 12 * 60 + 1);

        var done = sim.DrainEvents().OfType<GroupOrderFinished>().Single();
        var expected = 6 * Product(sim, Rice).SuggestedPriceCents * 1300 / 1000;
        Assert.True(done.Delivered);
        Assert.Equal(expected, done.RevenueCents);
        Assert.Equal(expected, sim.Economy.Today.RevenueCents);
        Assert.Equal(4, sim.Inventory.Count(backroom, Rice));
        Assert.Equal(StoreEventStatus.Delivered, Single(sim).Status);
    }

    [Fact]
    public void A_group_order_that_cannot_be_filled_takes_nothing()
    {
        var sim = Morning(OneProductGroupOrder(), selling: Rice);
        sim.Run(new AnswerOffer(Single(sim).Id, true));
        sim.OpenAndRunToClose();
        NextMorning(sim);
        sim.DrainEvents();
        OpenUntil(sim, 12 * 60 + 1);

        Assert.False(sim.DrainEvents().OfType<GroupOrderFinished>().Single().Delivered);
        Assert.Equal(1, sim.Inventory.Total(Rice));
        Assert.Equal(0, sim.Economy.Today.RevenueCents);
        Assert.Equal(StoreEventStatus.Missed, Single(sim).Status);
    }

    // --- Hygiene inspection ---

    private static InspectionDef Rules => SimTestKit.RealData.Events!.Events.Single(e => e.Id == "hygiene_inspection").Inspection!;

    private static GameSim SeafoodAndProduce(GameData? data = null)
    {
        var d = SeafoodScenario.Unlocked(data ?? Quiet());
        d = d with { Fixtures = d.Fixtures.Select(f => f.Id == "produce_stand" ? f with { UnlockStage = 1 } : f).ToList() };
        return SimTestKit.NewSim(data: d);
    }

    [Fact]
    public void A_clean_store_passes()
    {
        var sim = SeafoodAndProduce();
        SeafoodScenario.Tank(sim, 7, SeafoodScenario.Fish);
        SeafoodScenario.Place(sim, "produce_stand", 6, 3, "napa_cabbage");
        Shelve(sim, Rice, 4);

        Assert.Equal(new HygieneFindings(false, false, false), sim.CheckHygiene(Rules));
    }

    [Fact]
    public void Each_check_fails_on_its_own_problem()
    {
        var sim = SeafoodAndProduce();
        var tank = SeafoodScenario.Tank(sim, 7, SeafoodScenario.Fish);
        var stand = SeafoodScenario.Place(sim, "produce_stand", 6, 3, "napa_cabbage");

        Shelve(sim, Rice, 4, bestBefore: sim.Clock.Day - 1);
        Assert.Equal(new HygieneFindings(true, false, false), sim.CheckHygiene(Rules));

        var clean = SeafoodAndProduce();
        var cleanTank = SeafoodScenario.Tank(clean, 7, SeafoodScenario.Fish);
        clean.AddDead(cleanTank, SeafoodScenario.DeadLot(clean, SeafoodScenario.Fish, 1));
        Assert.True(clean.CheckHygiene(Rules).SpoiledGoods);

        var water = SeafoodAndProduce();
        var dirtyTank = SeafoodScenario.Tank(water, 7, SeafoodScenario.Fish);
        water.SetWaterQuality(dirtyTank, Rules.MinWaterQuality - 1);
        Assert.Equal(new HygieneFindings(false, true, false), water.CheckHygiene(Rules));
        water.SetWaterQuality(dirtyTank, Rules.MinWaterQuality);
        Assert.False(water.CheckHygiene(Rules).DirtyWater);

        sim.SetTidiness(stand, Rules.MinTidiness - 1);
        Assert.Equal(new HygieneFindings(true, false, true), sim.CheckHygiene(Rules));
        sim.SetTidiness(stand, Rules.MinTidiness);
        Assert.False(sim.CheckHygiene(Rules).MessyStands);
        _ = tank;
    }

    [Fact]
    public void The_inspector_visits_then_rewards_a_pass_or_fines_each_problem()
    {
        var pass = Morning(OnlyEvent("hygiene_inspection"));
        var cash = pass.Economy.CashCents;
        OpenUntil(pass, Rules.ArriveMinuteOfDay);
        Assert.False(pass.InspectorPresent);
        pass.Tick();
        Assert.True(pass.InspectorPresent);
        OpenUntil(pass, Rules.ArriveMinuteOfDay + Rules.StayMinutes + 1);
        Assert.False(pass.InspectorPresent);
        Assert.Equal(StoreEventStatus.Passed, Single(pass).Status);
        Assert.Equal(cash + Rules.RewardCents, pass.Economy.CashCents);
        Assert.Equal(-Rules.RewardCents, pass.Economy.Today.EventCostCents);

        var fail = Morning(OnlyEvent("hygiene_inspection"));
        Shelve(fail, Rice, 4, bestBefore: fail.Clock.Day - 1);
        cash = fail.Economy.CashCents;
        fail.DrainEvents();
        OpenUntil(fail, Rules.ArriveMinuteOfDay + Rules.StayMinutes + 1);
        var verdict = fail.DrainEvents().OfType<InspectionFinished>().Single();
        Assert.Equal(-Rules.FinePerProblemCents, verdict.CashCents);
        Assert.Equal(cash - Rules.FinePerProblemCents, fail.Economy.CashCents);
        Assert.Equal(StoreEventStatus.Failed, Single(fail).Status);
    }

    // --- Power cut ---

    private static PowerCutDef Cut => SimTestKit.RealData.Events!.Events.Single(e => e.Id == "power_cut").PowerCut!;

    private static GameSim PowerCutStore(bool generator)
    {
        var data = SeafoodScenario.Unlocked(OnlyEvent("power_cut", e => e with { MinStage = 1 }));
        var sim = Store(data, day: 4);
        NextMorning(sim);
        if (generator) Assert.Null(Send(sim, new RentGenerator(Single(sim).Id)));
        return sim;
    }

    private static (int Tank, int Ice) Stock(GameSim sim)
    {
        var tank = SeafoodScenario.Tank(sim, 7, SeafoodScenario.Fish);
        var ice = SeafoodScenario.Place(sim, "ice_table", 6, 3, "clams");
        SeafoodScenario.Stock(sim, ice, 0, "clams", 5);
        SeafoodScenario.Stock(sim, tank, 0, SeafoodScenario.Fish, 3);
        Shelve(sim, Rice, 4);
        return (tank, ice);
    }

    [Fact]
    public void A_power_cut_ages_chilled_goods_and_fouls_tank_water_faster()
    {
        var cut = PowerCutStore(generator: false);
        var normal = PowerCutStore(generator: true);
        var (cutTank, cutIce) = Stock(cut);
        var (normalTank, normalIce) = Stock(normal);
        var clamsBefore = cut.Inventory.Get(new StockLocation(cutIce, 0)).Lots.Single().BestBeforeDay;

        OpenUntil(cut, Cut.EndMinuteOfDay);
        OpenUntil(normal, Cut.EndMinuteOfDay);

        Assert.Equal(clamsBefore - Cut.AgeDays, cut.Inventory.Get(new StockLocation(cutIce, 0)).Lots.Single().BestBeforeDay);
        Assert.Equal(clamsBefore, normal.Inventory.Get(new StockLocation(normalIce, 0)).Lots.Single().BestBeforeDay);
        Assert.Equal(4, cut.Inventory.Count(new StockLocation(cut.InstanceOf("shelf"), 0), Rice));
        Assert.All(cut.Inventory.Get(new StockLocation(cut.InstanceOf("shelf"), 0)).Lots, l => Assert.Equal(cut.Clock.Day + Product(cut, Rice).ShelfLifeDays - 1, l.BestBeforeDay));
        Assert.True(cut.WaterQualityOf(cutTank) < normal.WaterQualityOf(normalTank));
        var normalDrop = 100 - normal.WaterQualityOf(normalTank);
        Assert.True(100 - cut.WaterQualityOf(cutTank) > 2 * normalDrop, $"cut drop {100 - cut.WaterQualityOf(cutTank)} vs {normalDrop}");
    }

    [Fact]
    public void Goods_on_their_last_day_are_lost_to_a_power_cut()
    {
        var sim = PowerCutStore(generator: false);
        var (_, ice) = Stock(sim);
        sim.Inventory.Get(new StockLocation(ice, 0)).ShortenShelfLife(_ => true, 2);
        sim.DrainEvents();

        OpenUntil(sim, Cut.StartMinuteOfDay + 1);

        var started = sim.DrainEvents().OfType<PowerCutStarted>().Single();
        Assert.False(started.Generator);
        Assert.Equal(5 * Product(sim, "clams").CostCents, started.LostCents);
        Assert.Equal(0, sim.Inventory.Count(new StockLocation(ice, 0), "clams"));
        Assert.Equal(started.LostCents, sim.Economy.Today.SeafoodLossCents);
        Assert.True(sim.PowerOut);
    }

    [Fact]
    public void A_generator_costs_money_and_keeps_the_power_on()
    {
        var sim = PowerCutStore(generator: false);
        var e = Single(sim);
        var cash = sim.Economy.CashCents;

        Assert.Null(Send(sim, new RentGenerator(e.Id)));
        Assert.Equal(RejectReason.OfferClosed, Send(sim, new RentGenerator(e.Id)));
        Assert.Equal(cash - Cut.GeneratorCostCents, sim.Economy.CashCents);
        Assert.Equal(Cut.GeneratorCostCents, sim.Economy.Today.EventCostCents);

        OpenUntil(sim, Cut.StartMinuteOfDay + 1);
        Assert.False(sim.PowerOut);
        Assert.Equal(StoreEventStatus.Started, Single(sim).Status);
    }

    [Fact]
    public void The_daily_report_lists_the_festival_and_the_events_and_balances()
    {
        var sim = Morning(OnlyEvent("hygiene_inspection"), day: MidAutumn - 2);
        sim.OpenAndRunToClose();
        var report = sim.Reports[^1];

        Assert.Equal(("mid_autumn", -2), (report.FestivalId, report.FestivalDayOffset));
        Assert.Equal(["hygiene_inspection"], report.EventIds!);
        Assert.Equal(-Rules.RewardCents, report.EventCostCents);
        Assert.Equal(report.RevenueCents - report.PurchaseCostCents - report.WagesCents - report.FixtureSpendCents - report.EventCostCents, report.ProfitCents);
        Assert.Equal(report.CashEndCents - report.CashStartCents, report.ProfitCents);
    }
}
