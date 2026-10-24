using ChineseGrocery.Sim.Data;

namespace ChineseGrocery.Sim.Inventory;

/// <summary>Slot accounting for storage areas: each product fills whole slots of its unitsPerStorageSlot.</summary>
public static class StorageCapacity
{
    /// <summary>Slots needed to hold the given units per product.</summary>
    public static int SlotsNeeded(IEnumerable<(ProductDef Product, int Units)> holdings) =>
        holdings
            .GroupBy(h => h.Product.Id)
            .Sum(g => CeilDiv(g.Sum(h => h.Units), g.First().Product.UnitsPerStorageSlot));

    private static int CeilDiv(int units, int perSlot) => (units + perSlot - 1) / perSlot;
}
