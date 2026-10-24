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

    public static void PrintRow(DailyReport r)
    {
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, Format,
            r.Day, Money(r.CashStartCents), Money(r.RevenueCents), Money(r.PurchaseCostCents), Money(r.WagesCents),
            Money(r.FixtureSpendCents), Money(r.LossCents), Money(r.ProfitCents), Money(r.CashEndCents), r.Customers, r.AngryLeaves,
            r.ItemsNotFound, r.ItemsTooExpensive));

        if (r.Sales.Count > 0)
        {
            Console.WriteLine("      sold: " + string.Join(", ", r.Sales.Select(s => $"{s.ProductId} {s.Units}")));
        }
    }

    public static string Money(long cents)
    {
        var sign = cents < 0 ? "-" : "";
        var abs = Math.Abs(cents);
        return string.Create(CultureInfo.InvariantCulture, $"{sign}{abs / 100}.{abs % 100:00}");
    }
}
