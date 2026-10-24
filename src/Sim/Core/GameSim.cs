using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Core;

/// <summary>
/// The whole simulation. The view layer reads state, enqueues commands and calls <see cref="Tick"/>;
/// queued commands run at the start of the next tick, then time advances if the store is open.
/// </summary>
public sealed class GameSim
{
    private readonly List<SimCommand> _queue = [];
    private readonly List<SimEvent> _events = [];
    private readonly List<DailyReport> _reports = [];
    private readonly Dictionary<string, ProductDef> _products;
    private readonly Dictionary<string, FixtureDef> _fixtures;
    private readonly OrderBook _orders = new();

    public GameSim(GameData data, ulong seed)
    {
        Data = data;
        Rng = new SimRng(seed);
        _products = data.Products.ToDictionary(p => p.Id, StringComparer.Ordinal);
        _fixtures = data.Fixtures.ToDictionary(f => f.Id, StringComparer.Ordinal);
        Clock = new GameClock(data.Economy.OpenMinuteOfDay);
        Economy = new EconomyState(data.Economy.StartingCashCents);
        Layout = new StoreLayout(data.Stages[0].StoreSize);
        foreach (var entry in data.Economy.StartingLayout)
        {
            Layout.Add(entry.FixtureId, entry.Origin, entry.Rotation, paidCents: 0);
        }

        _events.Add(new DayStarted(Clock.Day));
    }

    public GameData Data { get; }
    public SimRng Rng { get; }
    public long TickCount { get; private set; }
    public GameClock Clock { get; }
    public EconomyState Economy { get; }
    public StoreLayout Layout { get; }
    public InventoryState Inventory { get; } = new();
    public IReadOnlyList<PendingOrder> PendingOrders => _orders.Pending;
    public IReadOnlyList<DailyReport> Reports => _reports;

    public void Enqueue(SimCommand command) => _queue.Add(command);

    /// <summary>Returns and clears the events produced since the last call.</summary>
    public IReadOnlyList<SimEvent> DrainEvents()
    {
        var events = _events.ToList();
        _events.Clear();
        return events;
    }

    public void Tick()
    {
        TickCount++;

        var commands = _queue.ToList();
        _queue.Clear();
        foreach (var command in commands)
        {
            Execute(command);
        }

        if (Clock.Phase == DayPhase.Open)
        {
            Clock.Advance(Data.Economy.MinutesPerTick);
            if (Clock.MinuteOfDay >= Data.Economy.CloseMinuteOfDay)
            {
                CloseDay();
            }
        }
    }

    private void Execute(SimCommand command)
    {
        Rejection? rejection = command switch
        {
            OpenStore => HandleOpenStore(),
            StartNextDay => HandleStartNextDay(),
            PlaceOrder order => HandlePlaceOrder(order),
            PlaceFixture place => HandlePlaceFixture(place),
            MoveFixture move => HandleMoveFixture(move),
            SellFixture sell => HandleSellFixture(sell),
            _ => throw new ArgumentException($"Unknown command type {command.GetType().Name}", nameof(command)),
        };

        if (rejection is { } problem)
        {
            _events.Add(new CommandRejected(command, problem.Reason, problem.InstanceId == 0 ? null : problem.InstanceId));
        }
    }

    private static Rejection Reject(RejectReason reason) => new(reason);

    private Rejection? HandleOpenStore()
    {
        if (Clock.Phase != DayPhase.PreOpen) return Reject(RejectReason.WrongPhase);

        Clock.Open();
        _events.Add(new StoreOpened(Clock.Day));
        return null;
    }

    private Rejection? HandleStartNextDay()
    {
        if (Clock.Phase != DayPhase.Closed) return Reject(RejectReason.WrongPhase);

        Clock.StartNextDay(Data.Economy.OpenMinuteOfDay);
        Economy.BeginDay(Clock.Day);
        _events.Add(new DayStarted(Clock.Day));
        DeliverOrders();
        return null;
    }

    private Rejection? HandlePlaceOrder(PlaceOrder order)
    {
        if (Clock.Phase != DayPhase.PreOpen) return Reject(RejectReason.WrongPhase);
        if (order.Quantity <= 0) return Reject(RejectReason.InvalidQuantity);
        if (!_products.TryGetValue(order.ProductId, out var product)) return Reject(RejectReason.UnknownProduct);
        if (!product.Orderable) return Reject(RejectReason.NotOrderable);
        if (StorageRouting.FindDestination(Data, Layout.Fixtures, product) is not { } destination) return Reject(RejectReason.NoStorageForProduct);

        var cost = checked(order.Quantity * product.CostCents);
        if (!Economy.CanAfford(cost)) return Reject(RejectReason.InsufficientCash);
        if (!FitsInStorage(destination, product, order.Quantity)) return Reject(RejectReason.StorageFull);

        Economy.Spend(ExpenseCategory.Purchases, cost);
        _orders.Add(new PendingOrder(product.Id, order.Quantity, product.CostCents, Clock.Day));
        _events.Add(new OrderPlaced(Clock.Day, product.Id, order.Quantity, cost));
        return null;
    }

    /// <summary>Whether the destination can hold what it has, what is already on order for it, and this order.</summary>
    private bool FitsInStorage(PlacedFixture destination, ProductDef product, int quantity)
    {
        var held = Inventory.TryGet(destination.InstanceId)?.Lots.Select(l => (_products[l.ProductId], l.Quantity)) ?? [];
        var pending = _orders.Pending
            .Where(o => StorageRouting.FindDestination(Data, Layout.Fixtures, _products[o.ProductId])?.InstanceId == destination.InstanceId)
            .Select(o => (_products[o.ProductId], o.Quantity));
        var slots = StorageCapacity.SlotsNeeded(held.Concat(pending).Append((product, quantity)));
        return slots <= _fixtures[destination.FixtureId].Capacity;
    }

    private Rejection? HandlePlaceFixture(PlaceFixture place)
    {
        if (Clock.Phase != DayPhase.PreOpen) return Reject(RejectReason.WrongPhase);
        if (!_fixtures.TryGetValue(place.FixtureId, out var def)) return Reject(RejectReason.UnknownFixture);
        if (def.UnlockStage > Economy.Stage) return Reject(RejectReason.FixtureLocked);

        var candidate = new PlacedFixture(Layout.NextInstanceId, def.Id, place.Origin, def.PriceCents, place.Rotation);
        if (CheckLayout([.. Layout.Fixtures, candidate], candidate.InstanceId) is { } problem) return problem;
        if (!Economy.CanAfford(def.PriceCents)) return Reject(RejectReason.InsufficientCash);

        Economy.Spend(ExpenseCategory.Fixtures, def.PriceCents);
        var placed = Layout.Add(def.Id, place.Origin, place.Rotation, def.PriceCents);
        _events.Add(new FixturePlaced(placed.InstanceId, def.Id, placed.Origin, placed.Rotation, def.PriceCents));
        return null;
    }

    private Rejection? HandleMoveFixture(MoveFixture move)
    {
        if (Clock.Phase != DayPhase.PreOpen) return Reject(RejectReason.WrongPhase);
        if (Layout.Find(move.InstanceId) is not { } current) return Reject(RejectReason.UnknownFixtureInstance);

        var moved = Layout.Fixtures.Select(f => f.InstanceId == move.InstanceId ? f with { Origin = move.Origin, Rotation = move.Rotation } : f).ToList();
        if (CheckLayout(moved, move.InstanceId) is { } problem) return problem;

        Layout.Move(move.InstanceId, move.Origin, move.Rotation);
        _events.Add(new FixtureMoved(move.InstanceId, current.Origin, move.Origin, move.Rotation));
        return null;
    }

    private Rejection? HandleSellFixture(SellFixture sell)
    {
        if (Clock.Phase != DayPhase.PreOpen) return Reject(RejectReason.WrongPhase);
        if (Layout.Find(sell.InstanceId) is not { } target) return Reject(RejectReason.UnknownFixtureInstance);

        var remaining = Layout.Fixtures.Where(f => f.InstanceId != sell.InstanceId).ToList();
        if (_fixtures[target.FixtureId].Kind == FixtureKind.Entrance && !remaining.Any(f => _fixtures[f.FixtureId].Kind == FixtureKind.Entrance))
        {
            return Reject(RejectReason.LastEntrance);
        }

        if (Inventory.Holds(sell.InstanceId)) return Reject(RejectReason.FixtureNotEmpty);
        if (_orders.Pending.Any(o => StorageRouting.FindDestination(Data, remaining, _products[o.ProductId]) is null))
        {
            return Reject(RejectReason.StorageNeededForOrders);
        }

        if (CheckLayout(remaining, focusId: 0) is { } problem) return problem;

        var refund = target.PaidCents * Data.Economy.FixtureSellRefundPermille / 1000;
        Layout.Remove(sell.InstanceId);
        Inventory.Forget(sell.InstanceId);
        Economy.Refund(ExpenseCategory.Fixtures, refund);
        _events.Add(new FixtureSold(sell.InstanceId, target.FixtureId, refund));
        return null;
    }

    private Rejection? CheckLayout(IReadOnlyList<PlacedFixture> fixtures, int focusId) =>
        LayoutRules.Check(Layout.Size, fixtures, id => _fixtures[id], focusId);

    private void DeliverOrders()
    {
        foreach (var order in _orders.TakeAll())
        {
            var product = _products[order.ProductId];
            var destination = StorageRouting.FindDestination(Data, Layout.Fixtures, product)
                ?? throw new InvalidOperationException($"No storage for delivered '{order.ProductId}'; selling should have been blocked.");
            var bestBefore = Clock.Day + product.ShelfLifeDays - 1;
            Inventory.GetOrCreate(destination.InstanceId).Add(new StockLot(order.ProductId, order.Quantity, bestBefore, order.UnitCostCents));
            _events.Add(new OrderDelivered(Clock.Day, order.ProductId, order.Quantity, destination.InstanceId));
        }
    }

    private void CloseDay()
    {
        Clock.Close();
        _events.Add(new StoreClosed(Clock.Day));

        foreach (var pile in Inventory.Locations)
        {
            foreach (var lot in pile.RemoveExpired(Clock.Day))
            {
                var loss = checked(lot.Quantity * lot.UnitCostCents);
                Economy.RecordLoss(loss);
                _events.Add(new ProductExpired(Clock.Day, lot.ProductId, lot.Quantity, pile.FixtureInstanceId, loss));
            }
        }

        var report = Economy.CloseDay();
        _reports.Add(report);
        _events.Add(new DailyReportReady(report));
    }
}
