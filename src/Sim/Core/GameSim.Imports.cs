using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Imports;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Save;
using ChineseGrocery.Sim.Tasks;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Core;

/// <summary>
/// Importing by sea: cheaper than the local wholesaler but paid up front, a minimum order, and a week or two at sea
/// (own seeded stream). Shipments arrive on pallets that must be unloaded into storage before their goods can be
/// shelved; a pallet with no storage room for it is left by the door, blocking that cell. Storage areas can buy more room.
/// </summary>
public sealed partial class GameSim
{
    private const ulong ImportStreamSalt = 0x1A9D_5EA5_0B0A_7321;

    private readonly List<Shipment> _shipments = [];
    private readonly List<Pallet> _pallets = [];
    private readonly SortedDictionary<int, int> _storageBonus = new();
    private SimRng _importRng = null!;
    private int _nextShipmentId = 1;
    private int _nextPalletId = 1;

    public IReadOnlyList<Shipment> Shipments => _shipments;
    public IReadOnlyList<Pallet> Pallets => _pallets;

    /// <summary>Slots a storage area holds, with any room bought on top of the fixture's own.</summary>
    public int StorageCapacityOf(int instanceId) =>
        (Layout.Find(instanceId) is { } f ? _fixtures[f.FixtureId].Capacity : 0) + _storageBonus.GetValueOrDefault(instanceId);

    public bool IsFloorFree(GridPoint cell) => IsWalkable(cell);

    private ImportSettingsDef ImportSettings => Data.Economy.Imports ?? throw new InvalidOperationException("Imports need economy.imports settings.");

    private void StartImportStream(ulong seed) => _importRng = new SimRng(seed ^ ImportStreamSalt);

    private Rejection? HandlePlaceImportOrder(PlaceImportOrder order)
    {
        if (Clock.Phase == DayPhase.Open) return Reject(RejectReason.WrongPhase);
        if (!_products.TryGetValue(order.ProductId, out var product)) return Reject(RejectReason.UnknownProduct);
        if (product.Import is not { } import) return Reject(RejectReason.NotImportable);
        if (Data.Economy.Imports is not { } settings || Economy.Stage < settings.UnlockStage) return Reject(RejectReason.FixtureLocked);
        if (order.Quantity < import.MinUnits) return Reject(RejectReason.BelowMinimumOrder);

        var cost = checked(order.Quantity * import.CostCents);
        if (!Economy.CanAfford(cost)) return Reject(RejectReason.InsufficientCash);

        Economy.Spend(ExpenseCategory.Purchases, cost);
        var arrival = Clock.Day + _importRng.NextInt(settings.MinDays, settings.MaxDays + 1);
        var shipment = new Shipment(_nextShipmentId++, product.Id, order.Quantity, import.CostCents, Clock.Day, arrival);
        _shipments.Add(shipment);
        _events.Add(new ShipmentOrdered(shipment.Id, product.Id, order.Quantity, arrival));
        return null;
    }

    /// <summary>Holds up every shipment at sea (the delay event).</summary>
    public void DelayShipments(int days)
    {
        for (var i = 0; i < _shipments.Count; i++) _shipments[i] = _shipments[i] with { ArrivalDay = _shipments[i].ArrivalDay + days };
        _events.Add(new ShipmentsDelayed(days));
    }

    private Rejection? HandleUpgradeStorage(UpgradeStorage upgrade)
    {
        if (Layout.Find(upgrade.InstanceId) is not { } f) return Reject(RejectReason.UnknownFixtureInstance);
        if (!DataValidator.IsStorageArea(_fixtures[f.FixtureId].Kind)) return Reject(RejectReason.NotAStorageArea);
        if (!Economy.CanAfford(ImportSettings.StorageUpgradeCostCents)) return Reject(RejectReason.InsufficientCash);

        Economy.Spend(ExpenseCategory.Fixtures, ImportSettings.StorageUpgradeCostCents);
        _storageBonus[f.InstanceId] = _storageBonus.GetValueOrDefault(f.InstanceId) + ImportSettings.StorageUpgradeSlots;
        return null;
    }

    /// <summary>Shipments due today come in on pallets: into a storage area with room for the whole lot, else by the door.</summary>
    private void DeliverShipments()
    {
        foreach (var shipment in _shipments.Where(s => s.ArrivalDay <= Clock.Day).ToList())
        {
            _shipments.Remove(shipment);
            var product = _products[shipment.ProductId];
            var lot = new StockLot(product.Id, shipment.Quantity, Clock.Day + product.ShelfLifeDays - 1, shipment.UnitCostCents);
            var storage = Layout.Fixtures.FirstOrDefault(f => CanStore(f, product) && FreeUnits(f, product) >= shipment.Quantity);
            var cell = storage is null ? FreeCellByTheDoor() : null;
            var pallet = new Pallet(_nextPalletId++, [lot], storage?.InstanceId ?? (cell is null ? Layout.Fixtures.FirstOrDefault(f => CanStore(f, product))?.InstanceId : null), cell);
            _pallets.Add(pallet);
            if (cell is not null) RebuildOccupancy();
            _events.Add(new PalletArrived(pallet.Id, product.Id, shipment.Quantity, cell is not null));
        }
    }

    /// <summary>
    /// The free floor cell nearest the entrance that no fixture is used from and that, once blocked, still leaves every
    /// fixture reachable; null if there is none.
    /// </summary>
    private GridPoint? FreeCellByTheDoor()
    {
        var useCells = Layout.Fixtures
            .SelectMany(f => LayoutRules.InteractionCells(_fixtures[f.FixtureId], f.Origin, f.Rotation).Concat(LayoutRules.CustomerCells(_fixtures[f.FixtureId], f.Origin, f.Rotation)))
            .ToHashSet();
        var start = EntranceCells();
        var distances = GridPaths.Distances(IsWalkable, start[0]);
        foreach (var (cell, _) in distances.OrderBy(d => d.Value).ThenBy(d => d.Key.Y).ThenBy(d => d.Key.X))
        {
            if (useCells.Contains(cell) || _workers.Any(w => w.Position == cell) || _customers.Any(c => c.Position == cell)) continue;

            bool Walkable(GridPoint p) => p != cell && IsWalkable(p);
            var reachable = GridPaths.Reachable(Walkable, start);
            if (useCells.All(reachable.Contains)) return cell;
        }

        return null;
    }

    /// <summary>One Unload task per pallet that a storage area (its own, or any for a pallet by the door) has room for.</summary>
    private void RefreshUnloadTasks()
    {
        Tasks.RemoveOpen(t => t is UnloadTask u && _pallets.All(p => p.Id != u.PalletId));

        foreach (var pallet in _pallets)
        {
            if (Tasks.Tasks.Any(t => t is UnloadTask u && u.PalletId == pallet.Id)) continue;

            var product = _products[pallet.Lots[0].ProductId];
            var units = pallet.Lots.Sum(l => l.Quantity);
            var storage = pallet.FloorCell is null && pallet.StorageInstanceId is { } own && Layout.Find(own) is { } placed
                ? placed
                : Layout.Fixtures.FirstOrDefault(f => CanStore(f, product) && FreeUnits(f, product) >= units);
            if (storage is null) continue;

            Tasks.Add(id => new UnloadTask(id, PriorityOf(TaskType.Unload), pallet.Id, storage.InstanceId));
        }
    }

    private void UnloadPallet(Worker worker, UnloadTask task)
    {
        if (_pallets.Find(p => p.Id == task.PalletId) is not { } pallet) return;

        var pile = Inventory.GetOrCreate(StockLocation.Storage(task.StorageInstanceId));
        foreach (var lot in pallet.Lots) pile.Add(lot);
        _pallets.Remove(pallet);
        if (pallet.FloorCell is not null) RebuildOccupancy();
        _events.Add(new PalletUnloaded(pallet.Id, worker.Id));
    }

    /// <summary>Goods on pallets waiting in a storage area, counted against its room.</summary>
    private IEnumerable<(ProductDef Product, int Quantity)> PalletsIn(int storageInstanceId) =>
        _pallets.Where(p => p.FloorCell is null && p.StorageInstanceId == storageInstanceId)
            .SelectMany(p => p.Lots)
            .Select(l => (_products[l.ProductId], l.Quantity));

    private ImportSave ImportSave() => new(
        _importRng.State, _shipments.ToList(), _pallets.ToList(), _nextShipmentId, _nextPalletId, new SortedDictionary<int, int>(_storageBonus));

    private void RestoreImports(ImportSave? save, ulong fallbackSeed)
    {
        if (save is null)
        {
            StartImportStream(fallbackSeed);
            return;
        }

        _importRng = SimRng.FromState(save.Rng);
        _shipments.AddRange(save.Shipments);
        _pallets.AddRange(save.Pallets);
        _nextShipmentId = save.NextShipmentId;
        _nextPalletId = save.NextPalletId;
        foreach (var (k, v) in save.StorageBonus) _storageBonus[k] = v;
    }
}
