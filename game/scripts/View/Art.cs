using ChineseGrocery.Sim.Data;
using Godot;

namespace ChineseGrocery.Game.View;

/// <summary>
/// Loads sprites by the names the data files give (game/assets/sprites/NAME.png). Anything without a sprite, or whose
/// file is missing, falls back to a flat colour block and is listed once in the startup log.
/// </summary>
public sealed class Art
{
    private const string Folder = "res://assets/sprites/";

    private readonly Dictionary<string, Texture2D?> _cache = [];

    public Texture2D? Get(string? name)
    {
        if (name is null) return null;
        if (_cache.TryGetValue(name, out var cached)) return cached;

        var path = Folder + name + ".png";
        var texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
        _cache[name] = texture;
        return texture;
    }

    /// <summary>Logs every data entry that will be drawn as a colour block (no sprite given, or the file is missing).</summary>
    public void LogMissing(GameData data)
    {
        var missing = new List<string>();
        void Check(string what, string? sprite)
        {
            if (sprite is null) missing.Add($"{what} (no sprite in data)");
            else if (Get(sprite) is null) missing.Add($"{what} (file not found: {sprite}.png)");
        }

        foreach (var p in data.Products) Check($"product {p.Id}", p.Sprite);
        foreach (var f in data.Fixtures) Check($"fixture {f.Id}", f.Sprite?.Main);
        foreach (var t in data.CustomerTypes) Check($"customer type {t.Id}", t.Sprite?.Main);
        foreach (var r in data.Employees.Roles) Check($"role {r.Id}", r.Sprite?.Main);
        Check("player", data.Employees.Player.Sprite?.Main);

        GD.Print(missing.Count == 0
            ? "Art: every data entry has a sprite."
            : $"Art: {missing.Count} entries drawn as colour blocks:\n  " + string.Join("\n  ", missing));
    }

    public static Color? ParseTint(string? hex) => hex is null ? null : Color.FromHtml(hex);
}
