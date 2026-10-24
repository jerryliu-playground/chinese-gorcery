namespace ChineseGrocery.Sim.Inventory;

/// <summary>All stock in the store, by where it sits.</summary>
public sealed class InventoryState
{
    private readonly SortedDictionary<StockLocation, Stockpile> _locations = new();

    public IEnumerable<Stockpile> Locations => _locations.Values;

    public Stockpile Get(StockLocation location) => _locations[location];

    public Stockpile? TryGet(StockLocation location) => _locations.GetValueOrDefault(location);

    /// <summary>Units of the product at the location (0 when nothing is there).</summary>
    public int Count(StockLocation location, string productId) => TryGet(location)?.Count(productId) ?? 0;

    public int Total(string productId) => _locations.Values.Sum(s => s.Count(productId));

    /// <summary>True if any slot of the fixture holds goods.</summary>
    public bool Holds(int fixtureInstanceId) =>
        _locations.Any(kv => kv.Key.FixtureInstanceId == fixtureInstanceId && kv.Value.Lots.Count > 0);

    internal Stockpile GetOrCreate(StockLocation location)
    {
        if (!_locations.TryGetValue(location, out var pile))
        {
            pile = new Stockpile(location);
            _locations.Add(location, pile);
        }

        return pile;
    }

    internal void Forget(int fixtureInstanceId)
    {
        foreach (var key in _locations.Keys.Where(k => k.FixtureInstanceId == fixtureInstanceId).ToList())
        {
            _locations.Remove(key);
        }
    }
}
