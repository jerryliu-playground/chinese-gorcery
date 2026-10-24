using System.Globalization;
using ChineseGrocery.Sim.Data;

namespace ChineseGrocery.SimRunner;

/// <summary>Headless simulation runner. M0: loads and validates data only; the day loop arrives in M1.</summary>
public static class Program
{
    private const int ExitOk = 0;
    private const int ExitDataError = 1;
    private const int ExitUsageError = 2;

    public static int Main(string[] args)
    {
        if (!TryParseArgs(args, out var options, out var usageError))
        {
            Console.Error.WriteLine(usageError);
            Console.Error.WriteLine("Usage: SimRunner [--days N] [--seed S] [--data DIR]");
            return ExitUsageError;
        }

        var dataDir = options.DataDir ?? FindDataDirectory();
        if (dataDir is null)
        {
            Console.Error.WriteLine("Could not find the data directory; pass --data DIR.");
            return ExitUsageError;
        }

        GameData data;
        try
        {
            data = GameDataLoader.LoadFromDirectory(dataDir);
        }
        catch (DataValidationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitDataError;
        }

        Console.WriteLine($"Data OK ({Path.GetFullPath(dataDir)})");
        Console.WriteLine($"  products={data.Products.Count} fixtures={data.Fixtures.Count} customerTypes={data.CustomerTypes.Count} " +
                          $"roles={data.Employees.Roles.Count} meatCuts={data.MeatCuts.Count} stages={data.Stages.Count}");
        Console.WriteLine($"Run: days={options.Days} seed={options.Seed}");
        Console.WriteLine("Simulation not implemented yet (M1).");
        return ExitOk;
    }

    private sealed record Options(int Days, ulong Seed, string? DataDir);

    private static bool TryParseArgs(string[] args, out Options options, out string error)
    {
        options = new Options(Days: 1, Seed: 1, DataDir: null);
        error = "";

        for (var i = 0; i < args.Length; i++)
        {
            var flag = args[i];
            if (i + 1 >= args.Length)
            {
                error = $"Missing value for {flag}";
                return false;
            }

            var value = args[++i];
            switch (flag)
            {
                case "--days" when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var days) && days > 0:
                    options = options with { Days = days };
                    break;
                case "--seed" when ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seed):
                    options = options with { Seed = seed };
                    break;
                case "--data":
                    options = options with { DataDir = value };
                    break;
                default:
                    error = $"Invalid argument: {flag} {value}";
                    return false;
            }
        }

        return true;
    }

    /// <summary>Walks up from the working directory, then the binary's directory, looking for /data.</summary>
    private static string? FindDataDirectory()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "data");
                if (File.Exists(Path.Combine(candidate, DataFiles.Economy)))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
