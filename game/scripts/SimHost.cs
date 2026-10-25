using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Save;
using Godot;
using FileAccess = Godot.FileAccess;

namespace ChineseGrocery.Game;

/// <summary>
/// Owns the simulation and turns real time into ticks. At 1x the open part of the day lasts the configured number of
/// real seconds; the same tick rate keeps the player moving before opening and after closing (when the clock is frozen).
/// Pause stops ticking entirely; queued commands then apply on the next tick.
/// </summary>
public partial class SimHost : Node
{
    private const int MaxTicksPerFrame = 30;
    private const string SavePath = "user://save1.json";

    private double _pendingTicks;

    public GameData Data { get; private set; } = null!;
    public GameSim Sim { get; private set; } = null!;

    /// <summary>0 = paused, otherwise an index + 1 into economy.json speedMultipliers.</summary>
    public int SpeedStep { get; private set; } = 1;

    public int SpeedMultiplier => SpeedStep == 0 ? 0 : Data.Economy.SpeedMultipliers[SpeedStep - 1];

    /// <summary>Fraction of the way to the next tick, for smoothing movement.</summary>
    public float TickProgress => (float)_pendingTicks;

    /// <summary>
    /// While the Sim has a dialogue waiting (the dialogue box shows it), time stands still whatever the speed. Developer
    /// tours that hide dialogues turn this off.
    /// </summary>
    public bool PauseForDialogue { get; set; } = true;

    private bool DialogueWaiting => PauseForDialogue && Data.Story is not null && Sim.Story.Current is not null;

    public event Action<IReadOnlyList<SimEvent>>? EventsReady;
    public event Action? Ticked;

    /// <summary>Raised after a save is loaded and <see cref="Sim"/> is a different game.</summary>
    public event Action? GameReplaced;

    public bool HasSave => FileAccess.FileExists(SavePath);

    /// <summary>Writes the single save slot. Returns false (and logs why) if it could not be written.</summary>
    public bool SaveToSlot()
    {
        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        if (file is null)
        {
            GD.PushError($"Could not write {SavePath}: {FileAccess.GetOpenError()}");
            return false;
        }

        file.StoreString(SaveGame.Write(Sim));
        return true;
    }

    /// <summary>Replaces the running game with one read from save text (also used by the developer screenshot tour).</summary>
    public void LoadFromJson(string json)
    {
        Sim = SaveGame.Read(Data, json);
        _pendingTicks = 0;
        GameReplaced?.Invoke();
    }

    /// <summary>Replaces the running game with the saved one. Returns false (and logs why) if it could not be loaded.</summary>
    public bool LoadFromSlot()
    {
        if (!HasSave) return false;

        try
        {
            Sim = SaveGame.Read(Data, FileAccess.GetFileAsString(SavePath));
        }
        catch (SaveException ex)
        {
            GD.PushError(ex.Message);
            return false;
        }

        _pendingTicks = 0;
        GameReplaced?.Invoke();
        return true;
    }

    /// <summary>Loads data and starts a new game (seeded from the clock unless a seed is given). Returns the data error text, or null on success.</summary>
    public string? StartNewGame(ulong? seed = null)
    {
        var dataDir = ProjectSettings.GlobalizePath("res://").PathJoin("../data").SimplifyPath();
        try
        {
            Data = GameDataLoader.LoadFromDirectory(dataDir);
        }
        catch (DataValidationException ex)
        {
            GD.PushError(ex.Message);
            return ex.Message;
        }

        // Every portrait and line the dialogues use must exist (spec 18.7.2).
        if (Data.Story is { } story)
        {
            var problems = StoryValidator.CheckAssets(
                story, s => ResourceLoader.Exists("res://assets/sprites/" + s + ".png"), k => TranslationServer.Translate(k).ToString() != k);
            if (problems.Count > 0)
            {
                var message = string.Join("\n", problems);
                GD.PushError(message);
                return message;
            }
        }

        // The seed only has to differ between games; the Sim itself stays deterministic for a given seed.
        Sim = new GameSim(Data, seed ?? (ulong)Time.GetUnixTimeFromSystem());
        return null;
    }

    public void Send(SimCommand command) => Sim.Enqueue(command);

    /// <summary>Applies a command at once with one tick, even while paused (dialogue answers must not wait for time to run).</summary>
    public void SendNow(SimCommand command)
    {
        Sim.Enqueue(command);
        Sim.Tick();
        var events = Sim.DrainEvents();
        if (events.Count > 0) EventsReady?.Invoke(events);
        Ticked?.Invoke();
    }

    public void SetSpeedStep(int step) => SpeedStep = Math.Clamp(step, 0, Data.Economy.SpeedMultipliers.Count);

    public override void _Process(double delta)
    {
        if (Sim is null || SpeedStep == 0 || DialogueWaiting) return;

        var e = Data.Economy;
        var ticksPerDayOpen = (double)(e.CloseMinuteOfDay - e.OpenMinuteOfDay) * 60 / e.SecondsPerTick;
        _pendingTicks += delta * ticksPerDayOpen / e.RealSecondsPerOpenPhaseAt1x * SpeedMultiplier;

        for (var i = 0; _pendingTicks >= 1 && i < MaxTicksPerFrame; i++)
        {
            _pendingTicks -= 1;
            Sim.Tick();
            var events = Sim.DrainEvents();
            if (events.Count > 0) EventsReady?.Invoke(events);
            Ticked?.Invoke();
            if (DialogueWaiting) break;
        }

        _pendingTicks = Math.Min(_pendingTicks, 1);
    }
}
