namespace ChineseGrocery.Sim.Inventory;

/// <summary>All stock in the store, by the placed fixture holding it.</summary>
public sealed class InventoryState
{
    private readonly SortedDictionary<int, Stockpile> _locations = new();

    public IEnumerable<Stockpile> Locations => _locations.Values;

    public Stockpile Get(int fixtureInstanceId) => _locations[fixtureInstanceId];

    public Stockpile? TryGet(int fixtureInstanceId) => _locations.GetValueOrDefault(fixtureInstanceId);

    /// <summary>True if the fixture holds any goods.</summary>
    public bool Holds(int fixtureInstanceId) => _locations.TryGetValue(fixtureInstanceId, out var pile) && pile.Lots.Count > 0;

    public int Total(string productId) => _locations.Values.Sum(s => s.Count(productId));

    internal Stockpile GetOrCreate(int fixtureInstanceId)
    {
        if (!_locations.TryGetValue(fixtureInstanceId, out var pile))
        {
            pile = new Stockpile(fixtureInstanceId);
            _locations.Add(fixtureInstanceId, pile);
        }

        return pile;
    }

    internal void Forget(int fixtureInstanceId) => _locations.Remove(fixtureInstanceId);
}
