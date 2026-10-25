using System.Globalization;
using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Economy;

namespace ChineseGrocery.SimRunner;

/// <summary>Prints daily reports as a fixed-width table. Developer output, not game UI.</summary>
public static class ReportPrinter
{
    private const string Format = "{0,4} {1,11} {2,10} {3,10} {4,9} {5,9} {6,9} {7,11} {8,11} {9,5} {10,5} {11,8} {12,7}";

    public static void PrintHeader()
    {
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, Format,
            "Day", "CashStart", "Revenue", "Purchases", "Wages", "Fixtures", "Loss", "Profit", "CashEnd", "Cust", "Angry", "NotFound", "TooExp"));
    }

    public static void PrintRow(DailyReport r, ISet<string> produce, ISet<string> seafood, ISet<string> deli)
    {
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, Format,
            r.Day, Money(r.CashStartCents), Money(r.RevenueCents), Money(r.PurchaseCostCents), Money(r.WagesCents),
            Money(r.FixtureSpendCents), Money(r.LossCents), Money(r.ProfitCents), Money(r.CashEndCents), r.Customers, r.AngryLeaves,
            r.ItemsNotFound, r.ItemsTooExpensive));

        if (r.Sales.Count > 0)
        {
            Console.WriteLine("      sold: " + string.Join(", ", r.Sales.Select(s => $"{s.ProductId} {s.Units}")));
        }

        var produceSales = r.Sales.Where(s => produce.Contains(s.ProductId)).ToList();
        if (produceSales.Count > 0 || r.ProduceLossCents > 0)
        {
            Console.WriteLine($"      produce: units {produceSales.Sum(s => s.Units)}, revenue {Money(produceSales.Sum(s => s.RevenueCents))}, loss {Money(r.ProduceLossCents)}");
        }

        var seafoodSales = r.Sales.Where(s => seafood.Contains(s.ProductId)).ToList();
        if (seafoodSales.Count > 0 || r.SeafoodDeaths > 0 || r.SeafoodLossCents > 0)
        {
            Console.WriteLine($"      seafood: units {seafoodSales.Sum(s => s.Units)}, revenue {Money(seafoodSales.Sum(s => s.RevenueCents))}, " +
                              $"deaths {r.SeafoodDeaths}, loss {Money(r.SeafoodLossCents)}");
        }

        var deliSales = r.Sales.Where(s => deli.Contains(s.ProductId)).ToList();
        if (deliSales.Count > 0 || r.DeliLossCents > 0 || r.KitchenRescuedCents > 0)
        {
            Console.WriteLine($"      deli: units {deliSales.Sum(s => s.Units)}, revenue {Money(deliSales.Sum(s => s.RevenueCents))}, " +
                              $"loss {Money(r.DeliLossCents)}, near-expiry cooked {Money(r.KitchenRescuedCents)}");
        }
    }

    /// <summary>The day's date, the festival whose window it is in, its random events and their net cost.</summary>
    public static void PrintCalendar(GameSim sim, DailyReport r)
    {
        if (sim.DateOf(r.Day) is not { } date) return;

        var line = $"      calendar: Y{date.Year} {sim.Data.Calendar!.Seasons[date.SeasonIndex].Id} {date.DayOfSeason}";
        if (r.FestivalId is { } festival) line += $" | {festival} {(r.FestivalDayOffset == 0 ? "DAY" : r.FestivalDayOffset!.Value.ToString("+0;-0", CultureInfo.InvariantCulture))}";
        if (r.EventIds is { Count: > 0 } events) line += $" | events: {string.Join(", ", events)}";
        if (r.EventCostCents != 0) line += $" | event cost {Money(r.EventCostCents)}";
        if (r.FestivalId is not null || r.EventIds is not null) Console.WriteLine(line);
    }

    /// <summary>A season's summary: money, waste and each festival period's daily revenue against the other days.</summary>
    public static void PrintQuarter(GameSim sim, QuarterReport q)
    {
        var season = sim.Data.Calendar!.Seasons[q.SeasonIndex].Id;
        Console.WriteLine($"  === Y{q.Year} {season} (days {q.FirstDay}-{q.LastDay}): revenue {Money(q.RevenueCents)}, profit {Money(q.ProfitCents)}, " +
                          $"waste {Money(q.LossCents)}, customers {q.Customers}, other days avg revenue {Money(q.OtherDaysAverageRevenueCents)}/day");
        foreach (var f in q.Festivals)
        {
            Console.WriteLine($"      {f.FestivalId}: {f.Days} days, avg revenue {Money(f.AverageDailyRevenueCents)}/day, festival goods {f.GoodsUnits} units / {Money(f.GoodsRevenueCents)}");
        }
    }

    public static string Money(long cents)
    {
        var sign = cents < 0 ? "-" : "";
        var abs = Math.Abs(cents);
        return string.Create(CultureInfo.InvariantCulture, $"{sign}{abs / 100}.{abs % 100:00}");
    }
}
