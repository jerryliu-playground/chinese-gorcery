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
            Console.Error.WriteLine("Usage: SimRunner [--days N] [--seed S] [--stock-target N] [--display-score 0-100] [--no-tank-cleaning] [--no-deli] [--auto] [--imports] [--data DIR]");
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
        Console.WriteLine($"Run: days={options.Days} seed={options.Seed} stockTarget={options.StockTarget}" +
                          (options.DisplayScore is { } fixedScore ? $" displayScore={fixedScore} (fixed)" : "") +
                          (options.NoTankCleaning ? " tankCleaning=off" : "") + (options.NoDeli ? " deli=off" : "") + (options.Auto ? " staff=auto" : "") + (options.Imports ? " imports=on" : ""));
        Console.WriteLine();

        var sim = new GameSim(data, options.Seed) { DisplayScoreOverride = options.DisplayScore, TankCleaningDisabled = options.NoTankCleaning };
        var produce = data.Products.Where(p => p.Department == Department.Produce).Select(p => p.Id).ToHashSet();
        var seafood = data.Products.Where(p => p.Department == Department.Seafood).Select(p => p.Id).ToHashSet();
        var deli = data.Products.Where(p => p.Department == Department.Deli).Select(p => p.Id).ToHashSet();
        var bot = new PlayerBot(options.StockTarget, runDeli: !options.NoDeli, autoStaff: options.Auto, imports: options.Imports);
        var anomalies = new List<string>();
        ReportPrinter.PrintHeader();

        for (var day = 0; day < options.Days; day++)
        {
            bot.PlayDay(sim);
            var report = sim.Reports[^1];
            ReportPrinter.PrintRow(report, produce, seafood, deli);
            foreach (var note in bot.TakeNotes()) Console.WriteLine("      * " + note);
            anomalies.AddRange(AnomalyCheck.Check(sim, report));
        }

        Console.WriteLine();
        var reports = sim.Reports;
        Console.WriteLine($"Totals: revenue={ReportPrinter.Money(reports.Sum(r => r.RevenueCents))} profit={ReportPrinter.Money(reports.Sum(r => r.ProfitCents))} " +
                          $"customers={reports.Sum(r => r.Customers)} angry={reports.Sum(r => r.AngryLeaves)} " +
                          $"notFound={reports.Sum(r => r.ItemsNotFound)} tooExpensive={reports.Sum(r => r.ItemsTooExpensive)}");
        var produceSales = reports.SelectMany(r => r.Sales).Where(s => produce.Contains(s.ProductId)).ToList();
        Console.WriteLine($"Produce: units={produceSales.Sum(s => s.Units)} revenue={ReportPrinter.Money(produceSales.Sum(s => s.RevenueCents))} " +
                          $"loss={ReportPrinter.Money(reports.Sum(r => r.ProduceLossCents))}");
        var seafoodSales = reports.SelectMany(r => r.Sales).Where(s => seafood.Contains(s.ProductId)).ToList();
        Console.WriteLine($"Seafood: units={seafoodSales.Sum(s => s.Units)} revenue={ReportPrinter.Money(seafoodSales.Sum(s => s.RevenueCents))} " +
                          $"deaths={reports.Sum(r => r.SeafoodDeaths)} loss={ReportPrinter.Money(reports.Sum(r => r.SeafoodLossCents))}");
        var deliSales = reports.SelectMany(r => r.Sales).Where(s => deli.Contains(s.ProductId)).ToList();
        Console.WriteLine($"Deli: units={deliSales.Sum(s => s.Units)} revenue={ReportPrinter.Money(deliSales.Sum(s => s.RevenueCents))} " +
                          $"loss={ReportPrinter.Money(reports.Sum(r => r.DeliLossCents))} nearExpiryCooked={ReportPrinter.Money(reports.Sum(r => r.KitchenRescuedCents))}");
        Console.WriteLine($"Waste by department: produce={ReportPrinter.Money(reports.Sum(r => r.ProduceLossCents))} " +
                          $"seafood={ReportPrinter.Money(reports.Sum(r => r.SeafoodLossCents))} meat={ReportPrinter.Money(reports.Sum(r => r.MeatLossCents))} " +
                          $"(together {ReportPrinter.Money(reports.Sum(r => r.ProduceLossCents + r.SeafoodLossCents + r.MeatLossCents))})");
        if (sim.Loan is { } loan)
        {
            Console.WriteLine($"Student loan: paid={ReportPrinter.Money(reports.Sum(r => r.LoanPaymentCents))} balance={ReportPrinter.Money(loan.BalanceCents)} " +
                              $"arrears={ReportPrinter.Money(loan.ArrearsCents)} competition={sim.CompetitionArrivalPermille()}/1000");
        }

        Console.WriteLine($"Stage: {sim.Economy.Stage}  staff: {string.Join(", ", sim.Workers.Where(w => w.RoleId is not null).Select(w => $"{w.RoleId} L{w.Level} knife {w.KnifeSkill}"))}");
        Console.WriteLine($"Rejected commands: {bot.RejectedCommands} {string.Join(", ", bot.Rejections.Select(kv => $"{kv.Key} x{kv.Value}"))}" +
                          $" (orders cut for storage space: {bot.OrdersCutForSpace})");
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

    private sealed record Options(int Days, ulong Seed, int StockTarget, string? DataDir, int? DisplayScore = null, bool NoTankCleaning = false, bool NoDeli = false, bool Auto = false, bool Imports = false);

    private static bool TryParseArgs(string[] args, out Options options, out string error)
    {
        options = new Options(Days: 30, Seed: 1, StockTarget: 40, DataDir: null);
        error = "";

        for (var i = 0; i < args.Length; i++)
        {
            var flag = args[i];
            if (flag == "--no-tank-cleaning")
            {
                options = options with { NoTankCleaning = true };
                continue;
            }

            if (flag == "--no-deli")
            {
                options = options with { NoDeli = true };
                continue;
            }

            if (flag == "--auto")
            {
                options = options with { Auto = true };
                continue;
            }

            if (flag == "--imports")
            {
                options = options with { Imports = true };
                continue;
            }

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
                case "--display-score" when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var score) && score <= 100:
                    options = options with { DisplayScore = score };
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
