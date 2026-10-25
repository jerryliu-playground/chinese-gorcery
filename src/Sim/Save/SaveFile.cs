using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Tasks;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Save;

/// <summary>
/// Everything that changes during play. Static data (/data) is not copied in; a save is loaded against the current data.
/// Pending events are not saved: they are notifications, not state.
/// </summary>
public sealed record SaveFile(
    int Version,
    long TickCount,
    Core.RngState Rng,
    int Day,
    DayPhase Phase,
    int SecondOfDay,
    long CashCents,
    long CumulativeRevenueCents,
    int Stage,
    LedgerSave Today,
    IReadOnlyList<DailyReport> Reports,
    GridSize StoreSize,
    IReadOnlyList<PlacedFixture> Fixtures,
    int NextFixtureInstanceId,
    IReadOnlyList<StockSave> Stock,
    IReadOnlyList<SlotSave> ShelfSlots,
    IReadOnlyDictionary<string, long> Prices,
    IReadOnlyList<PendingOrder> PendingOrders,
    IReadOnlyList<SimTask> Tasks,
    IReadOnlyList<ClaimSave> Claims,
    int NextTaskId,
    IReadOnlyList<WorkerSave> Workers,
    IReadOnlyList<CustomerSave> Customers,
    int NextCustomerId,
    long NextQueueNumber,
    IReadOnlyList<SimCommand> QueuedCommands,
    IReadOnlyList<TidinessSave>? Tidiness = null,
    IReadOnlyList<TankSave>? Tanks = null,
    IReadOnlyList<MenuSave>? Menu = null);
