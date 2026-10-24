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
public sealed partial class GameSim
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
        Prices = new PriceBook(data.Products);
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
    public ShelfAssignments Shelves { get; } = new();
    public PriceBook Prices { get; }
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
            AssignShelfSlot assign => HandleAssignShelfSlot(assign),
            SetPrice price => HandleSetPrice(price),
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
                _events.Add(new ProductExpired(Clock.Day, lot.ProductId, lot.Quantity, pile.Location, loss));
            }
        }

        var report = Economy.CloseDay();
        _reports.Add(report);
        _events.Add(new DailyReportReady(report));
    }
}
