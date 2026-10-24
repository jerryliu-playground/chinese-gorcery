using ChineseGrocery.Sim.Data;

namespace ChineseGrocery.Sim.Tests.Data;

/// <summary>Sprite names in data must point at real files (entries without a sprite are allowed and logged by the game).</summary>
public class SpriteReferenceTests
{
    private static readonly string SpriteDir = Path.Combine(RepoPaths.Root, "game", "assets", "sprites");

    private static IEnumerable<string> Names(SpriteDef? s) =>
        s is null ? [] : new[] { s.Main, s.Alternate, s.Empty, s.Rest, s.Decoration }.OfType<string>();

    public static IEnumerable<string> AllReferencedSprites()
    {
        var d = SimTestKit.Data;
        return d.Products.Select(p => p.Sprite).OfType<string>()
            .Concat(d.Fixtures.SelectMany(f => Names(f.Sprite)))
            .Concat(d.CustomerTypes.SelectMany(t => Names(t.Sprite)))
            .Concat(d.Employees.Roles.SelectMany(r => Names(r.Sprite)))
            .Concat(Names(d.Employees.Player.Sprite))
            .Concat(d.Stages.SelectMany(s => new[] { s.FloorSprite, s.WallSprite }.OfType<string>()
                .Concat((s.Decorations ?? []).Select(x => x.Sprite))));
    }

    [Fact]
    public void Every_referenced_sprite_exists()
    {
        var missing = AllReferencedSprites().Distinct().Where(n => !File.Exists(Path.Combine(SpriteDir, n + ".png"))).ToList();

        Assert.NotEmpty(AllReferencedSprites());
        Assert.Empty(missing);
    }

    [Fact]
    public void Recolours_are_hex_colours()
    {
        var swaps = SimTestKit.Data.CustomerTypes.SelectMany(t => t.Sprite?.Recolor ?? []).ToList();

        Assert.All(swaps.SelectMany(s => new[] { s.From, s.To }), c => Assert.Matches("^#[0-9a-fA-F]{6}$", c));
    }
}
