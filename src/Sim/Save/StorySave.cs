using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Save;

/// <summary>Story, tutorial and loan progress. Missing in saves from before M15 (those load as a game past the tutorial).</summary>
public sealed record StorySave(
    RngState Rng,
    IReadOnlyList<string> Fired,
    IReadOnlyList<string> Pending,
    IReadOnlyDictionary<string, int> LastFiredDay,
    IReadOnlyDictionary<string, string> Choices,
    int? LastOptionalDay,
    IReadOnlyList<int> TutorialDone,
    int? TutorialActive,
    bool GoodsArrived,
    string? ShopName,
    bool OptionalEnabled,
    long? LoanBalanceCents,
    long LoanArrearsCents);
