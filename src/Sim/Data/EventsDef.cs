using System.Text.Json.Serialization;

namespace ChineseGrocery.Sim.Data;

/// <summary>Root of events.json. Each morning from <paramref name="FirstDay"/> on, one event may happen.</summary>
/// <param name="DailyChancePermille">Chance that something happens on a given morning.</param>
/// <param name="MaxNegativeAtOnce">Negative events announced or running at the same time, at most.</param>
public sealed record EventsDef(
    int FirstDay,
    int DailyChancePermille,
    int MaxNegativeAtOnce,
    IReadOnlyList<EventDef> Events,
    [property: JsonPropertyName("_tbd")] bool Tbd = false);

public enum EventKind
{
    SupplierStockout,
    CostRise,
    Inspection,
    PowerCut,
    DayModifier,
    ClearanceOffer,
    GroupOrder,

    /// <summary>Ships at sea arrive late (spec 17.2): every import shipment is pushed back.</summary>
    ShipDelay,
}

/// <summary>Row of events.json. The fields after <paramref name="Icon"/> apply to some kinds only.</summary>
/// <param name="Weight">Relative chance among the events that can happen that morning.</param>
/// <param name="Icon">Sprite shown with the notice.</param>
/// <param name="Seasons">If set, only in these seasons.</param>
/// <param name="LeadDays">Days between the notice and the event starting (warning enough to prepare).</param>
/// <param name="DurationDays">Days the event lasts (one if not set).</param>
/// <param name="CustomersPermille">Arrivals while it runs.</param>
/// <param name="Demand">Demand changes while it runs.</param>
/// <param name="CostPermille">Cost rise: wholesale prices of the department while it runs.</param>
public sealed record EventDef(
    string Id,
    string NameKey,
    EventKind Kind,
    bool Negative,
    int Weight,
    string Icon,
    int MinStage = 1,
    IReadOnlyList<string>? Seasons = null,
    int LeadDays = 0,
    IntRange? DurationDays = null,
    int CustomersPermille = 1000,
    IReadOnlyList<DemandFactorDef>? Demand = null,
    int CostPermille = 1000,
    InspectionDef? Inspection = null,
    PowerCutDef? PowerCut = null,
    ClearanceOfferDef? Offer = null,
    GroupOrderDef? GroupOrder = null,
    IntRange? DelayDays = null);

/// <summary>The inspector arrives, looks round for a while, then passes or fines the store.</summary>
/// <param name="MinWaterQuality">Every tank's water must be at least this clean.</param>
/// <param name="MinTidiness">Every produce stand must be at least this tidy.</param>
/// <param name="FinePerProblemCents">Fine for each of the three checks failed.</param>
/// <param name="ReputationReward">With reputation data: points for passing, given instead of <paramref name="RewardCents"/>.</param>
/// <param name="ReputationPerProblem">With reputation data: points lost for each check failed, on top of the fine.</param>
public sealed record InspectionDef(
    int ArriveMinuteOfDay, int StayMinutes, int MinWaterQuality, int MinTidiness, long RewardCents, long FinePerProblemCents,
    int ReputationReward = 0, int ReputationPerProblem = 0);

/// <summary>
/// Power is out between the two times: chilled goods lose <paramref name="AgeDays"/> of shelf life when it goes and
/// tank water fouls faster. A rented generator avoids both.
/// </summary>
public sealed record PowerCutDef(int StartMinuteOfDay, int EndMinuteOfDay, int AgeDays, int WaterDropPermille, long GeneratorCostCents);

/// <summary>A supplier offers near-expiry stock of one product cheap; it can be taken before opening only.</summary>
public sealed record ClearanceOfferDef(IntRange Units, int CostPermille, int DaysLeft);

/// <summary>A regular asks for goods to collect tomorrow, paying above the suggested price.</summary>
public sealed record GroupOrderDef(IntRange Products, IntRange Units, int RewardPermille, int PickupMinuteOfDay);
