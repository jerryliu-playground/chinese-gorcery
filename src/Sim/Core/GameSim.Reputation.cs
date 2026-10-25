using ChineseGrocery.Sim.Customers;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Reputation;
using ChineseGrocery.Sim.Save;

namespace ChineseGrocery.Sim.Core;

/// <summary>
/// The store's reputation (spec 16): every leaving customer gets a satisfaction score from what happened to them, and
/// at closing the day's average pulls reputation toward itself. Reputation scales arrivals, unlocks customer types by
/// stars and makes applicants a little better. Also: this season's trending products, the bilingual price labels and
/// the daily reviews. Randomness here comes from its own seeded stream, so it never shifts the customers' rolls.
/// Other systems move reputation through <see cref="NudgeReputation"/>.
/// </summary>
public sealed partial class GameSim
{
    private const ulong ReputationStreamSalt = 0x7E9A_7105_ABC5_57A2;

    private const int Milli = 1000;
    private const int MaxReputation = 100;

    private readonly List<string> _trends = [];
    private readonly List<WordOfMouthArrival> _wordOfMouth = [];
    private readonly List<DayFact> _facts = [];
    private readonly List<Review> _reviews = [];
    private readonly List<ReputationOfDay> _reputationHistory = [];
    private SimRng _reputationRng = null!;
    private int _reputationMilli;
    private int _trendSeason = -1;
    private long _satisfactionSum;
    private int _satisfiedCustomers;
    private int _paidCustomersToday;

    /// <summary>Store reputation, 0-100.</summary>
    public int Reputation => _reputationMilli / Milli;

    /// <summary>Reputation as 1-5 stars (thresholds in reputation.json).</summary>
    public int Stars => Data.Reputation is { } r ? Math.Max(1, r.StarMinReputation.Count(min => Reputation >= min)) : 1;

    public bool HasBilingualLabels { get; private set; }

    /// <summary>This season's trending products.</summary>
    public IReadOnlyList<string> TrendingProducts => _trends;

    /// <summary>Recent reviews, oldest first.</summary>
    public IReadOnlyList<Review> Reviews => _reviews;

    /// <summary>Reputation at each closing so far.</summary>
    public IReadOnlyList<ReputationOfDay> ReputationHistory => _reputationHistory;

    /// <summary>What has happened today that reviews can be written about.</summary>
    public IReadOnlyList<DayFact> TodaysFacts => _facts;

    /// <summary>The season a day falls in, counted from the first (0); trending products change with it.</summary>
    public int SeasonOf(int day) => SeasonsSinceStart(day);

    /// <summary>
    /// Moves reputation by whole points (negative lowers it), clamped to 0-100. The entry point for other systems:
    /// hygiene inspections, festival decorations, event choices, competition. <paramref name="reasonKey"/> is a
    /// translation key shown with the change.
    /// </summary>
    public void NudgeReputation(int points, string reasonKey) => ShiftReputation(points * Milli, reasonKey);

    private void ShiftReputation(long deltaMilli, string reasonKey)
    {
        if (Data.Reputation is null) return;

        var before = Reputation;
        _reputationMilli = (int)Math.Clamp(_reputationMilli + deltaMilli, 0, MaxReputation * Milli);
        _events.Add(new ReputationChanged(before, Reputation, reasonKey));
    }

    private void StartReputation(ulong seed)
    {
        _reputationRng = new SimRng(seed ^ ReputationStreamSalt);
        _reputationMilli = Math.Clamp(Data.Reputation?.StartReputation ?? 0, 0, MaxReputation) * Milli;
        RefreshTrends(announce: false);
    }

    /// <summary>Arrival-rate multiplier (permille) at the current reputation.</summary>
    public int ArrivalPermille() => Data.Reputation is { } r ? Curve(r.ArrivalPoints) : 1000;

    private int Curve(IReadOnlyList<ReputationPoint> points) =>
        Interpolation.Linear(points.Select(p => (p.Reputation, p.Permille)).ToList(), Reputation);

    /// <summary>
    /// How likely a type is to be the next customer now: nothing until the store has its stars and departments, then
    /// its weight plus its per-star step for every star above that.
    /// </summary>
    public int ArrivalWeightNow(CustomerTypeDef type)
    {
        if (type.ArrivalWeight == 0 || Stars < type.MinStars) return 0;
        if (type.RequiresDepartments is { } needs && !needs.All(HasDepartment)) return 0;

        return Math.Max(0, type.ArrivalWeight + type.ArrivalWeightPerStar * (Stars - type.MinStars));
    }

    /// <summary>Whether a placed fixture sells this department.</summary>
    public bool HasDepartment(Department department) => Layout.Fixtures.Any(f => Sells(_fixtures[f.FixtureId].Kind, department));

    private static bool Sells(FixtureKind kind, Department department) => department switch
    {
        Department.Meat => kind == FixtureKind.MeatDisplay,
        Department.Seafood => kind is FixtureKind.FishTank or FixtureKind.ShellfishTank or FixtureKind.IceTable,
        Department.Produce => kind == FixtureKind.ProduceStand,
        Department.Deli => DataValidator.IsDeliStation(kind),
        Department.Frozen => kind == FixtureKind.Freezer,
        _ => kind == FixtureKind.Shelf,
    };

    /// <summary>An applicant roll's lower end, raised by a good reputation.</summary>
    private int ApplicantFloor(IntRange range) =>
        range.Min + (Data.Reputation is { } r ? (range.Max - range.Min) * Curve(r.ApplicantQualityPoints) / 1000 : 0);

    /// <summary>With bilingual labels, customers who want them come with a longer list.</summary>
    private CustomerTypeDef WithLabels(CustomerTypeDef type) =>
        HasBilingualLabels && type.WantsEnglishLabels && Data.Reputation is { } r
            ? type with { ShoppingListSize = new IntRange(type.ShoppingListSize.Min + r.BilingualLabels.ExtraListItems, type.ShoppingListSize.Max + r.BilingualLabels.ExtraListItems) }
            : type;

    /// <summary>A trend seeker usually comes in for one of this season's trending products first.</summary>
    private void SeekTrend(Customer customer, CustomerTypeDef type)
    {
        if (!type.SeeksTrends || _trends.Count == 0 || Data.Reputation is not { } r) return;
        if (_reputationRng.NextInt(0, 1000) >= r.Trends.SeekPermille) return;

        var product = _trends[_trends.Count == 1 ? 0 : _reputationRng.NextInt(0, _trends.Count)];
        customer.SeekingTrend = product;
        customer.PutFirstOnList(product);
    }

    /// <summary>Someone who wants English labels, in a store without them, may be puzzled (a question mark, once).</summary>
    private void MaybePuzzle(Customer customer, CustomerTypeDef type)
    {
        if (!type.WantsEnglishLabels || HasBilingualLabels || customer.Confused || Data.Reputation is not { } r) return;
        if (_reputationRng.NextInt(0, 1000) >= r.BilingualLabels.ConfusedPermille) return;

        customer.Confused = true;
        _events.Add(new CustomerReaction(customer.Id, CustomerReactionKind.Confused));
        NoteFact(ReviewFact.LabelsConfused, null, type.Id);
    }

    /// <summary>With labels, customers who want them are more willing to buy.</summary>
    private int ApplyLabels(int chancePermille, CustomerTypeDef type) =>
        HasBilingualLabels && type.WantsEnglishLabels && Data.Reputation is { } r
            ? (int)Math.Min(1000, chancePermille * (1000L + r.BilingualLabels.BuyChanceBonusPermille) / 1000)
            : chancePermille;

    /// <summary>A shopper put an item in the basket: finding the trend they came for may bring a heart and a friend later.</summary>
    private void NoteTaken(Customer customer, CustomerTypeDef type, string productId)
    {
        if (productId != customer.SeekingTrend || customer.TrendFound is not null || Data.Reputation is not { } r) return;

        customer.TrendFound = true;
        NoteFact(ReviewFact.TrendFound, productId, type.Id);
        if (_reputationRng.NextInt(0, 1000) >= r.Trends.HeartPermille) return;

        customer.Hearts++;
        _events.Add(new CustomerReaction(customer.Id, CustomerReactionKind.Heart, productId));
        for (var i = 0; i < r.Trends.WordOfMouthArrivals; i++)
        {
            _wordOfMouth.Add(new WordOfMouthArrival(Clock.MinuteOfDay + r.Trends.WordOfMouthDelayMinutes, type.Id));
        }
    }

    /// <summary>Word of mouth: friends of happy trend seekers come in a while later (if the doors are still open).</summary>
    private void SpawnWordOfMouth()
    {
        foreach (var due in _wordOfMouth.Where(w => w.MinuteOfDay <= Clock.MinuteOfDay).ToList())
        {
            _wordOfMouth.Remove(due);
            SpawnCustomer(Data.CustomerTypes.Single(t => t.Id == due.CustomerTypeId), null);
        }
    }

    private void NoteNotFoundFacts(Customer customer, string productId)
    {
        NoteFact(ReviewFact.NotFound, productId, customer.TypeId);
        if (productId != customer.SeekingTrend || customer.TrendFound is not null) return;

        customer.TrendFound = false;
        NoteFact(ReviewFact.TrendMissed, productId, customer.TypeId);
    }

    /// <summary>What was paid for: fresh produce, meat and seafood, and deli dishes are worth a review.</summary>
    private void NoteCheckoutFacts(Customer customer, IReadOnlyList<BasketItem> items)
    {
        customer.ItemsBought += items.Sum(i => i.Lot.Quantity);
        foreach (var lot in items.Select(i => i.Lot))
        {
            var product = _products[lot.ProductId];
            if (product.Department == Department.Deli) NoteFact(ReviewFact.DeliTreat, lot.ProductId, customer.TypeId);
            else if (IsFresh(product, lot)) NoteFact(ReviewFact.FreshBuy, lot.ProductId, customer.TypeId);
        }
    }

    /// <summary>Produce in its fresh tier, meat and seafood before their last day, and anything sold live.</summary>
    private bool IsFresh(ProductDef product, StockLot lot) => product.Department switch
    {
        Department.Produce => Produce.Freshness.TierOf(product, lot, Clock.Day) == FreshnessTier.Fresh,
        Department.Meat or Department.Seafood => product.Live is not null || lot.BestBeforeDay > Clock.Day,
        _ => false,
    };

    /// <summary>A customer heads out: their satisfaction is scored once, from everything that happened to them.</summary>
    private void ScoreCustomer(Customer customer, CustomerExit exit)
    {
        if (Data.Reputation is not { } r || customer.Satisfaction is not null) return;

        var s = r.Satisfaction;
        var type = Data.CustomerTypes.Single(t => t.Id == customer.TypeId);
        var longWait = customer.LongestWaitSeconds * 1000L > type.PatienceMinutes * 60L * s.LongWaitPermilleOfPatience;
        var score = s.Base
                    + Math.Min(s.MaxItemBonus, customer.ItemsBought * s.PerItemBought)
                    + customer.ItemsNotFound * s.PerNotFound
                    + customer.ItemsTooExpensive * s.PerTooExpensive
                    + customer.Hearts * s.PerHeart
                    + (exit == CustomerExit.Angry ? s.GaveUp : 0)
                    + (longWait ? s.LongWait : 0)
                    + (customer.TrendFound switch { true => s.TrendFound, false => s.TrendMissed, null => 0 })
                    + (type.WantsEnglishLabels ? HasBilingualLabels ? s.LabelsOwned : customer.Confused ? s.LabelsConfused : 0 : 0);
        score = Math.Clamp(score, 0, 100);

        customer.Satisfaction = score;
        _satisfactionSum += score;
        _satisfiedCustomers++;
        _events.Add(new CustomerSatisfied(customer.Id, type.Id, score, customer.LongestWaitSeconds));

        if (exit == CustomerExit.Angry) NoteFact(ReviewFact.GaveUp, null, type.Id);
        if (exit != CustomerExit.Paid) return;

        _paidCustomersToday++;
        if (customer.LongestWaitSeconds >= r.Reviews.LongQueueMinutes * 60) NoteFact(ReviewFact.LongQueue, null, type.Id);
        if (type.WantsEnglishLabels && HasBilingualLabels) NoteFact(ReviewFact.LabelsLoved, null, type.Id);
        if (type.OffersGroupOrders) MaybeOfferGroupOrder(type);
    }

    private void NoteFact(ReviewFact fact, string? productId, string? customerTypeId)
    {
        if (Data.Reputation is null) return;

        var i = _facts.FindIndex(f => f.Fact == fact && f.ProductId == productId);
        if (i < 0) _facts.Add(new DayFact(fact, productId, 1, customerTypeId));
        else _facts[i] = _facts[i] with { Count = _facts[i].Count + 1, CustomerTypeId = customerTypeId ?? _facts[i].CustomerTypeId };
    }

    /// <summary>A new season brings new trending products: 1-2 snacks or drinks the store can get now.</summary>
    private void RefreshTrends(bool announce = true)
    {
        if (Data.Reputation is not { } r || SeasonOf(Clock.Day) == _trendSeason) return;

        _trendSeason = SeasonOf(Clock.Day);
        _trends.Clear();
        var pool = Data.Products
            .Where(p => r.Trends.Departments.Contains(p.Department) && CanGet(p))
            .Select(p => p.Id)
            .Order(StringComparer.Ordinal)
            .ToList();
        var count = Math.Min(pool.Count, _reputationRng.NextInt(r.Trends.Count.Min, r.Trends.Count.Max + 1));
        for (var i = 0; i < count; i++)
        {
            var pick = _reputationRng.NextInt(0, pool.Count);
            _trends.Add(pool[pick]);
            pool.RemoveAt(pick);
        }

        if (announce) _events.Add(new TrendsPicked(_trendSeason, _trends.ToList()));
    }

    /// <summary>Whether the store can buy the product now: from the wholesaler, or by import once importing is unlocked.</summary>
    private bool CanGet(ProductDef product) =>
        product.Orderable || (product.Import is not null && Data.Economy.Imports is { } imports && Economy.Stage >= imports.UnlockStage);

    private Rejection? HandleBuyBilingualLabels()
    {
        if (Data.Reputation is not { } r || Economy.Stage < r.BilingualLabels.UnlockStage) return Reject(RejectReason.FixtureLocked);
        if (HasBilingualLabels) return Reject(RejectReason.AlreadyOwned);
        if (!Economy.CanAfford(r.BilingualLabels.CostCents)) return Reject(RejectReason.InsufficientCash);

        Economy.Spend(ExpenseCategory.Fixtures, r.BilingualLabels.CostCents);
        HasBilingualLabels = true;
        _events.Add(new BilingualLabelsBought(r.BilingualLabels.CostCents));
        return null;
    }

    /// <summary>Morning: maybe a new season's trends; yesterday's unanswered offers lapse; new deliveries are set aside for orders.</summary>
    private void StartReputationDay()
    {
        RefreshTrends();
        ExpireGroupOrderOffers();
        ReserveForGroupOrders();
    }

    /// <summary>
    /// Closing: group orders are delivered or cancelled, the day's average satisfaction pulls reputation, reviews are
    /// written from what happened, and the day's tallies start over.
    /// </summary>
    private void CloseReputationDay()
    {
        if (Data.Reputation is not { } r) return;

        CloseGroupOrders();
        if (_paidCustomersToday >= r.Reviews.QuickCheckoutMinCustomers && _facts.All(f => f.Fact is not (ReviewFact.LongQueue or ReviewFact.GaveUp)))
        {
            NoteFact(ReviewFact.QuickCheckout, null, null);
        }

        if (_satisfiedCustomers > 0)
        {
            var averageMilli = _satisfactionSum * Milli / _satisfiedCustomers;
            ShiftReputation((averageMilli - _reputationMilli) * r.DailyPullPermille / 1000, "reputation.reason.customers");
        }

        PostReviews(r.Reviews);
        _reputationHistory.Add(new ReputationOfDay(Clock.Day, Reputation));

        _facts.Clear();
        _wordOfMouth.Clear();
        _satisfactionSum = 0;
        _satisfiedCustomers = 0;
        _paidCustomersToday = 0;
    }

    /// <summary>
    /// A few reviews, each about a different thing that really happened today (the product it happened to most),
    /// alternating good and bad where both happened, so players see what went well and what to fix.
    /// </summary>
    private void PostReviews(ReviewSettingsDef settings)
    {
        bool IsGood(ReviewFact fact) => settings.Templates.Where(t => t.Fact == fact).Average(t => t.Stars) >= 3.5;

        var pool = _facts.Select(f => f.Fact).Distinct().Where(f => settings.Templates.Any(t => t.Fact == f)).Order().ToList();
        if (pool.Count == 0) return;

        var wanted = Math.Min(pool.Count, _reputationRng.NextInt(settings.PerDay.Min, settings.PerDay.Max + 1));
        var posted = new List<Review>();
        bool? lastGood = null;
        while (posted.Count < wanted)
        {
            var candidates = pool.Where(f => lastGood is null || IsGood(f) != lastGood).ToList();
            if (candidates.Count == 0) candidates = pool;

            var fact = candidates[_reputationRng.NextInt(0, candidates.Count)];
            pool.Remove(fact);
            lastGood = IsGood(fact);

            var about = _facts.Where(f => f.Fact == fact).OrderByDescending(f => f.Count).ThenBy(f => f.ProductId, StringComparer.Ordinal).First();
            var templates = settings.Templates.Where(t => t.Fact == fact).ToList();
            var template = templates[templates.Count == 1 ? 0 : _reputationRng.NextInt(0, templates.Count)];
            posted.Add(new Review(Clock.Day, template.Id, fact, template.Stars, about.ProductId, about.CustomerTypeId));
        }

        _reviews.AddRange(posted);
        if (_reviews.Count > settings.Keep) _reviews.RemoveRange(0, _reviews.Count - settings.Keep);
        _events.Add(new ReviewsPosted(Clock.Day, posted));
    }

    private ReputationSave ReputationSave() => new(
        _reputationRng.State, _reputationMilli, HasBilingualLabels, _trendSeason, _trends.ToList(), _wordOfMouth.ToList(),
        _satisfactionSum, _satisfiedCustomers, _paidCustomersToday, _facts.ToList(), _reviews.ToList(), _reputationHistory.ToList(),
        _groupOrders.ToList(), _nextGroupOrderId);

    private void RestoreReputation(ReputationSave? save, ulong fallbackSeed)
    {
        if (save is null)
        {
            StartReputation(fallbackSeed);
            return;
        }

        _reputationRng = SimRng.FromState(save.Rng);
        _reputationMilli = save.ReputationMilli;
        HasBilingualLabels = save.HasBilingualLabels;
        _trendSeason = save.TrendSeason;
        _trends.AddRange(save.Trends);
        _wordOfMouth.AddRange(save.WordOfMouth);
        _satisfactionSum = save.SatisfactionSum;
        _satisfiedCustomers = save.SatisfiedCustomers;
        _paidCustomersToday = save.PaidCustomersToday;
        _facts.AddRange(save.Facts);
        _reviews.AddRange(save.Reviews);
        _reputationHistory.AddRange(save.History);
        _groupOrders.AddRange(save.GroupOrders);
        _nextGroupOrderId = save.NextGroupOrderId;
    }
}
