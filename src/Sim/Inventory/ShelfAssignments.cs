namespace ChineseGrocery.Sim.Inventory;

/// <summary>Which product each shelf / display slot is meant to hold. Restock tasks keep assigned slots filled.</summary>
public sealed class ShelfAssignments
{
    private readonly SortedDictionary<StockLocation, string> _products = new();

    public IReadOnlyDictionary<StockLocation, string> Assigned => _products;

    public string? ProductAt(StockLocation slot) => _products.GetValueOrDefault(slot);

    internal void Set(StockLocation slot, string? productId)
    {
        if (productId is null) _products.Remove(slot);
        else _products[slot] = productId;
    }

    internal void Forget(int fixtureInstanceId)
    {
        foreach (var key in _products.Keys.Where(k => k.FixtureInstanceId == fixtureInstanceId).ToList())
        {
            _products.Remove(key);
        }
    }
}
