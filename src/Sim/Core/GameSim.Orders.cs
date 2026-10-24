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
        var held = Inventory.TryGet(StockLocation.Storage(destination.InstanceId))?.Lots.Select(l => (_products[l.ProductId], l.Quantity)) ?? [];
        var pending = _orders.Pending
            .Where(o => StorageRouting.FindDestination(Data, Layout.Fixtures, _products[o.ProductId])?.InstanceId == destination.InstanceId)
            .Select(o => (_products[o.ProductId], o.Quantity));
        var slots = StorageCapacity.SlotsNeeded(held.Concat(pending).Append((product, quantity)));
        return slots <= _fixtures[destination.FixtureId].Capacity;
    }

    private void DeliverOrders()
    {
        foreach (var order in _orders.TakeAll())
        {
            var product = _products[order.ProductId];
            var destination = StorageRouting.FindDestination(Data, Layout.Fixtures, product)
                ?? throw new InvalidOperationException($"No storage for delivered '{order.ProductId}'; selling should have been blocked.");
            var bestBefore = Clock.Day + product.ShelfLifeDays - 1;
            Inventory.GetOrCreate(StockLocation.Storage(destination.InstanceId)).Add(new StockLot(order.ProductId, order.Quantity, bestBefore, order.UnitCostCents));
            _events.Add(new OrderDelivered(Clock.Day, order.ProductId, order.Quantity, StockLocation.Storage(destination.InstanceId)));
        }
    }
}
