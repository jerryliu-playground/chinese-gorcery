namespace ChineseGrocery.Sim.Grid;

/// <summary>The store floor: its size and the fixtures on it, ordered by instance id.</summary>
public sealed class StoreLayout
{
    private readonly List<PlacedFixture> _fixtures = [];

    internal StoreLayout(GridSize size) => Size = size;

    public GridSize Size { get; }
    public IReadOnlyList<PlacedFixture> Fixtures => _fixtures;
    public int NextInstanceId { get; private set; } = 1;

    public PlacedFixture? Find(int instanceId) => _fixtures.Find(f => f.InstanceId == instanceId);

    internal PlacedFixture Add(string fixtureId, GridPoint origin, Rotation rotation, long paidCents)
    {
        var placed = new PlacedFixture(NextInstanceId++, fixtureId, origin, paidCents, rotation);
        _fixtures.Add(placed);
        return placed;
    }

    internal void Remove(int instanceId) => _fixtures.RemoveAll(f => f.InstanceId == instanceId);

    internal void Move(int instanceId, GridPoint origin, Rotation rotation)
    {
        var i = _fixtures.FindIndex(f => f.InstanceId == instanceId);
        _fixtures[i] = _fixtures[i] with { Origin = origin, Rotation = rotation };
    }
}
