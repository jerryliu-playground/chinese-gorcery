namespace ChineseGrocery.Sim.Inventory;

/// <summary>Where goods sit: a placed fixture and, for shelves and displays, one of its slots (storage areas use slot 0).</summary>
public readonly record struct StockLocation(int FixtureInstanceId, int Slot) : IComparable<StockLocation>
{
    public static StockLocation Storage(int fixtureInstanceId) => new(fixtureInstanceId, 0);

    public int CompareTo(StockLocation other)
    {
        var byFixture = FixtureInstanceId.CompareTo(other.FixtureInstanceId);
        return byFixture != 0 ? byFixture : Slot.CompareTo(other.Slot);
    }
}
