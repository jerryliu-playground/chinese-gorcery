using Godot;

namespace ChineseGrocery.Game.Ui;

/// <summary>UI theme: the bundled Fusion Pixel Font (Simplified Chinese, 12 px, SIL OFL 1.1; see game/fonts/fusion-pixel).</summary>
public static class UiTheme
{
    /// <summary>The font's native pixel size; other sizes would blur the pixel glyphs.</summary>
    public const int FontSize = 12;

    private const string FontPath = "res://fonts/fusion-pixel/fusion-pixel-12px-proportional-zh_hans.ttf.woff2";

    public static Theme Create() => new() { DefaultFont = Font(), DefaultFontSize = FontSize };

    /// <summary>The UI font with pixel-crisp settings, for drawing text outside controls (pop-ups, bubbles).</summary>
    public static FontFile Font()
    {
        var font = GD.Load<FontFile>(FontPath);
        font.Antialiasing = TextServer.FontAntialiasing.None;
        font.Hinting = TextServer.Hinting.None;
        font.SubpixelPositioning = TextServer.SubpixelPositioning.Disabled;
        font.GenerateMipmaps = false;
        return font;
    }
}
