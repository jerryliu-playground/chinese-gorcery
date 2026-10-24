using ChineseGrocery.Game.Audio;
using Godot;

namespace ChineseGrocery.Game.Ui;

/// <summary>Volume sliders (master, music, effects), saved to the user settings, not to game saves.</summary>
public partial class SettingsPanel : PanelBase
{
    protected override void Rebuild()
    {
        SetTitle(UiText.T("ui.settings.title"));
        ClearBody();
        foreach (var (bus, key) in new[]
                 {
                     (AudioSettings.MasterBus, "ui.settings.master"), (AudioSettings.MusicBus, "ui.settings.music"), (AudioSettings.SfxBus, "ui.settings.sfx"),
                 })
        {
            var value = Text("", 40);
            var slider = new HSlider { MinValue = 0, MaxValue = 100, Step = 5, Value = AudioSettings.Get(bus) * 100, CustomMinimumSize = new Vector2(160, 16) };
            value.Text = $"{(int)slider.Value}%";
            slider.ValueChanged += v =>
            {
                AudioSettings.Set(bus, (float)v / 100);
                value.Text = $"{(int)v}%";
            };
            Body.AddChild(Row(Text(UiText.T(key), 70), slider, value));
        }

        if (!ResourceLoader.Exists("res://assets/audio/music.ogg") && !ResourceLoader.Exists("res://assets/audio/music.wav") && !ResourceLoader.Exists("res://assets/audio/music.mp3"))
        {
            Body.AddChild(Text(UiText.T("ui.settings.no_music")));
        }
    }
}
