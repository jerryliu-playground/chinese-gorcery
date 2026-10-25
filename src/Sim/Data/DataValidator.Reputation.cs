namespace ChineseGrocery.Sim.Data;

/// <summary>Checks on reputation.json and the reputation fields of customer types.</summary>
public static partial class DataValidator
{
    private const int StarCount = 5;
    private const int MaxReputationValue = 100;

    private static void ValidateReputation(GameData data, Dictionary<string, ProductDef> products, HashSet<int> stageIds, List<string> errors)
    {
        const string file = DataFiles.Reputation;
        foreach (var t in data.CustomerTypes)
        {
            var at = $"{DataFiles.CustomerTypes}: '{t.Id}'";
            if (t.MinStars is < 1 or > StarCount) errors.Add($"{at}: minStars must be between 1 and {StarCount}");
            if (t.RequiresDepartments is { } needs && needs.Distinct().Count() != needs.Count) errors.Add($"{at}: requiresDepartments has duplicates");
        }

        if (data.Reputation is not { } r) return;

        if (r.StartReputation is < 0 or > MaxReputationValue) errors.Add($"{file}: startReputation must be between 0 and {MaxReputationValue}");
        if (r.StarMinReputation.Count != StarCount || r.StarMinReputation[0] != 0 || r.StarMinReputation.Zip(r.StarMinReputation.Skip(1)).Any(p => p.Second <= p.First) || r.StarMinReputation[^1] > MaxReputationValue)
        {
            errors.Add($"{file}: starMinReputation must be {StarCount} rising values from 0 to at most {MaxReputationValue}");
        }

        CheckPermille(file, "dailyPullPermille", r.DailyPullPermille, errors);
        CheckReputationCurve(file, "arrivalPoints", r.ArrivalPoints, errors);
        CheckReputationCurve(file, "applicantQualityPoints", r.ApplicantQualityPoints, errors);
        if (r.ApplicantQualityPoints.Any(p => p.Permille > PermilleWhole)) errors.Add($"{file}: applicantQualityPoints permille must be at most {PermilleWhole}");

        var s = r.Satisfaction;
        if (s.Base is < 0 or > MaxReputationValue) errors.Add($"{file}: satisfaction.base must be between 0 and {MaxReputationValue}");
        if (s.MaxItemBonus < 0 || s.PerItemBought < 0) errors.Add($"{file}: satisfaction item bonuses must be >= 0");
        if (s.LongWaitPermilleOfPatience <= 0) errors.Add($"{file}: satisfaction.longWaitPermilleOfPatience must be > 0");

        var trends = r.Trends;
        if (trends.Departments.Count == 0) errors.Add($"{file}: trends.departments must not be empty");
        CheckRange(file, "trends.count", trends.Count, 0, errors);
        CheckPermille(file, "trends.seekPermille", trends.SeekPermille, errors);
        CheckPermille(file, "trends.heartPermille", trends.HeartPermille, errors);
        if (trends.WordOfMouthArrivals < 0 || trends.WordOfMouthDelayMinutes < 0) errors.Add($"{file}: trends word of mouth values must be >= 0");

        var labels = r.BilingualLabels;
        if (labels.CostCents < 0) errors.Add($"{file}: bilingualLabels.costCents must be >= 0");
        if (!stageIds.Contains(labels.UnlockStage)) errors.Add($"{file}: bilingualLabels.unlockStage {labels.UnlockStage} does not exist in {DataFiles.Stages}");
        if (labels.BuyChanceBonusPermille < 0 || labels.ExtraListItems < 0) errors.Add($"{file}: bilingualLabels bonuses must be >= 0");
        CheckPermille(file, "bilingualLabels.confusedPermille", labels.ConfusedPermille, errors);

        var orders = r.GroupOrders;
        CheckPermille(file, "groupOrders.offerPermille", orders.OfferPermille, errors);
        CheckPermille(file, "groupOrders.wholesalePermille", orders.WholesalePermille, errors);
        if (orders.MaxActive <= 0) errors.Add($"{file}: groupOrders.maxActive must be > 0");
        CheckRange(file, "groupOrders.deadlineDays", orders.DeadlineDays, 0, errors);
        CheckRange(file, "groupOrders.lines", orders.Lines, 1, errors);
        if (orders.Candidates.Count == 0) errors.Add($"{file}: groupOrders.candidates must not be empty");
        foreach (var c in orders.Candidates)
        {
            if (!products.ContainsKey(c.ProductId)) errors.Add($"{file}: groupOrders candidate '{c.ProductId}' does not exist in {DataFiles.Products}");
            CheckRange(file, $"groupOrders candidate '{c.ProductId}' units", c.Units, 1, errors);
        }

        if (orders.Candidates.Select(c => c.ProductId).Distinct().Count() != orders.Candidates.Count) errors.Add($"{file}: groupOrders.candidates list a product more than once");

        var reviews = r.Reviews;
        CheckRange(file, "reviews.perDay", reviews.PerDay, 1, errors);
        if (reviews.Keep <= 0) errors.Add($"{file}: reviews.keep must be > 0");
        CheckIds(file, reviews.Templates.Select(t => t.Id).ToList(), errors);
        foreach (var t in reviews.Templates.Where(t => t.Stars is < 1 or > StarCount)) errors.Add($"{file}: review template '{t.Id}' stars must be between 1 and {StarCount}");
    }

    private static void CheckReputationCurve(string file, string field, IReadOnlyList<ReputationPoint> points, List<string> errors)
    {
        if (points.Count == 0) errors.Add($"{file}: {field} must not be empty");
        if (points.Zip(points.Skip(1)).Any(p => p.Second.Reputation <= p.First.Reputation)) errors.Add($"{file}: {field} reputation values must rise");
        if (points.Any(p => p.Permille < 0)) errors.Add($"{file}: {field} permille must be >= 0");
    }
}
