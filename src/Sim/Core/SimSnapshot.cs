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
        Inventory = sim.Inventory.Locations.Select(l => new { l.Location, l.Lots }),
        sim.PendingOrders,
        Shelves = sim.Shelves.Assigned.Select(kv => new { kv.Key, kv.Value }),
        Prices = sim.Prices.Overrides,
        Workers = sim.Workers.Select(w => new
        {
            w.Id, w.RoleId, w.Position, w.Path, w.TaskId, Step = w.Step.ToString(), w.WorkTicksLeft, w.Carrying,
            w.KnifeSkill, w.WorkSpeedPermille, w.WageCentsPerDay, w.MoralePermille, w.Level, w.Xp,
        }),
        Tasks = sim.Tasks.Tasks.Select(t => new { Task = t.ToString(), ClaimedBy = sim.Tasks.ClaimedBy(t.Id) }),
        Customers = sim.Customers.Select(c => new
        {
            c.Id, c.TypeId, c.Position, c.Path, State = c.State.ToString(), c.ShoppingList, c.Basket, c.Target,
            c.BrowseTicksLeft, c.CheckoutInstanceId, c.QueueNumber, c.WaitedMinutes, c.ItemsNotFound, c.ItemsTooExpensive,
        }),
        sim.Reports,
    }, Options);

    /// <summary>Short SHA-256 of <see cref="Describe"/>, handy for comparing runs.</summary>
    public static string Hash(GameSim sim) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Describe(sim))))[..16];
}
