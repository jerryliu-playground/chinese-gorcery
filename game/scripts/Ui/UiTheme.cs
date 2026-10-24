using Godot;

namespace ChineseGrocery.Game.Ui;

/// <summary>Placeholder theme: the OS's Chinese UI font at a size that fits the 640x360 base resolution.</summary>
public static class UiTheme
{
    public const int FontSize = 10;

    /// <summary>
    /// Godot's automatic glyph fallback missed some simplified characters (e.g. U+6742), so name system Simplified
    /// Chinese fonts explicitly. Not PingFang: on macOS "PingFang SC" resolves to PingFang HK, which lacks some
    /// simplified glyphs. To be replaced by a bundled font when one is chosen.
    /// </summary>
    public static Theme Create() => new()
    {
        DefaultFont = new SystemFont { FontNames = ["Hiragino Sans GB", "STHeiti", "Microsoft YaHei", "Noto Sans CJK SC", "Source Han Sans SC", "sans-serif"] },
        DefaultFontSize = FontSize,
    };
}
