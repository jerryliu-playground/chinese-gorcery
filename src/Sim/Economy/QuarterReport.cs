namespace ChineseGrocery.Sim.Economy;

/// <summary>
/// Summary of one season (days <paramref name="FirstDay"/>-<paramref name="LastDay"/>): money, waste, and how the
/// festival periods in it sold against the other days.
/// </summary>
/// <param name="OtherDaysAverageRevenueCents">Average revenue of the season's days outside any festival window.</param>
public sealed record QuarterReport(
    int Year,
    int SeasonIndex,
    int FirstDay,
    int LastDay,
    long RevenueCents,
    long ProfitCents,
    long LossCents,
    int Customers,
    long OtherDaysAverageRevenueCents,
    IReadOnlyList<FestivalSales> Festivals);
