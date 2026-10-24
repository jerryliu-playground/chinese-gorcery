using System.Globalization;
using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;

namespace ChineseGrocery.SimRunner;

/// <summary>Headless simulation runner: plays N days with a player bot and prints the daily reports.</summary>
public static class Program
{
    private const int ExitOk = 0;
    private const int ExitDataError = 1;
    private const int ExitUsageError = 2;
    private const int ExitAnomaly = 3;

    public static int Main(string[] args)
    {
        if (!TryParseArgs(args, out var options, out var usageError))
        {
            Console.Error.WriteLine(usageError);
            Console.Error.WriteLine("Usage: SimRunner [--days N] [--seed S] [--stock-target N] [--data DIR]");
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
        Console.WriteLine($"Run: days={options.Days} seed={options.Seed} stockTarget={options.StockTarget}");
        Console.WriteLine();

        var sim = new GameSim(data, options.Seed);
        var bot = new PlayerBot(options.StockTarget);
        var anomalies = new List<string>();
        ReportPrinter.PrintHeader();

        for (var day = 0; day < options.Days; day++)
        {
            bot.PlayDay(sim);
            var report = sim.Reports[^1];
            ReportPrinter.PrintRow(report);
            anomalies.AddRange(AnomalyCheck.Check(sim, report));
        }

        Console.WriteLine();
        var reports = sim.Reports;
        Console.WriteLine($"Totals: revenue={ReportPrinter.Money(reports.Sum(r => r.RevenueCents))} profit={ReportPrinter.Money(reports.Sum(r => r.ProfitCents))} " +
                          $"customers={reports.Sum(r => r.Customers)} angry={reports.Sum(r => r.AngryLeaves)} " +
                          $"notFound={reports.Sum(r => r.ItemsNotFound)} tooExpensive={reports.Sum(r => r.ItemsTooExpensive)}");
        Console.WriteLine($"Rejected commands: {bot.RejectedCommands} (orders cut for storage space: {bot.OrdersCutForSpace})");
        Console.WriteLine($"State hash: {SimSnapshot.Hash(sim)}");

        if (anomalies.Count > 0)
        {
            Console.WriteLine($"ANOMALIES ({anomalies.Count}):");
            anomalies.ForEach(a => Console.WriteLine("  - " + a));
            return ExitAnomaly;
        }

        Console.WriteLine("No anomalies.");
        return ExitOk;
    }

    private sealed record Options(int Days, ulong Seed, int StockTarget, string? DataDir);

    private static bool TryParseArgs(string[] args, out Options options, out string error)
    {
        options = new Options(Days: 30, Seed: 1, StockTarget: 40, DataDir: null);
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
                case "--stock-target" when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var target) && target > 0:
                    options = options with { StockTarget = target };
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
