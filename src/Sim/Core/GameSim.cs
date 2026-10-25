using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Staff;
using ChineseGrocery.Sim.Tasks;
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
    private readonly List<Worker> _workers = [];
    private readonly HashSet<GridPoint> _occupied = [];

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

        RebuildOccupancy();
        StockTheOldStore();
        var player = data.Employees.Player;
        Player = new Worker(1, isPlayer: true, PlayerStartCell(), player.MoveCellsPerMinute, player.WorkSpeedPermille, player.KnifeSkill);
        _workers.Add(Player);

        _events.Add(new DayStarted(Clock.Day));
        StartStory(seed);
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
    public TaskBoard Tasks { get; } = new();
    public Worker Player { get; }
    public IReadOnlyList<Worker> Workers => _workers;
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
        var storyMark = _events.Count;

        var commands = _queue.ToList();
        _queue.Clear();
        foreach (var command in commands)
        {
            Execute(command);
        }

        SpawnArrivals();
        UpdateCustomers();
        RefreshTasks();
        UpdateWorkers();

        if (Clock.Phase == DayPhase.Open)
        {
            FoulTankWater();
            Clock.Advance(Data.Economy.SecondsPerTick);

            // Doors shut at closing time, but the day only ends once the last customer is out.
            if (Clock.MinuteOfDay >= Data.Economy.CloseMinuteOfDay && _customers.Count == 0)
            {
                CloseDay();
            }
        }

        UpdateStory(storyMark);
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
            MovePlayer move => HandleMovePlayer(move),
            PlayerInteract interact => HandlePlayerInteract(interact),
            UpgradeStage => HandleUpgradeStage(),
            HireEmployee hire => HandleHireEmployee(hire),
            Deli.SetMenuItem menu => HandleSetMenuItem(menu),
            SetWage wage => HandleSetWage(wage),
            RestockAll => HandleRestockAll(),
            Story.StoryCommand story => HandleStoryCommand(story),
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
        _cookedToday.Clear();
        _events.Add(new DayStarted(Clock.Day));
        DeliverOrders();
        return null;
    }

    private void CloseDay()
    {
        Clock.Close();
        _events.Add(new StoreClosed(Clock.Day));
        KillTankAnimals();
        KillStrandedLiveSeafood();

        foreach (var pile in Inventory.Locations)
        {
            foreach (var lot in pile.RemoveExpired(Clock.Day))
            {
                var loss = checked(lot.Quantity * lot.UnitCostCents);
                Economy.RecordLoss(loss, _products[lot.ProductId].Department);
                _events.Add(new ProductExpired(Clock.Day, lot.ProductId, lot.Quantity, pile.Location, loss));
            }
        }

        PayWages();
        CollectLoanPayment();

        foreach (var worker in _workers)
        {
            foreach (var lot in worker.RemoveExpired(Clock.Day))
            {
                var loss = checked(lot.Quantity * lot.UnitCostCents);
                Economy.RecordLoss(loss, _products[lot.ProductId].Department);
                _events.Add(new ProductExpired(Clock.Day, lot.ProductId, lot.Quantity, null, loss));
            }

            if (worker.Step == WorkStep.Carrying && worker.Carrying.Count == 0) FinishTask(worker, completed: false);
        }

        var report = Economy.CloseDay();

        // The day's books close now: anything spent after closing (orders for tomorrow) goes on the next day's report.
        Economy.BeginDay(Clock.Day + 1);
        _reports.Add(report);
        _events.Add(new DailyReportReady(report));
    }

    /// <summary>New game only: the parents' shelves start assigned and full, with the rest of the opening stock in storage.</summary>
    private void StockTheOldStore()
    {
        var entries = Data.Economy.StartingLayout;
        for (var i = 0; i < entries.Count; i++)
        {
            var slots = entries[i].Slots ?? [];
            for (var slot = 0; slot < slots.Count; slot++)
            {
                if (slots[slot] is { } productId) Shelves.Set(new StockLocation(Layout.Fixtures[i].InstanceId, slot), productId);
            }
        }

        foreach (var entry in Data.Economy.StartingStock ?? [])
        {
            var product = _products[entry.ProductId];
            var storage = StorageRouting.FindDestination(Data, Layout.Fixtures, product)!;
            Inventory.GetOrCreate(StockLocation.Storage(storage.InstanceId))
                .Add(new StockLot(product.Id, entry.Quantity, Clock.Day + product.ShelfLifeDays - 1, product.CostCents));
        }

        FillShelvesFromStorage(announce: false);
    }

    private bool IsProduce(string productId) => _products[productId].Department == Department.Produce;

    /// <summary>
    /// What one unit of this batch sells for today: the set price, raised by a skilled cook's bonus and marked down by
    /// the batch's freshness tier.
    /// </summary>
    public long EffectivePrice(StockLot lot)
    {
        var product = _products[lot.ProductId];
        var price = Prices.PriceOf(lot.ProductId) * (1000 + lot.PriceBonusPermille) / 1000;
        if (product.FreshnessDays is null || Data.Economy.Freshness is not { } f) return price;

        return price * f.PriceFactorsPermille.Of(Produce.Freshness.TierOf(product, lot, Clock.Day)) / 1000;
    }

    private bool IsWalkable(GridPoint p) => LayoutRules.InBounds(Layout.Size, p) && !_occupied.Contains(p);

    /// <summary>Recomputes the cells covered by fixtures; call after any layout change.</summary>
    private void RebuildOccupancy()
    {
        _occupied.Clear();
        foreach (var f in Layout.Fixtures)
        {
            _occupied.UnionWith(LayoutRules.Footprint(_fixtures[f.FixtureId], f.Origin, f.Rotation));
        }
    }

    private GridPoint PlayerStartCell()
    {
        var entrance = Layout.Fixtures.First(f => _fixtures[f.FixtureId].Kind == FixtureKind.Entrance);
        return LayoutRules.CustomerCells(_fixtures[entrance.FixtureId], entrance.Origin, entrance.Rotation).First();
    }
}
