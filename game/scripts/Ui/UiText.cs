using System.Globalization;
using System.Text;
using Godot;

namespace ChineseGrocery.Game.Ui;

/// <summary>Translation and formatting helpers. All player-facing text goes through translation keys.</summary>
public static class UiText
{
    public static string T(string key) => TranslationServer.Translate(key).ToString();

    /// <summary>Translates <paramref name="key"/> and fills <c>{name}</c> placeholders.</summary>
    public static string F(string key, params (string Name, object Value)[] args)
    {
        var text = T(key);
        foreach (var (name, value) in args)
        {
            text = text.Replace("{" + name + "}", Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        return text;
    }

    /// <summary>Formats cents as dollars, e.g. -$1,234.56.</summary>
    public static string Money(long cents)
    {
        var sign = cents < 0 ? "-" : "";
        var abs = Math.Abs(cents);
        return string.Create(CultureInfo.InvariantCulture, $"{sign}${abs / 100:#,0}.{abs % 100:00}");
    }

    public static string ClockTime(int minuteOfDay) =>
        string.Create(CultureInfo.InvariantCulture, $"{minuteOfDay / 60 % 24:00}:{minuteOfDay % 60:00}");

    /// <summary>Enum value as a translation-key segment: WrongPhase -> wrong_phase.</summary>
    public static string Snake(Enum value)
    {
        var name = value.ToString();
        var sb = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0) sb.Append('_');
            sb.Append(char.ToLowerInvariant(name[i]));
        }

        return sb.ToString();
    }
}
