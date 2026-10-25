using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Reputation;

namespace ChineseGrocery.Sim.Save;

/// <summary>Reputation, trends, labels, the day's tallies and facts, reviews and group orders.</summary>
public sealed record ReputationSave(
    RngState Rng,
    int ReputationMilli,
    bool HasBilingualLabels,
    int TrendSeason,
    IReadOnlyList<string> Trends,
    IReadOnlyList<WordOfMouthArrival> WordOfMouth,
    long SatisfactionSum,
    int SatisfiedCustomers,
    int PaidCustomersToday,
    IReadOnlyList<DayFact> Facts,
    IReadOnlyList<Review> Reviews,
    IReadOnlyList<ReputationOfDay> History,
    IReadOnlyList<GroupOrder> GroupOrders,
    int NextGroupOrderId);
