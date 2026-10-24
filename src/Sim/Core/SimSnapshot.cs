using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ChineseGrocery.Sim.Core;

/// <summary>Canonical text dump of the observable sim state, for determinism checks.</summary>
public static class SimSnapshot
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    public static string Describe(GameSim sim) => JsonSerializer.Serialize(new
    {
        sim.TickCount,
        sim.Clock.Day,
        Phase = sim.Clock.Phase.ToString(),
        sim.Clock.MinuteOfDay,
        sim.Economy.CashCents,
        sim.Economy.CumulativeRevenueCents,
        sim.Economy.Stage,
        Rng = sim.Rng.State,
        sim.Layout.Size,
        Layout = sim.Layout.Fixtures,
        sim.Layout.NextInstanceId,
        Inventory = sim.Inventory.Locations.Select(l => new { l.FixtureInstanceId, l.Lots }),
        sim.PendingOrders,
        sim.Reports,
    }, Options);

    /// <summary>Short SHA-256 of <see cref="Describe"/>, handy for comparing runs.</summary>
    public static string Hash(GameSim sim) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Describe(sim))))[..16];
}
