using Godot;

namespace ChineseGrocery.Game.Ui;

/// <summary>Chooses and remembers the UI language (zh / en). The choice is a view setting, not part of a save.</summary>
public static class Language
{
    private const string SettingsPath = "user://settings.cfg";
    private const string Section = "ui";
    private const string Key = "locale";

    public static readonly string[] Supported = ["zh", "en"];

    /// <summary>The remembered choice, else Chinese if the OS is set to Chinese, else English.</summary>
    public static void Apply()
    {
        var config = new ConfigFile();
        var saved = config.Load(SettingsPath) == Error.Ok ? (string)config.GetValue(Section, Key, "") : "";
        var locale = Supported.Contains(saved) ? saved : OS.GetLocaleLanguage() == "zh" ? "zh" : "en";
        TranslationServer.SetLocale(locale);
    }

    public static void Toggle()
    {
        var next = TranslationServer.GetLocale().StartsWith("zh", StringComparison.Ordinal) ? "en" : "zh";
        TranslationServer.SetLocale(next);

        var config = new ConfigFile();
        config.Load(SettingsPath);
        config.SetValue(Section, Key, next);
        config.Save(SettingsPath);
    }
}
