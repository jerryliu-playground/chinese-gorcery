using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Tasks;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Core;

/// <summary>Assigning products to shelf and display slots.</summary>
public sealed partial class GameSim
{
    /// <summary>Fills every assigned slot from storage (oldest stock first), except slots someone is already restocking.</summary>
    private Rejection? HandleRestockAll()
    {
        if (Clock.Phase != DayPhase.PreOpen) return Reject(RejectReason.WrongPhase);

        var moved = FillShelvesFromStorage(announce: true);
        if (moved == 0) return Reject(RejectReason.NothingToDo);

        _events.Add(new ShelvesRestocked(moved));
        return null;
    }

    /// <summary>Moves storage stock onto assigned slots until full; returns the units moved.</summary>
    private int FillShelvesFromStorage(bool announce)
    {
        var moved = 0;
        foreach (var (slot, productId) in Shelves.Assigned)
        {
            if (Tasks.Tasks.Any(t => t is RestockTask r && r.Target == slot && Tasks.ClaimedBy(r.Id) is not null)) continue;

            var space = _products[productId].UnitsPerShelfSlot - Inventory.Count(slot, productId);
            var wasEmpty = Inventory.Count(slot, productId) == 0;
            var movedHere = 0;
            while (space > 0 && FindOldestInStorage(productId) is { } source)
            {
                var take = Math.Min(space, Inventory.Count(source, productId));
                var shelf = Inventory.GetOrCreate(slot);
                foreach (var lot in Inventory.Get(source).Take(productId, take)) shelf.Add(lot);
                space -= take;
                movedHere += take;
            }

            moved += movedHere;
            if (announce && movedHere > 0) _events.Add(new ItemsShelved(slot, productId, movedHere, wasEmpty));
        }

        return moved;
    }

    /// <summary>The storage area holding the earliest-expiring stock of a product.</summary>
    private StockLocation? FindOldestInStorage(string productId) =>
        Inventory.Locations
            .Where(p => IsStorageArea(p.Location.FixtureInstanceId) && p.Count(productId) > 0)
            .OrderBy(p => p.Lots.Where(l => l.ProductId == productId).Min(l => l.BestBeforeDay))
            .ThenBy(p => p.Location)
            .Select(p => (StockLocation?)p.Location)
            .FirstOrDefault();

    private Rejection? HandleAssignShelfSlot(AssignShelfSlot assign)
    {
        if (Layout.Find(assign.InstanceId) is not { } placed) return Reject(RejectReason.UnknownFixtureInstance);

        var def = _fixtures[placed.FixtureId];
        if (!DataValidator.IsDisplay(def.Kind)) return Reject(RejectReason.NotADisplay);
        if (assign.Slot < 0 || assign.Slot >= def.Capacity) return Reject(RejectReason.InvalidSlot);

        if (assign.ProductId is not null)
        {
            if (!_products.TryGetValue(assign.ProductId, out var product)) return Reject(RejectReason.UnknownProduct);
            if (product.IngredientOnly) return Reject(RejectReason.NotForSale);
            if (!def.Storage.Contains(product.Storage)) return Reject(RejectReason.WrongStorageType);
        }

        var slot = new StockLocation(assign.InstanceId, assign.Slot);
        var current = Shelves.ProductAt(slot);
        if (current is not null && current != assign.ProductId && Inventory.Count(slot, current) > 0) return Reject(RejectReason.SlotNotEmpty);
        if (current != assign.ProductId && Tasks.Tasks.Any(t => t is RestockTask r && r.Target == slot && Tasks.ClaimedBy(r.Id) is not null))
        {
            return Reject(RejectReason.SlotInUse);
        }

        Shelves.Set(slot, assign.ProductId);
        _events.Add(new ShelfSlotAssigned(slot, assign.ProductId));
        return null;
    }
}
