namespace ChineseGrocery.Sim.Inventory;

/// <summary>All stock in the store, by location id (a storage fixture id until placed fixtures exist).</summary>
public sealed class InventoryState
{
    private readonly SortedDictionary<string, Stockpile> _locations = new(StringComparer.Ordinal);

    public IEnumerable<Stockpile> Locations => _locations.Values;

    public Stockpile Get(string locationId) => _locations[locationId];

    public int Total(string productId) => _locations.Values.Sum(s => s.Count(productId));

    internal Stockpile GetOrCreate(string locationId)
    {
        if (!_locations.TryGetValue(locationId, out var pile))
        {
            pile = new Stockpile(locationId);
            _locations.Add(locationId, pile);
        }

        return pile;
    }
}
