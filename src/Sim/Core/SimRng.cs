namespace ChineseGrocery.Sim.Core;

/// <summary>Seeded xoshiro256** generator. The only source of randomness in the Sim; its state is saveable.</summary>
public sealed class SimRng
{
    private ulong _s0, _s1, _s2, _s3;

    public SimRng(ulong seed)
    {
        var x = seed;
        _s0 = SplitMix64(ref x);
        _s1 = SplitMix64(ref x);
        _s2 = SplitMix64(ref x);
        _s3 = SplitMix64(ref x);
    }

    private SimRng(RngState state) => (_s0, _s1, _s2, _s3) = (state.S0, state.S1, state.S2, state.S3);

    public RngState State => new(_s0, _s1, _s2, _s3);

    public static SimRng FromState(RngState state)
    {
        if ((state.S0 | state.S1 | state.S2 | state.S3) == 0) throw new ArgumentException("All-zero RNG state is invalid.", nameof(state));
        return new SimRng(state);
    }

    public ulong NextULong()
    {
        var result = RotateLeft(_s1 * 5, 7) * 9;
        var t = _s1 << 17;
        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = RotateLeft(_s3, 45);
        return result;
    }

    /// <summary>Uniform integer in [<paramref name="minInclusive"/>, <paramref name="maxExclusive"/>), without modulo bias.</summary>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Range is empty.");

        var range = (ulong)((long)maxExclusive - minInclusive);
        var threshold = (0UL - range) % range;
        ulong r;
        do
        {
            r = NextULong();
        }
        while (r < threshold);

        return (int)((long)minInclusive + (long)(r % range));
    }

    private static ulong RotateLeft(ulong x, int k) => (x << k) | (x >> (64 - k));

    private static ulong SplitMix64(ref ulong x)
    {
        var z = x += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
