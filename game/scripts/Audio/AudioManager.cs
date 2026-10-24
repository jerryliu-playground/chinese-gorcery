using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Customers;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Tasks;
using Godot;

namespace ChineseGrocery.Game.Audio;

/// <summary>
/// Plays sound for what happens in the store (from Sim events and what the view sees), the shop ambience, background
/// music when a music file exists, and a click for every button. Sounds come from game/assets/audio.
/// </summary>
public partial class AudioManager : Node
{
    private const string Folder = "res://assets/audio/";
    private const int Voices = 8;

    // Headroom so several effects at once (busy store at 4x) do not clip.
    private const float EffectVolumeDb = -9;
    private const float AmbienceVolumeDb = -20;
    private const float MusicVolumeDb = -8;

    private readonly List<AudioStreamPlayer> _voices = [];
    private readonly Dictionary<string, AudioStream?> _streams = [];
    private readonly Dictionary<string, double> _lastPlayed = [];
    private SimHost _host = null!;
    private View.FeelConfig _feel = null!;
    private Sim.Grid.GridPoint _lastPlayerCell;
    private double _now;
    private double _nextChop;

    public void Init(SimHost host, View.FeelConfig feel)
    {
        _host = host;
        _feel = feel;
        AudioSettings.EnsureBuses();
        AudioSettings.ApplySaved();

        for (var i = 0; i < Voices; i++)
        {
            var voice = new AudioStreamPlayer { Bus = AudioSettings.SfxBus, VolumeDb = EffectVolumeDb };
            AddChild(voice);
            _voices.Add(voice);
        }

        // Headless runs (build checks) have no sound, and the dummy audio driver never releases a looping playback.
        if (DisplayServer.GetName() != "headless")
        {
            StartLoop("ambient_shop", AudioSettings.SfxBus, AmbienceVolumeDb);
            StartLoop("music", AudioSettings.MusicBus, MusicVolumeDb, optional: true);
        }

        host.EventsReady += OnEvents;
        GetTree().NodeAdded += node =>
        {
            if (node is BaseButton button) button.Pressed += () => Play("ui_click", 0.03);
        };
        _lastPlayerCell = host.Sim.Player.Position;
    }

    private AudioStream? Stream(string name)
    {
        if (_streams.TryGetValue(name, out var cached)) return cached;

        AudioStream? stream = null;
        foreach (var ext in new[] { ".wav", ".ogg", ".mp3" })
        {
            if (!ResourceLoader.Exists(Folder + name + ext)) continue;
            stream = GD.Load<AudioStream>(Folder + name + ext);
            break;
        }

        _streams[name] = stream;
        return stream;
    }

    private void StartLoop(string name, string bus, float volumeDb, bool optional = false)
    {
        if (Stream(name) is not { } stream)
        {
            if (!optional) GD.PushWarning($"Missing sound: {name}");
            else GD.Print($"Audio: no {name} file yet, staying silent.");
            return;
        }

        // WAV loops are set in the file's import settings (ambient_shop.wav.import); compressed music loops here.
        switch (stream)
        {
            case AudioStreamOggVorbis ogg:
                ogg.Loop = true;
                break;
            case AudioStreamMP3 mp3:
                mp3.Loop = true;
                break;
        }

        var player = new AudioStreamPlayer { Stream = stream, Bus = bus, VolumeDb = volumeDb, Autoplay = true };
        AddChild(player);
    }

    /// <summary>Plays a one-shot effect, at most once per <paramref name="minGap"/> seconds per sound.</summary>
    public void Play(string name, double minGap = 0.05)
    {
        if (_lastPlayed.TryGetValue(name, out var last) && _now - last < minGap) return;
        if (Stream(name) is not { } stream) return;

        _lastPlayed[name] = _now;
        var voice = _voices.FirstOrDefault(v => !v.Playing) ?? _voices[0];
        voice.Stream = stream;
        voice.Play();
    }

    private void OnEvents(IReadOnlyList<SimEvent> events)
    {
        foreach (var e in events)
        {
            switch (e)
            {
                case CheckoutCompleted:
                    Play("cash_ding", 0.1);
                    break;
                case ItemsShelved:
                    Play("shelve", 0.1);
                    break;
                case CustomerEntered:
                    Play("door_bell", 0.5);
                    break;
                case CustomerReaction { Kind: CustomerReactionKind.GaveUp }:
                    Play("angry", 0.3);
                    break;
                case StageUpgraded:
                    Play("celebrate");
                    break;
            }
        }
    }

    /// <summary>Stops and lets go of every stream so nothing (e.g. the looping ambience) is still held at exit.</summary>
    public override void _ExitTree()
    {
        foreach (var player in GetChildren().OfType<AudioStreamPlayer>())
        {
            player.Stop();
            player.Stream = null;
        }

        _streams.Clear();
    }

    public override void _Process(double delta)
    {
        _now += delta;
        if (_host?.Sim is not { } sim) return;

        // Footsteps when the player reaches a new cell.
        if (sim.Player.Position != _lastPlayerCell)
        {
            _lastPlayerCell = sim.Player.Position;
            Play("footstep", 0.12);
        }

        // Chopping in time with the cleaver drawn at the table.
        if (sim.Workers.Any(w => w.Step == WorkStep.Cutting) && _host.SpeedMultiplier > 0 && _now >= _nextChop)
        {
            Play("chop", 0.05);
            _nextChop = _now + Mathf.Max(0.05f, _feel.ChopSeconds) / _host.SpeedMultiplier;
        }
    }
}
