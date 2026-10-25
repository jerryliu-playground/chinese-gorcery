using System.Text.Json.Serialization;
using ChineseGrocery.Sim.Reputation;

namespace ChineseGrocery.Sim.Data;

/// <summary>Root of reputation.json: the store's reputation, what moves it and what it changes (spec 16).</summary>
/// <param name="StartReputation">Reputation (0-100) of a new game.</param>
/// <param name="StarMinReputation">Five rising thresholds: the reputation needed for 1, 2, 3, 4 and 5 stars (the first is 0).</param>
/// <param name="DailyPullPermille">At closing, how far the day's average satisfaction pulls reputation toward itself.</param>
/// <param name="ArrivalPoints">Arrival-rate multiplier (permille) by reputation; 1000 at the start keeps day 1 as before.</param>
/// <param name="ApplicantQualityPoints">
/// By reputation: how far (permille of each range) the lower end of applicants' skill and speed rolls is raised.
/// </param>
public sealed record ReputationDef(
    int StartReputation,
    IReadOnlyList<int> StarMinReputation,
    int DailyPullPermille,
    IReadOnlyList<ReputationPoint> ArrivalPoints,
    IReadOnlyList<ReputationPoint> ApplicantQualityPoints,
    SatisfactionDef Satisfaction,
    TrendSettingsDef Trends,
    BilingualLabelsDef BilingualLabels,
    GroupOrderSettingsDef GroupOrders,
    ReviewSettingsDef Reviews,
    [property: JsonPropertyName("_tbd")] bool Tbd = false);

/// <summary>A point on a reputation curve: at <paramref name="Reputation"/>, the value is <paramref name="Permille"/>.</summary>
public sealed record ReputationPoint(int Reputation, int Permille);

/// <summary>
/// A leaving customer's satisfaction (0-100): the base, plus or minus what happened to them. Negative values are
/// penalties.
/// </summary>
/// <param name="LongWaitPermilleOfPatience">Waiting in a line longer than this share of the type's patience is a long wait.</param>
public sealed record SatisfactionDef(
    int Base,
    int PerItemBought,
    int MaxItemBonus,
    int PerNotFound,
    int PerTooExpensive,
    int GaveUp,
    int LongWait,
    int LongWaitPermilleOfPatience,
    int PerHeart,
    int TrendFound,
    int TrendMissed,
    int LabelsOwned,
    int LabelsConfused);

/// <summary>Trending products, picked at the start of every season for that season.</summary>
/// <param name="Departments">Departments the trend is picked from.</param>
/// <param name="Count">How many products trend at once.</param>
/// <param name="SeekPermille">Chance that a trend-seeking customer comes in looking for one of them.</param>
/// <param name="HeartPermille">Chance of a heart when they find it.</param>
/// <param name="WordOfMouthArrivals">Extra customers of the same type a heart brings in, later the same day.</param>
/// <param name="WordOfMouthDelayMinutes">How long after the heart they come in.</param>
public sealed record TrendSettingsDef(
    IReadOnlyList<Department> Departments,
    IntRange Count,
    int SeekPermille,
    int HeartPermille,
    int WordOfMouthArrivals,
    int WordOfMouthDelayMinutes);

/// <summary>The one-off "bilingual price labels" store upgrade and what it does for customers who want English labels.</summary>
/// <param name="BuyChanceBonusPermille">With labels: buy chance raised by this share (capped at certain).</param>
/// <param name="ExtraListItems">With labels: items added to their shopping list.</param>
/// <param name="ConfusedPermille">Without labels: chance, per item they look at, of a question mark (once per customer).</param>
public sealed record BilingualLabelsDef(
    long CostCents,
    int UnlockStage,
    int BuyChanceBonusPermille,
    int ExtraListItems,
    int ConfusedPermille);

/// <summary>Restaurant group orders: a buyer asks for goods by a deadline and pays a wholesale price.</summary>
/// <param name="OfferPermille">Chance that a paying group-order customer offers one (if none is waiting or too many are running).</param>
/// <param name="MaxActive">Accepted orders that can run at once.</param>
/// <param name="DeadlineDays">Days given: due at the closing of the accepting day plus this many days.</param>
/// <param name="WholesalePermille">Payment per unit as a share of the suggested retail price.</param>
/// <param name="Lines">How many different products an order asks for.</param>
/// <param name="Candidates">Products an order can ask for, with how many units.</param>
public sealed record GroupOrderSettingsDef(
    int OfferPermille,
    int MaxActive,
    IntRange DeadlineDays,
    int WholesalePermille,
    IntRange Lines,
    IReadOnlyList<GroupOrderCandidateDef> Candidates,
    int DeliveredReputation,
    int FailedReputation);

public sealed record GroupOrderCandidateDef(string ProductId, IntRange Units);

/// <summary>Short customer reviews written at closing from what really happened that day.</summary>
/// <param name="PerDay">How many reviews a day (fewer if fewer things happened).</param>
/// <param name="Keep">How many recent reviews are kept.</param>
/// <param name="LongQueueMinutes">A paying customer who waited this long in a line makes a "long queue" review possible.</param>
/// <param name="QuickCheckoutMinCustomers">Paying customers needed (with no long waits) for a "quick checkout" review.</param>
/// <param name="Templates">Review texts (translation key review.&lt;id&gt;.text) by the fact they report and their stars.</param>
public sealed record ReviewSettingsDef(
    IntRange PerDay,
    int Keep,
    int LongQueueMinutes,
    int QuickCheckoutMinCustomers,
    IReadOnlyList<ReviewTemplateDef> Templates);

public sealed record ReviewTemplateDef(string Id, ReviewFact Fact, int Stars);
