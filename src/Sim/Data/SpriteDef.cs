namespace ChineseGrocery.Sim.Data;

/// <summary>
/// Display-only art references (sprite names under /game/assets/sprites, no extension). The Sim never reads these.
/// </summary>
/// <param name="Main">The usual look (for multi-cell fixtures, the first cell).</param>
/// <param name="Alternate">Shelves: a second stocked look, alternated between neighbouring cells.</param>
/// <param name="Empty">Shelves: look when out of stock.</param>
/// <param name="Rest">Multi-cell fixtures: look of the cells after the first (e.g. plain counter next to the register).</param>
/// <param name="Dirty">Fish tanks: look when the water is bad.</param>
/// <param name="Decoration">Ornament drawn on top (e.g. a lucky cat on the counter).</param>
/// <param name="Recolor">Palette swaps (exact #RRGGBB colours), for a type that borrows another's sprite.</param>
public sealed record SpriteDef(
    string Main,
    string? Alternate = null,
    string? Empty = null,
    string? Rest = null,
    string? Decoration = null,
    IReadOnlyList<ColorSwapDef>? Recolor = null,
    string? Dirty = null);

/// <summary>Replace one exact sprite colour with another.</summary>
public sealed record ColorSwapDef(string From, string To);
