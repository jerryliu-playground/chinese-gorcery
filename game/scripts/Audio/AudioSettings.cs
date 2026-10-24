using Godot;

namespace ChineseGrocery.Game.Audio;

/// <summary>Master / music / effects volumes (0-1) on the audio buses, kept in the user settings file, not in saves.</summary>
public static class AudioSettings
{
    public const string MasterBus = "Master";
    public const string MusicBus = "Music";
    public const string SfxBus = "SFX";

    private const string SettingsPath = "user://settings.cfg";
    private const string Section = "audio";

    public static readonly string[] Buses = [MasterBus, MusicBus, SfxBus];

    /// <summary>Creates the Music and SFX buses (feeding Master) if the project has no bus layout for them.</summary>
    public static void EnsureBuses()
    {
        foreach (var name in new[] { MusicBus, SfxBus })
        {
            if (AudioServer.GetBusIndex(name) >= 0) continue;

            AudioServer.AddBus();
            var index = AudioServer.BusCount - 1;
            AudioServer.SetBusName(index, name);
            AudioServer.SetBusSend(index, MasterBus);
        }
    }

    public static float Get(string bus)
    {
        var config = new ConfigFile();
        return config.Load(SettingsPath) == Error.Ok ? (float)config.GetValue(Section, bus.ToLowerInvariant(), 1.0) : 1f;
    }

    public static void Set(string bus, float volume, bool save = true)
    {
        volume = Mathf.Clamp(volume, 0, 1);
        var index = AudioServer.GetBusIndex(bus);
        AudioServer.SetBusVolumeDb(index, Mathf.LinearToDb(Mathf.Max(volume, 0.0001f)));
        AudioServer.SetBusMute(index, volume <= 0.001f);
        if (!save) return;

        var config = new ConfigFile();
        config.Load(SettingsPath);
        config.SetValue(Section, bus.ToLowerInvariant(), volume);
        config.Save(SettingsPath);
    }

    public static void ApplySaved()
    {
        foreach (var bus in Buses) Set(bus, Get(bus), save: false);
    }
}
