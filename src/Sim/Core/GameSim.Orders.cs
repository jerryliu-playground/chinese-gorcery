using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Core;

/// <summary>Ordering from the wholesaler and next-day delivery.</summary>
public sealed partial class GameSim
{
    private Rejection? HandlePlaceOrder(PlaceOrder order)
    {
        // Before opening or after closing; either way it is delivered before the next opening.
        if (Clock.Phase == DayPhase.Open) return Reject(RejectReason.WrongPhase);
        if (order.Quantity <= 0) return Reject(RejectReason.InvalidQuantity);
        if (!_products.TryGetValue(order.ProductId, out var product)) return Reject(RejectReason.UnknownProduct);
        if (!product.Orderable) return Reject(RejectReason.NotOrderable);
        if (!InOrderWindow(product.Id)) return Reject(RejectReason.OutOfSeason);
        if (IsOutOfStock(product.Id)) return Reject(RejectReason.SupplierOutOfStock);
        if (StorageRouting.FindDestination(Data, Layout.Fixtures, product) is null) return Reject(RejectReason.NoStorageForProduct);

        var unitCost = CostOf(product);
        var cost = checked(order.Quantity * unitCost);
        if (!Economy.CanAfford(cost)) return Reject(RejectReason.InsufficientCash);
        if (AllocateStorage(product, order.Quantity) is not { } parts) return Reject(RejectReason.StorageFull);

        Economy.Spend(ExpenseCategory.Purchases, cost);
        foreach (var (storage, units) in parts)
        {
            _orders.Add(new PendingOrder(product.Id, units, unitCost, Clock.Day, storage));
        }

        _events.Add(new OrderPlaced(Clock.Day, product.Id, order.Quantity, cost));
        return null;
    }

    /// <summary>
    /// Splits an order over the storage areas that can keep the product, filling each in turn (lowest id first).
    /// Null if all of them together cannot take it.
    /// </summary>
    private List<(int Storage, int Units)>? AllocateStorage(ProductDef product, int quantity)
    {
        var parts = new List<(int, int)>();
        var left = quantity;
        foreach (var storage in Layout.Fixtures.Where(f => CanStore(f, product)))
        {
            var take = Math.Min(left, FreeUnits(storage, product));
            if (take <= 0) continue;

            parts.Add((storage.InstanceId, take));
            left -= take;
            if (left == 0) return parts;
        }

        return null;
    }

    /// <summary>How many more units of the product could be ordered now, over every storage area that keeps it.</summary>
    public int RoomFor(string productId)
    {
        var product = _products[productId];
        return Layout.Fixtures.Where(f => CanStore(f, product)).Sum(f => FreeUnits(f, product));
    }

    /// <summary>Slots a storage area has taken, by stock in it and orders headed for it.</summary>
    public int SlotsUsed(int storageInstanceId) => StorageCapacity.SlotsNeeded(Holdings(storageInstanceId));

    private bool CanStore(PlacedFixture fixture, ProductDef product)
    {
        var def = _fixtures[fixture.FixtureId];
        return DataValidator.IsStorageArea(def.Kind) && def.Storage.Contains(product.Storage);
    }

    /// <summary>How many more units of the product this storage area can take, counting stock and orders already headed there.</summary>
    private int FreeUnits(PlacedFixture storage, ProductDef product)
    {
        var holdings = Holdings(storage.InstanceId);
        var otherSlots = StorageCapacity.SlotsNeeded(holdings.Where(h => h.Product.Id != product.Id));
        var mine = holdings.Where(h => h.Product.Id == product.Id).Sum(h => h.Quantity);
        return Math.Max(0, (_fixtures[storage.FixtureId].Capacity - otherSlots) * product.UnitsPerStorageSlot - mine);
    }

    private List<(ProductDef Product, int Quantity)> Holdings(int storageInstanceId)
    {
        var held = Inventory.TryGet(StockLocation.Storage(storageInstanceId))?.Lots.Select(l => (_products[l.ProductId], l.Quantity)) ?? [];
        var incoming = _orders.Pending
            .Where(o => DeliveryTarget(o)?.InstanceId == storageInstanceId)
            .Select(o => (_products[o.ProductId], o.Quantity));
        return held.Concat(incoming).ToList();
    }

    /// <summary>Where an order part will be delivered: its own storage area if it still stands, else the first that can keep it.</summary>
    private PlacedFixture? DeliveryTarget(PendingOrder order)
    {
        var product = _products[order.ProductId];
        return order.DestinationInstanceId is { } id && Layout.Find(id) is { } own && CanStore(own, product)
            ? own
            : StorageRouting.FindDestination(Data, Layout.Fixtures, product);
    }

    private void DeliverOrders()
    {
        foreach (var order in _orders.TakeAll())
        {
            var product = _products[order.ProductId];
            var destination = DeliveryTarget(order)
                ?? throw new InvalidOperationException($"No storage for delivered '{order.ProductId}'; selling should have been blocked.");
            var bestBefore = Clock.Day + product.ShelfLifeDays - 1;
            Inventory.GetOrCreate(StockLocation.Storage(destination.InstanceId)).Add(new StockLot(order.ProductId, order.Quantity, bestBefore, order.UnitCostCents));
            _events.Add(new OrderDelivered(Clock.Day, order.ProductId, order.Quantity, StockLocation.Storage(destination.InstanceId)));
        }
    }
}
