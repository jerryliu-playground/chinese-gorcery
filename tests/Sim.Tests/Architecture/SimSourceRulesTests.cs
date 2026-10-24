namespace ChineseGrocery.Sim.Tests.Architecture;

/// <summary>Guards the CLAUDE.md architecture rules on the Sim source tree.</summary>
public class SimSourceRulesTests
{
    public static TheoryData<string> ForbiddenTokens => new()
    {
        "using Godot",
        "DateTime.Now",
        "DateTime.UtcNow",
        "Random.Shared",
        "new Random(",
        "Guid.NewGuid",
        "float ",
        "double ",
        "decimal ",
    };

    [Theory]
    [MemberData(nameof(ForbiddenTokens))]
    public void Sim_source_does_not_contain(string token)
    {
        var offenders = Directory.EnumerateFiles(RepoPaths.SimSource, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => File.ReadAllText(f).Contains(token, StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(RepoPaths.Root, f));

        Assert.Empty(offenders);
    }

    [Fact]
    public void Sim_project_does_not_reference_godot()
    {
        var csproj = File.ReadAllText(Path.Combine(RepoPaths.SimSource, "Sim.csproj"));

        Assert.DoesNotContain("Godot", csproj, StringComparison.OrdinalIgnoreCase);
    }
}
