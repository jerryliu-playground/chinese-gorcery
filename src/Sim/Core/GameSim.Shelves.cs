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
    private Rejection? HandleAssignShelfSlot(AssignShelfSlot assign)
    {
        if (Layout.Find(assign.InstanceId) is not { } placed) return Reject(RejectReason.UnknownFixtureInstance);

        var def = _fixtures[placed.FixtureId];
        if (!DataValidator.IsDisplay(def.Kind)) return Reject(RejectReason.NotADisplay);
        if (assign.Slot < 0 || assign.Slot >= def.Capacity) return Reject(RejectReason.InvalidSlot);

        if (assign.ProductId is not null)
        {
            if (!_products.TryGetValue(assign.ProductId, out var product)) return Reject(RejectReason.UnknownProduct);
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
