using ChineseGrocery.Sim.Customers;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Save;
using ChineseGrocery.Sim.Tasks;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Core;

/// <summary>Capturing and restoring the complete simulation state.</summary>
public sealed partial class GameSim
{
    internal SaveFile ToSave()
    {
        var today = Economy.Today;
        return new SaveFile(
            SaveGame.CurrentVersion,
            TickCount,
            Rng.State,
            Clock.Day,
            Clock.Phase,
            Clock.SecondOfDay,
            Economy.CashCents,
            Economy.CumulativeRevenueCents,
            Economy.Stage,
            new LedgerSave(
                today.Day, today.CashStartCents, today.RevenueCents, today.CostOfGoodsSoldCents, today.PurchaseCostCents,
                today.WagesCents, today.FixtureSpendCents, today.LossCents, today.Customers, today.AngryLeaves,
                today.ItemsNotFound, today.ItemsTooExpensive, today.Sales, today.ProduceLossCents, today.SeafoodLossCents, today.SeafoodDeaths, today.MeatLossCents, today.DeliLossCents,
                today.KitchenRescuedCents, today.LoanPaymentCents),
            _reports.ToList(),
            Layout.Size,
            Layout.Fixtures.ToList(),
            Layout.NextInstanceId,
            Inventory.Locations.Where(p => p.Lots.Count > 0).Select(p => new StockSave(p.Location, p.Lots.ToList())).ToList(),
            Shelves.Assigned.Select(kv => new SlotSave(kv.Key, kv.Value)).ToList(),
            new SortedDictionary<string, long>(Prices.Overrides.ToDictionary(kv => kv.Key, kv => kv.Value), StringComparer.Ordinal),
            _orders.Pending.ToList(),
            Tasks.Tasks.ToList(),
            Tasks.Claims.OrderBy(kv => kv.Key).Select(kv => new ClaimSave(kv.Key, kv.Value)).ToList(),
            Tasks.NextId,
            _workers.Select(w => new WorkerSave(
                w.Id, w.IsPlayer, w.RoleId, w.Position, w.Path.ToList(), w.MoveCellsPerMinute, w.MoveProgress, w.WorkSpeedPermille,
                w.KnifeSkill, w.WageCentsPerDay, w.MoralePermille, w.Level, w.Xp, w.TaskId, w.Step, w.WorkTicksLeft,
                w.ServingCustomerId, w.Carrying.ToList(), w.IsPresent, w.Shift, w.RestDays.ToList(), w.AllowedTasks?.ToList(),
                w.ManagedDepartment, w.MoraleAdjustPermille, w.WorkStreakDays, w.TasksToday, w.QuitWarning)).ToList(),
            _customers.Select(c => new CustomerSave(
                c.Id, c.TypeId, c.Position, c.Path.ToList(), c.MoveProgress, c.State, c.ShoppingList.ToList(), c.Basket.ToList(),
                c.Target?.Slot, c.Target?.ProductId, c.BrowseTicksLeft, c.CheckoutInstanceId, c.QueueNumber, c.WaitedSeconds,
                c.Exit, c.ItemsNotFound, c.ItemsTooExpensive, c.WaitingFor,
                c.ItemsBought, c.Hearts, c.SeekingTrend, c.TrendFound, c.Confused, c.LongestWaitSeconds, c.Satisfaction)).ToList(),
            _nextCustomerId,
            _nextQueueNumber,
            _queue.ToList(),
            _tidiness.Select(kv => new TidinessSave(kv.Key, kv.Value)).ToList(),
            TankSaves(),
            _menu.Select(kv => new MenuSave(kv.Key, kv.Value, CookedToday(kv.Key))).ToList(),
            StaffSave(),
            ImportSave(),
            SaveStory(),
            ReputationSave());
    }

    internal static GameSim FromSave(GameData data, SaveFile save) => new(data, save);

    /// <summary>Rebuilds a game from a save; the starting layout and new-game setup are skipped.</summary>
    private GameSim(GameData data, SaveFile save)
    {
        Data = data;
        Rng = SimRng.FromState(save.Rng);
        _products = data.Products.ToDictionary(p => p.Id, StringComparer.Ordinal);
        _fixtures = data.Fixtures.ToDictionary(f => f.Id, StringComparer.Ordinal);
        TickCount = save.TickCount;

        Clock = new GameClock(data.Economy.OpenMinuteOfDay) { Day = save.Day, Phase = save.Phase, SecondOfDay = save.SecondOfDay };

        var t = save.Today;
        var ledger = new DayLedger(t.Day, t.CashStartCents)
        {
            RevenueCents = t.RevenueCents, CostOfGoodsSoldCents = t.CostOfGoodsSoldCents, PurchaseCostCents = t.PurchaseCostCents,
            WagesCents = t.WagesCents, FixtureSpendCents = t.FixtureSpendCents, LossCents = t.LossCents, Customers = t.Customers,
            AngryLeaves = t.AngryLeaves, ItemsNotFound = t.ItemsNotFound, ItemsTooExpensive = t.ItemsTooExpensive,
            ProduceLossCents = t.ProduceLossCents, SeafoodLossCents = t.SeafoodLossCents, SeafoodDeaths = t.SeafoodDeaths,
            MeatLossCents = t.MeatLossCents, DeliLossCents = t.DeliLossCents, KitchenRescuedCents = t.KitchenRescuedCents,
            LoanPaymentCents = t.LoanPaymentCents,
        };
        ledger.RestoreSales(t.Sales);
        Economy = new EconomyState(save.CashCents)
        {
            CashCents = save.CashCents, CumulativeRevenueCents = save.CumulativeRevenueCents, Stage = save.Stage, Today = ledger,
        };
        _reports.AddRange(save.Reports);

        Prices = new PriceBook(data.Products);
        foreach (var (productId, price) in save.Prices) Prices.Set(productId, price);

        Layout = new StoreLayout(save.StoreSize);
        Layout.Restore(save.Fixtures, save.NextFixtureInstanceId);
        foreach (var f in save.Fixtures) _ = _fixtures[f.FixtureId];
        RebuildOccupancy();

        foreach (var pile in save.Stock)
        {
            var stockpile = Inventory.GetOrCreate(pile.Location);
            foreach (var lot in pile.Lots) stockpile.Add(lot);
        }

        foreach (var slot in save.ShelfSlots) Shelves.Set(slot.Slot, slot.ProductId);
        foreach (var order in save.PendingOrders) _orders.Add(order);
        Tasks.Restore(save.Tasks, save.Claims.Select(c => KeyValuePair.Create(c.TaskId, c.WorkerId)), save.NextTaskId);

        foreach (var w in save.Workers)
        {
            var worker = new Worker(w.Id, w.IsPlayer, w.Position, w.MoveCellsPerMinute, w.WorkSpeedPermille, w.KnifeSkill)
            {
                RoleId = w.RoleId, MoveProgress = w.MoveProgress, WageCentsPerDay = w.WageCentsPerDay, MoralePermille = w.MoralePermille,
                Level = w.Level, Xp = w.Xp, TaskId = w.TaskId, Step = w.Step, WorkTicksLeft = w.WorkTicksLeft,
                ServingCustomerId = w.ServingCustomerId, IsPresent = w.IsPresent, Shift = w.Shift, RestDays = w.RestDays ?? [],
                AllowedTasks = w.AllowedTasks, ManagedDepartment = w.ManagedDepartment, MoraleAdjustPermille = w.MoraleAdjustPermille,
                WorkStreakDays = w.WorkStreakDays, TasksToday = w.TasksToday, QuitWarning = w.QuitWarning,
            };
            worker.SetPath(w.Path);
            worker.Carry(w.Carrying);
            _workers.Add(worker);
        }

        Player = _workers.Single(w => w.IsPlayer);

        foreach (var c in save.Customers)
        {
            _ = Data.CustomerTypes.Single(t => t.Id == c.TypeId);
            var customer = new Customer(c.Id, c.TypeId, c.Position, c.ShoppingList)
            {
                MoveProgress = c.MoveProgress, State = c.State, BrowseTicksLeft = c.BrowseTicksLeft,
                CheckoutInstanceId = c.CheckoutInstanceId, QueueNumber = c.QueueNumber, WaitedSeconds = c.WaitedSeconds,
                Exit = c.Exit, ItemsNotFound = c.ItemsNotFound, ItemsTooExpensive = c.ItemsTooExpensive, WaitingFor = c.WaitingFor,
                Target = c.TargetSlot is { } slot ? (slot, c.TargetProductId!) : null,
                ItemsBought = c.ItemsBought, Hearts = c.Hearts, SeekingTrend = c.SeekingTrend, TrendFound = c.TrendFound,
                Confused = c.Confused, LongestWaitSeconds = c.LongestWaitSeconds, Satisfaction = c.Satisfaction,
            };
            customer.SetPath(c.Path);
            foreach (var item in c.Basket) customer.AddToBasket(item);
            _customers.Add(customer);
        }

        _nextCustomerId = save.NextCustomerId;
        _nextQueueNumber = save.NextQueueNumber;
        _queue.AddRange(save.QueuedCommands);
        foreach (var stand in save.Tidiness ?? []) _tidiness[stand.StandInstanceId] = stand.Tidiness;
        RestoreTanks(save.Tanks ?? []);
        RestoreStaff(save.Staff, (ulong)save.TickCount);
        RestoreImports(save.Imports, (ulong)save.TickCount);
        RestoreReputation(save.Reputation, (ulong)save.TickCount);
        RebuildOccupancy();
        foreach (var m in save.Menu ?? [])
        {
            _menu[m.Slot] = m.Item;
            if (m.CookedToday > 0) _cookedToday[m.Slot] = m.CookedToday;
        }

        RestoreStory(save.Story, save.Rng);
    }
}
