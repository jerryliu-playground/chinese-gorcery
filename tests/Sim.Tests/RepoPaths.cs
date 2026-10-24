namespace ChineseGrocery.Sim.Tests;

/// <summary>Locates repository folders from the test binary's location.</summary>
public static class RepoPaths
{
    public static string Root { get; } = FindRoot();

    public static string Data => Path.Combine(Root, "data");
    public static string SimSource => Path.Combine(Root, "src", "Sim");
    public static string StringsCsv => Path.Combine(Root, "game", "locale", "strings.csv");

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ChineseGrocery.sln")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Repository root (ChineseGrocery.sln) not found.");
    }
}
