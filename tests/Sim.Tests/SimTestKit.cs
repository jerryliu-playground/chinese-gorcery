using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Tests;

/// <summary>Shared setup and drivers for simulation tests.</summary>
public static class SimTestKit
{
    public static GameData Data { get; } = GameDataLoader.LoadFromDirectory(RepoPaths.Data);

    public static GameSim NewSim(ulong seed = 1, GameData? data = null) => new(data ?? Data, seed);

    /// <summary>Enqueues commands and runs one tick so they take effect.</summary>
    public static void Run(this GameSim sim, params SimCommand[] commands)
    {
        foreach (var c in commands)
        {
            sim.Enqueue(c);
        }

        sim.Tick();
    }

    /// <summary>From PreOpen: opens the store and ticks until it closes.</summary>
    public static void OpenAndRunToClose(this GameSim sim)
    {
        sim.Run(new OpenStore());
        var guard = 24 * 60 + 1;
        while (sim.Clock.Phase != DayPhase.Closed)
        {
            sim.Tick();
            if (--guard == 0) throw new InvalidOperationException("Day never closed.");
        }
    }

    /// <summary>From PreOpen: runs a full day and lands on the next day's PreOpen.</summary>
    public static void RunFullDay(this GameSim sim)
    {
        sim.OpenAndRunToClose();
        sim.Run(new StartNextDay());
    }

    public static T Single<T>(this IEnumerable<SimEvent> events) where T : SimEvent => events.OfType<T>().Single();
}
