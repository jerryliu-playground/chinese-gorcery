using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Tasks;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Tests;

/// <summary>Shared setup and drivers for simulation tests.</summary>
public static class SimTestKit
{
    /// <summary>The real data tables as shipped (including the opening stock).</summary>
    public static GameData RealData { get; } = GameDataLoader.LoadFromDirectory(RepoPaths.Data);

    /// <summary>
    /// The real data minus the opening stock and pre-assigned shelf slots, so mechanics tests start from an empty
    /// store and control exactly what is in it. The calendar keeps its dates, but there is no seasonal demand, no
    /// festivals and no random events (calendar tests use <see cref="CalendarData"/>).
    /// </summary>
    public static GameData Data { get; } = RealData with
    {
        Economy = RealData.Economy with
        {
            StartingStock = [],
            StartingLayout = RealData.Economy.StartingLayout.Select(e => e with { Slots = null }).ToList(),
        },
        Calendar = RealData.Calendar! with { SeasonalDemand = [] },
        Festivals = null,
        Events = null,
    };

    /// <summary><see cref="Data"/> with the real seasons, festivals and random events back on.</summary>
    public static GameData CalendarData { get; } = Data with { Calendar = RealData.Calendar, Festivals = RealData.Festivals, Events = RealData.Events };

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
        var guard = 24 * 60 * 60 + 1;
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

    /// <summary>Walks the player to a cell and ticks until they arrive.</summary>
    public static void WalkPlayerTo(this GameSim sim, GridPoint cell)
    {
        sim.Run(new MovePlayer(cell));
        for (var guard = 0; sim.Player.Path.Count > 0; guard++)
        {
            if (guard == 1000) throw new InvalidOperationException($"Player never reached {cell}.");
            sim.Tick();
        }
    }

    /// <summary>Instance id of the first placed fixture with this definition id.</summary>
    public static int InstanceOf(this GameSim sim, string fixtureId) =>
        sim.Layout.Fixtures.First(f => f.FixtureId == fixtureId).InstanceId;

    public static T Single<T>(this IEnumerable<SimEvent> events) where T : SimEvent => events.OfType<T>().Single();
}
