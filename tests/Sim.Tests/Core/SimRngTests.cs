using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Tests.Core;

public class SimRngTests
{
    private static List<ulong> Take(SimRng rng, int n) => Enumerable.Range(0, n).Select(_ => rng.NextULong()).ToList();

    [Fact]
    public void Same_seed_gives_same_sequence()
    {
        Assert.Equal(Take(new SimRng(42), 100), Take(new SimRng(42), 100));
    }

    [Fact]
    public void Different_seeds_give_different_sequences()
    {
        Assert.NotEqual(Take(new SimRng(1), 10), Take(new SimRng(2), 10));
    }

    [Fact]
    public void Restoring_state_continues_the_same_sequence()
    {
        var rng = new SimRng(7);
        Take(rng, 13);
        var restored = SimRng.FromState(rng.State);

        Assert.Equal(Take(rng, 50), Take(restored, 50));
    }

    [Fact]
    public void Next_int_stays_in_range_and_covers_it()
    {
        var rng = new SimRng(3);
        var seen = Enumerable.Range(0, 1000).Select(_ => rng.NextInt(5, 9)).ToHashSet();

        Assert.Equal([5, 6, 7, 8], seen.Order());
    }

    [Fact]
    public void Next_int_rejects_empty_range()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimRng(1).NextInt(3, 3));
    }
}
