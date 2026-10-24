using System.Globalization;
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

    public static void PrintRow(DailyReport r, ISet<string> produce, ISet<string> seafood)
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
    }

    public static string Money(long cents)
    {
        var sign = cents < 0 ? "-" : "";
        var abs = Math.Abs(cents);
        return string.Create(CultureInfo.InvariantCulture, $"{sign}{abs / 100}.{abs % 100:00}");
    }
}
