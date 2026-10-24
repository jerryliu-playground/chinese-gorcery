using System.Text.RegularExpressions;
using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Grid;

namespace ChineseGrocery.Sim.Data;

/// <summary>Cross-file consistency checks on loaded data. Reports every problem found, not just the first.</summary>
public static partial class DataValidator
{
    private const int MinutesPerDay = 24 * 60;
    private const int PermilleWhole = 1000;

    [GeneratedRegex("^[a-z0-9]+(_[a-z0-9]+)*$")]
    private static partial Regex IdPattern();

    public static void ThrowIfInvalid(GameData data)
    {
        var errors = Validate(data);
        if (errors.Count > 0)
        {
            throw new DataValidationException(errors);
        }
    }

    public static IReadOnlyList<string> Validate(GameData data)
    {
        var errors = new List<string>();
        var stageIds = data.Stages.Select(s => s.Id).ToHashSet();
        var products = data.Products.GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());

        ValidateProducts(data.Products, errors);
        ValidateFixtures(data.Fixtures, stageIds, errors);
        ValidateStorageCoverage(data.Products, data.Fixtures, errors);
        ValidateCustomerTypes(data.CustomerTypes, errors);
        ValidateEmployees(data.Employees, stageIds, errors);
        ValidateMeatCuts(data.MeatCuts, products, errors);
        ValidateStages(data.Stages, errors);
        ValidateEconomy(data.Economy, errors);
        ValidateStartingLayout(data, errors);

        return errors;
    }

    private static void ValidateProducts(IReadOnlyList<ProductDef> products, List<string> errors)
    {
        const string file = DataFiles.Products;
        CheckIds(file, products.Select(p => p.Id).ToList(), errors);

        for (var i = 0; i < products.Count; i++)
        {
            var p = products[i];
            var at = $"{file}: [{i}] '{p.Id}'";
            CheckNameKey(at, p.NameKey, "product", p.Id, errors);
            if (p.CostCents < 0) errors.Add($"{at}: costCents must be >= 0");
            if (p.SuggestedPriceCents <= 0) errors.Add($"{at}: suggestedPriceCents must be > 0");
            if (p.ShelfLifeDays <= 0) errors.Add($"{at}: shelfLifeDays must be > 0");
            if (p.UnitsPerShelfSlot <= 0) errors.Add($"{at}: unitsPerShelfSlot must be > 0");
            if (p.UnitsPerStorageSlot <= 0) errors.Add($"{at}: unitsPerStorageSlot must be > 0");
            if (p.Orderable && p.CostCents <= 0) errors.Add($"{at}: orderable products need costCents > 0");
        }
    }

    private static void ValidateFixtures(IReadOnlyList<FixtureDef> fixtures, HashSet<int> stageIds, List<string> errors)
    {
        const string file = DataFiles.Fixtures;
        CheckIds(file, fixtures.Select(f => f.Id).ToList(), errors);

        for (var i = 0; i < fixtures.Count; i++)
        {
            var f = fixtures[i];
            var at = $"{file}: [{i}] '{f.Id}'";
            CheckNameKey(at, f.NameKey, "fixture", f.Id, errors);
            if (f.Size.W <= 0 || f.Size.H <= 0) errors.Add($"{at}: size must be positive");
            if (f.Capacity < 0) errors.Add($"{at}: capacity must be >= 0");
            if (f.PriceCents < 0) errors.Add($"{at}: priceCents must be >= 0");
            if (!stageIds.Contains(f.UnlockStage)) errors.Add($"{at}: unlockStage {f.UnlockStage} does not exist in {DataFiles.Stages}");
            if (f.Storage.Distinct().Count() != f.Storage.Count) errors.Add($"{at}: storage has duplicates");
            if (IsStorageArea(f.Kind) && f.Storage.Count == 0) errors.Add($"{at}: storage areas must list at least one storage type");

            foreach (var cell in f.InteractionCells.Concat(f.CustomerCells))
            {
                if (!IsOrthogonallyAdjacent(cell, f.Size))
                {
                    errors.Add($"{at}: cell ({cell.X},{cell.Y}) must be outside the footprint and touch one of its sides");
                }
            }

            if (f.InteractionCells.Distinct().Count() != f.InteractionCells.Count) errors.Add($"{at}: interactionCells has duplicates");
            if (f.CustomerCells.Distinct().Count() != f.CustomerCells.Count) errors.Add($"{at}: customerCells has duplicates");

            var needsWorkerCell = f.Kind is not FixtureKind.Entrance;
            var needsCustomerCell = f.Kind is FixtureKind.Entrance or FixtureKind.Checkout || IsDisplay(f.Kind);
            if (needsWorkerCell && f.InteractionCells.Count == 0) errors.Add($"{at}: {f.Kind} needs at least one interaction cell");
            if (needsCustomerCell && f.CustomerCells.Count == 0) errors.Add($"{at}: {f.Kind} needs at least one customer cell");
            if ((IsDisplay(f.Kind) || IsStorageArea(f.Kind)) && f.Capacity <= 0) errors.Add($"{at}: {f.Kind} needs capacity > 0");
            if (IsDisplay(f.Kind) && f.Storage.Count == 0) errors.Add($"{at}: displays must list at least one storage type");
        }

        foreach (var kind in new[] { FixtureKind.Shelf, FixtureKind.Checkout, FixtureKind.Backroom, FixtureKind.Entrance })
        {
            if (!fixtures.Any(f => f.Kind == kind && f.UnlockStage == 1))
            {
                errors.Add($"{file}: no fixture of kind '{kind}' is available at stage 1");
            }
        }
    }

    /// <summary>Fixture kinds customers take goods from, organised in single-product slots.</summary>
    public static bool IsDisplay(FixtureKind kind) => kind is FixtureKind.Shelf or FixtureKind.MeatDisplay;

    /// <summary>Fixture kinds that receive deliveries.</summary>
    public static bool IsStorageArea(FixtureKind kind) => kind is FixtureKind.Backroom or FixtureKind.ColdStorage;

    private static void ValidateStorageCoverage(IReadOnlyList<ProductDef> products, IReadOnlyList<FixtureDef> fixtures, List<string> errors)
    {
        var storable = fixtures.Where(f => IsStorageArea(f.Kind)).SelectMany(f => f.Storage).ToHashSet();
        foreach (var p in products.Where(p => p.Orderable && !storable.Contains(p.Storage)))
        {
            errors.Add($"{DataFiles.Products}: '{p.Id}' is orderable but no backroom/cold storage fixture stores '{p.Storage}'");
        }
    }

    private static bool IsOrthogonallyAdjacent(GridPoint cell, GridSize size)
    {
        var insideX = cell.X >= 0 && cell.X < size.W;
        var insideY = cell.Y >= 0 && cell.Y < size.H;
        var besideX = cell.X == -1 || cell.X == size.W;
        var besideY = cell.Y == -1 || cell.Y == size.H;
        return (insideX && besideY) || (insideY && besideX);
    }

    private static void ValidateCustomerTypes(IReadOnlyList<CustomerTypeDef> types, List<string> errors)
    {
        const string file = DataFiles.CustomerTypes;
        CheckIds(file, types.Select(t => t.Id).ToList(), errors);
        if (types.Count == 0) errors.Add($"{file}: at least one customer type is required");
        if (types.Sum(t => t.ArrivalWeight) <= 0) errors.Add($"{file}: arrivalWeight must sum to > 0");

        for (var i = 0; i < types.Count; i++)
        {
            var t = types[i];
            var at = $"{file}: [{i}] '{t.Id}'";
            CheckNameKey(at, t.NameKey, "customer_type", t.Id, errors);
            if (t.Preferences.Any(p => p.Weight < 0)) errors.Add($"{at}: preference weights must be >= 0");
            if (t.Preferences.Sum(p => p.Weight) <= 0) errors.Add($"{at}: preference weights must sum to > 0");
            if (t.Preferences.Select(p => p.Department).Distinct().Count() != t.Preferences.Count) errors.Add($"{at}: preferences list a department more than once");
            if (t.ArrivalWeight < 0) errors.Add($"{at}: arrivalWeight must be >= 0");
            if (t.PriceComfortRatioPermille < 0) errors.Add($"{at}: priceComfortRatioPermille must be >= 0");
            if (t.PriceCeilingRatioPermille <= t.PriceComfortRatioPermille) errors.Add($"{at}: priceCeilingRatioPermille must be > priceComfortRatioPermille");
            CheckPermille(at, "minBuyChancePermille", t.MinBuyChancePermille, errors);
            if (t.MoveCellsPerTick <= 0) errors.Add($"{at}: moveCellsPerTick must be > 0");
            if (t.BrowseMinutesPerItem < 0) errors.Add($"{at}: browseMinutesPerItem must be >= 0");
            if (t.PatienceMinutes <= 0) errors.Add($"{at}: patienceMinutes must be > 0");
            CheckRange(at, "shoppingListSize", t.ShoppingListSize, minAllowed: 1, errors);
        }
    }

    private static void ValidateEmployees(EmployeesDef employees, HashSet<int> stageIds, List<string> errors)
    {
        const string file = DataFiles.Employees;
        var roles = employees.Roles;
        CheckIds(file, roles.Select(r => r.Id).ToList(), errors);

        for (var i = 0; i < roles.Count; i++)
        {
            var r = roles[i];
            var at = $"{file}: roles[{i}] '{r.Id}'";
            CheckNameKey(at, r.NameKey, "role", r.Id, errors);
            if (!stageIds.Contains(r.UnlockStage)) errors.Add($"{at}: unlockStage {r.UnlockStage} does not exist in {DataFiles.Stages}");
            if (r.MaxCount <= 0) errors.Add($"{at}: maxCount must be > 0");
            if (r.TaskTypes.Count == 0) errors.Add($"{at}: taskTypes must not be empty");
            if (r.MarketWageCentsPerDay < 0) errors.Add($"{at}: marketWageCentsPerDay must be >= 0");
            CheckRange(at, "knifeSkill", r.KnifeSkill, minAllowed: 0, errors);
            CheckRange(at, "speedPermille", r.SpeedPermille, minAllowed: 1, errors);
            if (r.XpPerTask <= 0) errors.Add($"{at}: xpPerTask must be > 0");
            if (r.XpPerLevel <= 0) errors.Add($"{at}: xpPerLevel must be > 0");
        }

        var player = employees.Player;
        if (player.MoveCellsPerTick <= 0) errors.Add($"{file}: player: moveCellsPerTick must be > 0");
        if (player.WorkSpeedPermille <= 0) errors.Add($"{file}: player: workSpeedPermille must be > 0");
        if (player.KnifeSkill < 0) errors.Add($"{file}: player: knifeSkill must be >= 0");
    }

    private static void ValidateMeatCuts(IReadOnlyList<MeatCutDef> cuts, Dictionary<string, ProductDef> products, List<string> errors)
    {
        const string file = DataFiles.MeatCuts;
        CheckIds(file, cuts.Select(c => c.PrimalId).ToList(), errors);

        void CheckMeatProduct(string at, string field, string id)
        {
            if (!products.TryGetValue(id, out var p))
            {
                errors.Add($"{at}: {field} '{id}' does not exist in {DataFiles.Products}");
            }
            else if (p.Department != Department.Meat)
            {
                errors.Add($"{at}: {field} '{id}' is not in the meat department");
            }
            else if (p.WeightGrams is not > 0)
            {
                errors.Add($"{at}: {field} '{id}' needs weightGrams > 0");
            }
        }

        for (var i = 0; i < cuts.Count; i++)
        {
            var c = cuts[i];
            var at = $"{file}: [{i}] '{c.PrimalId}'";
            CheckMeatProduct(at, "primalId", c.PrimalId);
            CheckMeatProduct(at, "groundProductId", c.GroundProductId);
            if (c.Cuts.Count == 0) errors.Add($"{at}: cuts must not be empty");

            foreach (var y in c.Cuts)
            {
                CheckMeatProduct(at, "cuts.productId", y.ProductId);
                if (y.Permille <= 0) errors.Add($"{at}: cut '{y.ProductId}' permille must be > 0");
            }

            if (c.Cuts.Select(y => y.ProductId).Distinct().Count() != c.Cuts.Count) errors.Add($"{at}: cuts list a product more than once");
            if (c.GroundPermille < 0) errors.Add($"{at}: groundPermille must be >= 0");
            if (c.Cuts.Sum(y => y.Permille) + c.GroundPermille > PermilleWhole) errors.Add($"{at}: cuts + ground permille exceeds {PermilleWhole}");
            if (c.KnifeSkillCutBonusPermillePerPoint < 0) errors.Add($"{at}: knifeSkillCutBonusPermillePerPoint must be >= 0");
        }
    }

    private static void ValidateStages(IReadOnlyList<StageDef> stages, List<string> errors)
    {
        const string file = DataFiles.Stages;
        if (stages.Count == 0)
        {
            errors.Add($"{file}: at least one stage is required");
            return;
        }

        for (var i = 0; i < stages.Count; i++)
        {
            var s = stages[i];
            var at = $"{file}: [{i}] stage {s.Id}";
            if (s.Id != i + 1) errors.Add($"{at}: stage ids must be 1, 2, 3... in order");
            CheckNameKey(at, s.NameKey, "stage", s.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), errors);
            if (s.StoreSize.W <= 0 || s.StoreSize.H <= 0) errors.Add($"{at}: storeSize must be positive");
            if (s.RequiredCumulativeRevenueCents < 0) errors.Add($"{at}: requiredCumulativeRevenueCents must be >= 0");
            if (s.UpgradeCostCents < 0) errors.Add($"{at}: upgradeCostCents must be >= 0");

            if (i > 0)
            {
                var prev = stages[i - 1];
                if (s.RequiredCumulativeRevenueCents < prev.RequiredCumulativeRevenueCents) errors.Add($"{at}: requiredCumulativeRevenueCents must not decrease");
                if (s.StoreSize.W < prev.StoreSize.W || s.StoreSize.H < prev.StoreSize.H) errors.Add($"{at}: storeSize must not shrink");
            }
        }
    }

    private static void ValidateEconomy(EconomyDef e, List<string> errors)
    {
        const string file = DataFiles.Economy;
        if (e.StartingCashCents < 0) errors.Add($"{file}: startingCashCents must be >= 0");
        if (e.MinutesPerTick <= 0) errors.Add($"{file}: minutesPerTick must be > 0");
        if (e.OpenMinuteOfDay < 0 || e.OpenMinuteOfDay >= e.CloseMinuteOfDay || e.CloseMinuteOfDay > MinutesPerDay)
        {
            errors.Add($"{file}: need 0 <= openMinuteOfDay < closeMinuteOfDay <= {MinutesPerDay}");
        }

        if (e.RealSecondsPerOpenPhaseAt1x <= 0) errors.Add($"{file}: realSecondsPerOpenPhaseAt1x must be > 0");
        if (e.SpeedMultipliers.Count == 0 || e.SpeedMultipliers[0] != 1) errors.Add($"{file}: speedMultipliers must start with 1");
        for (var i = 1; i < e.SpeedMultipliers.Count; i++)
        {
            if (e.SpeedMultipliers[i] <= e.SpeedMultipliers[i - 1]) errors.Add($"{file}: speedMultipliers must be strictly ascending");
        }

        if (e.CustomerArrivals.Count == 0) errors.Add($"{file}: customerArrivals must not be empty");
        else if (e.CustomerArrivals[0].StartMinuteOfDay != e.OpenMinuteOfDay) errors.Add($"{file}: first customerArrivals bucket must start at openMinuteOfDay");
        for (var i = 0; i < e.CustomerArrivals.Count; i++)
        {
            var b = e.CustomerArrivals[i];
            if (b.CustomersPerHour < 0) errors.Add($"{file}: customerArrivals[{i}]: customersPerHour must be >= 0");
            if (b.StartMinuteOfDay < e.OpenMinuteOfDay || b.StartMinuteOfDay >= e.CloseMinuteOfDay) errors.Add($"{file}: customerArrivals[{i}]: must start within opening hours");
            if (i > 0 && b.StartMinuteOfDay <= e.CustomerArrivals[i - 1].StartMinuteOfDay) errors.Add($"{file}: customerArrivals must be strictly ascending by startMinuteOfDay");
        }

        CheckPermille(file, "fixtureSellRefundPermille", e.FixtureSellRefundPermille, errors);

        var t = e.Tasks;
        CheckPermille(file, "tasks.restockBelowPermille", t.RestockBelowPermille, errors);
        foreach (var (name, minutes) in new[]
                 {
                     ("restockPickupMinutes", t.RestockPickupMinutes), ("restockShelveMinutes", t.RestockShelveMinutes),
                     ("checkoutBaseMinutes", t.CheckoutBaseMinutes), ("checkoutMinutesPerItem", t.CheckoutMinutesPerItem),
                     ("cutPickupMinutes", t.CutPickupMinutes), ("cutMeatMinutes", t.CutMeatMinutes),
                 })
        {
            if (minutes < 0) errors.Add($"{file}: tasks.{name} must be >= 0");
        }

        foreach (var type in Enum.GetValues<Tasks.TaskType>())
        {
            var count = t.Priorities.Count(p => p.TaskType == type);
            if (count != 1) errors.Add($"{file}: tasks.priorities must list '{type}' exactly once (found {count})");
        }
    }

    private static void ValidateStartingLayout(GameData data, List<string> errors)
    {
        const string at = DataFiles.Economy + ": startingLayout";
        var fixtures = data.Fixtures.GroupBy(f => f.Id).ToDictionary(g => g.Key, g => g.First());
        var layout = data.Economy.StartingLayout;

        var bad = false;
        foreach (var entry in layout)
        {
            if (!fixtures.TryGetValue(entry.FixtureId, out var def))
            {
                errors.Add($"{at}: fixture '{entry.FixtureId}' does not exist in {DataFiles.Fixtures}");
                bad = true;
            }
            else if (def.UnlockStage != 1)
            {
                errors.Add($"{at}: fixture '{entry.FixtureId}' is not available at stage 1");
            }
        }

        if (bad || data.Stages.Count == 0) return;

        var placed = layout.Select((e, i) => new PlacedFixture(i + 1, e.FixtureId, e.Origin, 0, e.Rotation)).ToList();
        if (LayoutRules.Check(data.Stages[0].StoreSize, placed, id => fixtures[id]) is { } problem)
        {
            var which = problem.InstanceId == 0 ? "" : $" at entry [{problem.InstanceId - 1}] '{placed[problem.InstanceId - 1].FixtureId}'";
            errors.Add($"{at}: invalid layout ({problem.Reason}){which}");
        }
    }

    private static void CheckIds(string file, IReadOnlyList<string> ids, List<string> errors)
    {
        foreach (var id in ids.Where(id => !IdPattern().IsMatch(id)))
        {
            errors.Add($"{file}: id '{id}' must be lower_snake_case");
        }

        foreach (var dup in ids.GroupBy(id => id).Where(g => g.Count() > 1))
        {
            errors.Add($"{file}: duplicate id '{dup.Key}'");
        }
    }

    private static void CheckNameKey(string at, string nameKey, string category, string id, List<string> errors)
    {
        var expected = $"{category}.{id}.name";
        if (nameKey != expected) errors.Add($"{at}: nameKey must be '{expected}' (was '{nameKey}')");
    }

    private static void CheckPermille(string at, string field, int value, List<string> errors)
    {
        if (value is < 0 or > PermilleWhole) errors.Add($"{at}: {field} must be between 0 and {PermilleWhole}");
    }

    private static void CheckRange(string at, string field, IntRange range, int minAllowed, List<string> errors)
    {
        if (range.Min < minAllowed) errors.Add($"{at}: {field}.min must be >= {minAllowed}");
        if (range.Max < range.Min) errors.Add($"{at}: {field}.max must be >= {field}.min");
    }
}
