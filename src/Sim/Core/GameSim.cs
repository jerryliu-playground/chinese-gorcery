using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Economy;
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
    private readonly OrderBook _orders = new();

    public GameSim(GameData data, ulong seed)
    {
        Data = data;
        Rng = new SimRng(seed);
        _products = data.Products.ToDictionary(p => p.Id, StringComparer.Ordinal);
        Clock = new GameClock(data.Economy.OpenMinuteOfDay);
        Economy = new EconomyState(data.Economy.StartingCashCents);
        _events.Add(new DayStarted(Clock.Day));
    }

    public GameData Data { get; }
    public SimRng Rng { get; }
    public long TickCount { get; private set; }
    public GameClock Clock { get; }
    public EconomyState Economy { get; }
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
        var rejection = command switch
        {
            OpenStore => HandleOpenStore(),
            StartNextDay => HandleStartNextDay(),
            PlaceOrder order => HandlePlaceOrder(order),
            _ => throw new ArgumentException($"Unknown command type {command.GetType().Name}", nameof(command)),
        };

        if (rejection is { } reason)
        {
            _events.Add(new CommandRejected(command, reason));
        }
    }

    private RejectReason? HandleOpenStore()
    {
        if (Clock.Phase != DayPhase.PreOpen) return RejectReason.WrongPhase;

        Clock.Open();
        _events.Add(new StoreOpened(Clock.Day));
        return null;
    }

    private RejectReason? HandleStartNextDay()
    {
        if (Clock.Phase != DayPhase.Closed) return RejectReason.WrongPhase;

        Clock.StartNextDay(Data.Economy.OpenMinuteOfDay);
        Economy.BeginDay(Clock.Day);
        _events.Add(new DayStarted(Clock.Day));
        DeliverOrders();
        return null;
    }

    private RejectReason? HandlePlaceOrder(PlaceOrder order)
    {
        if (Clock.Phase != DayPhase.PreOpen) return RejectReason.WrongPhase;
        if (order.Quantity <= 0) return RejectReason.InvalidQuantity;
        if (!_products.TryGetValue(order.ProductId, out var product)) return RejectReason.UnknownProduct;
        if (!product.Orderable) return RejectReason.NotOrderable;
        if (StorageRouting.FindDestination(Data, product, Economy.Stage) is not { } destination) return RejectReason.NoStorageForProduct;

        var cost = checked(order.Quantity * product.CostCents);
        if (!Economy.CanAfford(cost)) return RejectReason.InsufficientCash;

        Economy.Spend(ExpenseCategory.Purchases, cost);
        _orders.Add(new PendingOrder(product.Id, order.Quantity, product.CostCents, Clock.Day, destination.Id));
        _events.Add(new OrderPlaced(Clock.Day, product.Id, order.Quantity, cost));
        return null;
    }

    private void DeliverOrders()
    {
        foreach (var order in _orders.TakeAll())
        {
            var bestBefore = Clock.Day + _products[order.ProductId].ShelfLifeDays - 1;
            Inventory.GetOrCreate(order.DestinationId).Add(new StockLot(order.ProductId, order.Quantity, bestBefore, order.UnitCostCents));
            _events.Add(new OrderDelivered(Clock.Day, order.ProductId, order.Quantity, order.DestinationId));
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
                _events.Add(new ProductExpired(Clock.Day, lot.ProductId, lot.Quantity, pile.Id, loss));
            }
        }

        var report = Economy.CloseDay();
        _reports.Add(report);
        _events.Add(new DailyReportReady(report));
    }
}
