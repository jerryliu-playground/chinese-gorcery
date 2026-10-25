using ChineseGrocery.Sim.Calendar;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Produce;
using ChineseGrocery.Sim.Save;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Core;

/// <summary>
/// Random events (events.json): each morning one may happen, never more than the allowed number of negative ones at a
/// time. Event randomness comes from its own seeded stream, so it never shifts the customers' rolls.
/// </summary>
public sealed partial class GameSim
{
    private const ulong EventStreamSalt = 0xCA1E_DA25_E7E1_7012;

    private readonly List<StoreEvent> _storeEvents = [];
    private SimRng _eventRng = null!;
    private int _nextStoreEventId = 1;

    /// <summary>Events announced, running or just finished; each is dropped the morning after its last day.</summary>
    public IReadOnlyList<StoreEvent> StoreEvents => _storeEvents;

    public EventDef EventDefOf(StoreEvent e) =>
        Data.Events?.Events.FirstOrDefault(d => d.Id == e.EventId) ?? throw new KeyNotFoundException($"Unknown event '{e.EventId}'.");

    /// <summary>The power is out right now (and no generator is running).</summary>
    public bool PowerOut => _storeEvents.Any(e => e.Status == StoreEventStatus.Started && !e.GeneratorRented && EventDefOf(e).Kind == EventKind.PowerCut);

    /// <summary>The hygiene inspector is in the store right now.</summary>
    public bool InspectorPresent => _storeEvents.Any(e => e.Status == StoreEventStatus.Started && EventDefOf(e).Kind == EventKind.Inspection);

    /// <summary>How fast tank water fouls compared with normal (permille): faster while the pumps are off.</summary>
    private int PowerCutWaterPermille() =>
        PowerOut ? _storeEvents.Where(e => e.Status == StoreEventStatus.Started).Select(EventDefOf).First(d => d.Kind == EventKind.PowerCut).PowerCut!.WaterDropPermille : 1000;

    /// <summary>Wholesale cost of one unit today, after any cost rise.</summary>
    public long CostOf(ProductDef product)
    {
        var cost = product.CostCents;
        foreach (var (e, def) in EventsOn(Clock.Day).Where(x => x.Def.Kind == EventKind.CostRise && x.Event.Department == product.Department))
        {
            cost = cost * def.CostPermille / 1000;
        }

        return cost;
    }

    /// <summary>Whether the supplier cannot deliver the product today.</summary>
    public bool IsOutOfStock(string productId) =>
        EventsOn(Clock.Day).Any(x => x.Def.Kind == EventKind.SupplierStockout && x.Event.ProductId == productId);

    private IEnumerable<(StoreEvent Event, EventDef Def)> EventsOn(int day) =>
        _storeEvents.Where(e => e.IsOn(day)).Select(e => (e, EventDefOf(e)));

    private void StartEventStream(ulong seed) => _eventRng = new SimRng(seed ^ EventStreamSalt);

    /// <summary>Each new morning: finished events are dropped, festival reminders go out, and a new event may happen.</summary>
    private void BeginCalendarDay()
    {
        _storeEvents.RemoveAll(e => e.EndDay < Clock.Day);
        RemindFestivals();
        RollStoreEvent();
    }

    private void RollStoreEvent()
    {
        if (Data.Events is not { } rules || Data.Calendar is null || Clock.Day < rules.FirstDay) return;
        if (_eventRng.NextInt(0, 1000) >= rules.DailyChancePermille) return;

        var possible = rules.Events.Where(d => CanHappen(d, rules)).ToList();
        if (possible.Count == 0) return;

        var def = possible[PickWeighted(_eventRng, possible.Select(d => d.Weight).ToList())];
        var start = Clock.Day + (def.Kind == EventKind.GroupOrder ? 1 : def.LeadDays);
        var days = def.DurationDays is { } range ? _eventRng.NextInt(range.Min, range.Max + 1) : 1;
        var e = new StoreEvent(_nextStoreEventId++, def.Id, Clock.Day, start, start + days - 1);
        e = def.Kind switch
        {
            EventKind.SupplierStockout => e with { ProductId = Pick(SellingProducts()).Id },
            EventKind.CostRise => e with { Department = Pick(SellingProducts().Select(p => p.Department).Distinct().Order().ToList()) },
            EventKind.ClearanceOffer => ClearanceOffer(e, def.Offer!),
            EventKind.GroupOrder => GroupOrderRequest(e, def.GroupOrder!),
            EventKind.ShipDelay => e with { DelayDays = _eventRng.NextInt(def.DelayDays!.Value.Min, def.DelayDays.Value.Max + 1) },
            _ => e,
        };

        _storeEvents.Add(e);
        _events.Add(new StoreEventAnnounced(e));
        if (def.Kind == EventKind.ShipDelay) DelayShipments(e.DelayDays);
    }

    private bool CanHappen(EventDef def, EventsDef rules)
    {
        if (def.MinStage > Economy.Stage || def.Weight <= 0) return false;
        if (def.Seasons is { } seasons && !seasons.Contains(Season!.Id)) return false;
        if (_storeEvents.Any(e => e.EventId == def.Id)) return false;
        if (def.Negative && _storeEvents.Count(e => EventDefOf(e).Negative) >= rules.MaxNegativeAtOnce) return false;

        return def.Kind switch
        {
            EventKind.SupplierStockout or EventKind.CostRise => SellingProducts().Count > 0,
            EventKind.Inspection => def.Inspection is not null && FestivalAround(Clock.Day + def.LeadDays) is not { Offset: 0 },
            EventKind.PowerCut => def.PowerCut is not null,
            EventKind.ClearanceOffer => def.Offer is { } offer && ClearanceCandidates(offer).Count > 0,
            EventKind.GroupOrder => def.GroupOrder is not null && SellingProducts().Any(p => p.Live is null),
            EventKind.ShipDelay => def.DelayDays is not null && Shipments.Count > 0,
            _ => true,
        };
    }

    /// <summary>Ordinary goods the store sells (assigned to a slot and bought from the wholesaler), in id order.</summary>
    private List<ProductDef> SellingProducts() =>
        Shelves.Assigned.Values.Distinct().Order(StringComparer.Ordinal)
            .Select(id => _products[id])
            .Where(p => p.Orderable && !IsFestivalGood(p.Id))
            .ToList();

    private List<ProductDef> ClearanceCandidates(ClearanceOfferDef offer) =>
        SellingProducts().Where(p => p.Live is null && RoomFor(p.Id) >= offer.Units.Min).ToList();

    private T Pick<T>(IReadOnlyList<T> items) => items[_eventRng.NextInt(0, items.Count)];

    private StoreEvent ClearanceOffer(StoreEvent e, ClearanceOfferDef offer)
    {
        var product = Pick(ClearanceCandidates(offer));
        var units = Math.Min(_eventRng.NextInt(offer.Units.Min, offer.Units.Max + 1), RoomFor(product.Id));
        return e with
        {
            Status = StoreEventStatus.Offered,
            ProductId = product.Id,
            Goods = [new StockEntryDef(product.Id, units)],
            UnitCostCents = Math.Max(1, product.CostCents * offer.CostPermille / 1000),
            BestBeforeDay = Clock.Day + Math.Min(offer.DaysLeft, product.ShelfLifeDays) - 1,
        };
    }

    private StoreEvent GroupOrderRequest(StoreEvent e, GroupOrderDef order)
    {
        var candidates = SellingProducts().Where(p => p.Live is null).ToList();
        var count = Math.Min(_eventRng.NextInt(order.Products.Min, order.Products.Max + 1), candidates.Count);
        var goods = new List<StockEntryDef>();
        for (var i = 0; i < count; i++)
        {
            var product = Pick(candidates);
            candidates.Remove(product);
            goods.Add(new StockEntryDef(product.Id, _eventRng.NextInt(order.Units.Min, order.Units.Max + 1)));
        }

        return e with { Status = StoreEventStatus.Offered, Goods = goods };
    }

    private static int PickWeighted(SimRng rng, IReadOnlyList<int> weights)
    {
        var roll = rng.NextInt(0, weights.Sum());
        for (var i = 0; i < weights.Count; i++)
        {
            roll -= weights[i];
            if (roll < 0) return i;
        }

        throw new InvalidOperationException("Weights must sum to more than zero.");
    }

    private StoreEvent? StoreEventById(int id) => _storeEvents.Find(e => e.Id == id);

    private void Replace(StoreEvent e) => _storeEvents[_storeEvents.FindIndex(x => x.Id == e.Id)] = e;

    /// <summary>Offers nobody answered lapse when the store opens.</summary>
    private void CloseOffers()
    {
        foreach (var e in _storeEvents.Where(e => e.Status == StoreEventStatus.Offered).ToList()) Replace(e with { Status = StoreEventStatus.Declined });
    }

    private Rejection? HandleAnswerOffer(AnswerOffer answer)
    {
        if (Clock.Phase != DayPhase.PreOpen) return Reject(RejectReason.WrongPhase);
        if (StoreEventById(answer.EventInstanceId) is not { } e) return Reject(RejectReason.UnknownStoreEvent);
        if (e.Status != StoreEventStatus.Offered) return Reject(RejectReason.OfferClosed);

        if (!answer.Accept)
        {
            Replace(e with { Status = StoreEventStatus.Declined });
            return null;
        }

        if (EventDefOf(e).Kind == EventKind.ClearanceOffer)
        {
            var product = _products[e.ProductId!];
            var units = e.Goods![0].Quantity;
            var cost = checked(units * e.UnitCostCents);
            if (!Economy.CanAfford(cost)) return Reject(RejectReason.InsufficientCash);
            if (AllocateStorage(product, units) is not { } parts) return Reject(RejectReason.StorageFull);

            Economy.Spend(ExpenseCategory.Purchases, cost);
            foreach (var (storage, n) in parts)
            {
                Inventory.GetOrCreate(StockLocation.Storage(storage)).Add(new StockLot(product.Id, n, e.BestBeforeDay, e.UnitCostCents));
            }

            _events.Add(new ClearanceBought(e.Id, product.Id, units, cost));
        }

        Replace(e with { Status = StoreEventStatus.Accepted });
        return null;
    }

    private Rejection? HandleRentGenerator(RentGenerator rent)
    {
        if (StoreEventById(rent.EventInstanceId) is not { } e || EventDefOf(e).PowerCut is not { } cut) return Reject(RejectReason.UnknownStoreEvent);
        if (e.GeneratorRented || e.Status != StoreEventStatus.Announced || e.StartDay < Clock.Day) return Reject(RejectReason.OfferClosed);
        if (!Economy.CanAfford(cut.GeneratorCostCents)) return Reject(RejectReason.InsufficientCash);

        Economy.Spend(ExpenseCategory.Events, cut.GeneratorCostCents);
        Replace(e with { GeneratorRented = true });
        return null;
    }

    /// <summary>While open: the inspector comes and goes, the power goes and comes back, group orders are collected.</summary>
    private void RunStoreEvents()
    {
        var minute = Clock.MinuteOfDay;
        foreach (var e in _storeEvents.Where(e => e.IsOn(Clock.Day)).ToList())
        {
            var def = EventDefOf(e);
            switch (def.Kind)
            {
                case EventKind.Inspection when def.Inspection is { } rules:
                    if (e.Status == StoreEventStatus.Announced && minute >= rules.ArriveMinuteOfDay)
                    {
                        Replace(e with { Status = StoreEventStatus.Started });
                        _events.Add(new InspectorArrived(e.Id));
                    }
                    else if (e.Status == StoreEventStatus.Started && minute >= rules.ArriveMinuteOfDay + rules.StayMinutes)
                    {
                        FinishInspection(e, rules);
                    }

                    break;

                case EventKind.PowerCut when def.PowerCut is { } cut:
                    if (e.Status == StoreEventStatus.Announced && minute >= cut.StartMinuteOfDay) StartPowerCut(e, cut);
                    else if (e.Status == StoreEventStatus.Started && minute >= cut.EndMinuteOfDay) EndPowerCut(e);
                    break;

                case EventKind.GroupOrder when def.GroupOrder is { } order && e.Status == StoreEventStatus.Accepted && minute >= order.PickupMinuteOfDay:
                    CollectGroupOrder(e, order);
                    break;
            }
        }
    }

    /// <summary>At closing nothing is left half done: a power cut ends, an accepted group order nobody could collect is missed.</summary>
    private void SettleStoreEventsAtClose()
    {
        foreach (var e in _storeEvents.Where(e => e.IsOn(Clock.Day)).ToList())
        {
            if (e.Status == StoreEventStatus.Started && EventDefOf(e).Kind == EventKind.PowerCut) EndPowerCut(e);
            if (e.Status == StoreEventStatus.Accepted && EventDefOf(e).Kind == EventKind.GroupOrder)
            {
                Replace(e with { Status = StoreEventStatus.Missed });
                _events.Add(new GroupOrderFinished(e.Id, false, 0));
            }
        }
    }

    /// <summary>What the inspector would find now: spoiled goods or dead animals on display, dirty tank water, messy produce stands.</summary>
    public HygieneFindings CheckHygiene(InspectionDef rules)
    {
        var spoiled = Inventory.Locations
                          .Where(p => IsDisplay(p.Location.FixtureInstanceId))
                          .SelectMany(p => p.Lots)
                          .Any(l => l.BestBeforeDay < Clock.Day || Freshness.TierOf(_products[l.ProductId], l, Clock.Day) == FreshnessTier.Spoiled)
                      || Tanks().Any(t => DeadIn(t).Count > 0);
        var dirty = Tanks().Any(t => WaterQualityOf(t) < rules.MinWaterQuality);
        var messy = Layout.Fixtures.Where(f => IsProduceStand(f.InstanceId)).Any(f => TidinessOf(f.InstanceId) < rules.MinTidiness);
        return new HygieneFindings(spoiled, dirty, messy);
    }

    private void FinishInspection(StoreEvent e, InspectionDef rules)
    {
        var findings = CheckHygiene(rules);
        var reputation = Data.Reputation is not null;
        long cash;
        if (findings.Passed)
        {
            // Spec 16.1: once there is reputation, passing earns reputation instead of the small cash reward.
            cash = reputation && rules.ReputationReward > 0 ? 0 : rules.RewardCents;
            Economy.Refund(ExpenseCategory.Events, cash);
            if (reputation && rules.ReputationReward > 0) NudgeReputation(rules.ReputationReward, "reputation.reason.inspection_passed");
        }
        else
        {
            var fine = findings.Problems * rules.FinePerProblemCents;
            Economy.Pay(ExpenseCategory.Events, fine);
            cash = -fine;
            if (reputation && rules.ReputationPerProblem > 0) NudgeReputation(-findings.Problems * rules.ReputationPerProblem, "reputation.reason.inspection_failed");
        }

        Replace(e with { Status = findings.Passed ? StoreEventStatus.Passed : StoreEventStatus.Failed });
        _events.Add(new InspectionFinished(e.Id, findings, cash));
    }

    /// <summary>Without a generator, chilled goods everywhere lose shelf life; anything now past its last day is thrown out.</summary>
    private void StartPowerCut(StoreEvent e, PowerCutDef cut)
    {
        long lost = 0;
        if (!e.GeneratorRented)
        {
            foreach (var pile in Inventory.Locations.ToList())
            {
                pile.ShortenShelfLife(l => _products[l.ProductId].Storage == StorageType.Chilled, cut.AgeDays);
                foreach (var lot in pile.RemoveExpired(Clock.Day - 1))
                {
                    var loss = checked(lot.Quantity * lot.UnitCostCents);
                    lost += loss;
                    Economy.RecordLoss(loss, _products[lot.ProductId].Department);
                    _events.Add(new ProductExpired(Clock.Day, lot.ProductId, lot.Quantity, pile.Location, loss));
                }
            }
        }

        Replace(e with { Status = StoreEventStatus.Started });
        _events.Add(new PowerCutStarted(e.Id, e.GeneratorRented, lost));
    }

    private void EndPowerCut(StoreEvent e)
    {
        Replace(e with { Status = StoreEventStatus.Ended });
        _events.Add(new PowerRestored(e.Id));
    }

    /// <summary>
    /// The regular collects the goods if all of them are in the store (taken from storage first, then the shelves) and
    /// pays the reward price; otherwise nothing is taken and the order is missed.
    /// </summary>
    private void CollectGroupOrder(StoreEvent e, GroupOrderDef order)
    {
        var goods = e.Goods ?? [];
        if (goods.Any(g => SellableUnits(g.ProductId) < g.Quantity))
        {
            Replace(e with { Status = StoreEventStatus.Missed });
            _events.Add(new GroupOrderFinished(e.Id, false, 0));
            return;
        }

        long total = 0;
        foreach (var g in goods)
        {
            var left = g.Quantity;
            long cost = 0;
            foreach (var pile in SellablePiles(g.ProductId))
            {
                var take = Math.Min(left, pile.Count(g.ProductId));
                if (take == 0) continue;

                cost += pile.Take(g.ProductId, take).Sum(l => l.Quantity * l.UnitCostCents);
                left -= take;
                if (left == 0) break;
            }

            var revenue = checked(g.Quantity * _products[g.ProductId].SuggestedPriceCents * order.RewardPermille / 1000);
            Economy.Earn(g.ProductId, g.Quantity, revenue, cost);
            total += revenue;
        }

        Replace(e with { Status = StoreEventStatus.Delivered });
        _events.Add(new GroupOrderFinished(e.Id, true, total));
    }

    private int SellableUnits(string productId) => SellablePiles(productId).Sum(p => p.Count(productId));

    /// <summary>Storage areas first, then display slots.</summary>
    private List<Stockpile> SellablePiles(string productId) =>
        Inventory.Locations
            .Where(p => p.Count(productId) > 0 && (IsStorageArea(p.Location.FixtureInstanceId) || IsDisplay(p.Location.FixtureInstanceId)))
            .OrderBy(p => IsStorageArea(p.Location.FixtureInstanceId) ? 0 : 1)
            .ThenBy(p => p.Location)
            .ToList();

    private CalendarSave CalendarSave() =>
        new(_eventRng.State, _nextStoreEventId, _storeEvents.ToList(), Decorations);

    private void RestoreCalendar(CalendarSave? save, ulong fallbackSeed)
    {
        if (save is null)
        {
            StartEventStream(fallbackSeed);
            return;
        }

        _eventRng = SimRng.FromState(save.Rng);
        _nextStoreEventId = save.NextEventId;
        _storeEvents.AddRange(save.Events);
        foreach (var d in save.Decorations) _decorations[d.DecorationId] = d.WallX;
    }
}
