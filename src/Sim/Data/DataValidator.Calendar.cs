namespace ChineseGrocery.Sim.Data;

/// <summary>Checks on calendar.json, festivals.json and events.json.</summary>
public static partial class DataValidator
{
    private static void ValidateCalendar(GameData data, Dictionary<string, ProductDef> products, HashSet<int> stageIds, List<string> errors)
    {
        var seasons = new HashSet<string>();
        if (data.Calendar is { } calendar)
        {
            const string file = DataFiles.Calendar;
            if (calendar.Seasons.Count == 0) errors.Add($"{file}: needs at least one season");
            CheckIds(file, calendar.Seasons.Select(s => s.Id).ToList(), errors);
            foreach (var s in calendar.Seasons)
            {
                CheckNameKey($"{file}: season '{s.Id}'", s.NameKey, "season", s.Id, errors);
                if (s.Days <= 0) errors.Add($"{file}: season '{s.Id}' needs days > 0");
            }

            seasons = calendar.Seasons.Select(s => s.Id).ToHashSet();
            for (var i = 0; i < calendar.SeasonalDemand.Count; i++)
            {
                var d = calendar.SeasonalDemand[i];
                var at = $"{file}: seasonalDemand[{i}]";
                if (!seasons.Contains(d.Season)) errors.Add($"{at}: unknown season '{d.Season}'");
                CheckDemand(at, d.Factor, products, errors);
            }
        }

        if (data.Festivals is { } festivals) ValidateFestivals(festivals, data.Calendar, seasons, products, errors);
        if (data.Events is { } events) ValidateEvents(events, data.Economy, seasons, products, stageIds, errors);
    }

    private static void ValidateFestivals(FestivalsDef fs, CalendarDef? calendar, HashSet<string> seasons, Dictionary<string, ProductDef> products, List<string> errors)
    {
        const string file = DataFiles.Festivals;
        if (fs.DefaultPrepDays < 1) errors.Add($"{file}: defaultPrepDays must be >= 1");
        if (fs.OrderWindowDays < 1) errors.Add($"{file}: orderWindowDays must be >= 1");
        if (fs.OffSeasonDemandPermille is < 1 or > PermilleWhole) errors.Add($"{file}: offSeasonDemandPermille must be between 1 and {PermilleWhole}");
        if (fs.DecorationBonusPermille <= 0) errors.Add($"{file}: decorationBonusPermille must be > 0");
        CheckPermille(file, "curve.prepStartSharePermille", fs.Curve.PrepStartSharePermille, errors);
        CheckPermille(file, "curve.eveSharePermille", fs.Curve.EveSharePermille, errors);
        foreach (var share in fs.Curve.AfterSharesPermille) CheckPermille(file, "curve.afterSharesPermille", share, errors);

        CheckIds(file, fs.Festivals.Select(f => f.Id).ToList(), errors);
        var festivalIds = fs.Festivals.Select(f => f.Id).ToHashSet();
        foreach (var f in fs.Festivals)
        {
            var at = $"{file}: festival '{f.Id}'";
            CheckNameKey(at, f.NameKey, "festival", f.Id, errors);
            if (!seasons.Contains(f.Season)) errors.Add($"{at}: unknown season '{f.Season}'");
            else if (f.Day < 1 || f.Day > calendar!.Seasons.Single(s => s.Id == f.Season).Days) errors.Add($"{at}: day must be within its season");
            if (f.PrepDays is < 1) errors.Add($"{at}: prepDays must be >= 1");
            if (calendar is not null && (f.PrepDays ?? fs.DefaultPrepDays) + fs.Curve.AfterSharesPermille.Count >= calendar.DaysPerYear) errors.Add($"{at}: its window must be shorter than a year");
            if (f.GoodsPeakPermille <= 0) errors.Add($"{at}: goodsPeakPermille must be > 0");
            if (f.DayCustomersPermille <= 0) errors.Add($"{at}: dayCustomersPermille must be > 0");
            foreach (var id in f.Goods)
            {
                if (!products.TryGetValue(id, out var p)) errors.Add($"{at}: unknown good '{id}'");
                else if (!p.Orderable) errors.Add($"{at}: good '{id}' must be orderable");
            }

            for (var i = 0; i < f.Demand.Count; i++) CheckDemand($"{at}: demand[{i}]", f.Demand[i], products, errors);
        }

        CheckIds(file, fs.Decorations.Select(d => d.Id).ToList(), errors);
        foreach (var d in fs.Decorations)
        {
            var at = $"{file}: decoration '{d.Id}'";
            CheckNameKey(at, d.NameKey, "decoration", d.Id, errors);
            if (d.CostCents < 0) errors.Add($"{at}: costCents must be >= 0");
            if (d.Festivals.Count == 0) errors.Add($"{at}: needs at least one festival");
            foreach (var id in d.Festivals.Where(id => !festivalIds.Contains(id))) errors.Add($"{at}: unknown festival '{id}'");
        }
    }

    private static void ValidateEvents(EventsDef ev, EconomyDef economy, HashSet<string> seasons, Dictionary<string, ProductDef> products, HashSet<int> stageIds, List<string> errors)
    {
        const string file = DataFiles.Events;
        if (ev.FirstDay < 1) errors.Add($"{file}: firstDay must be >= 1");
        CheckPermille(file, "dailyChancePermille", ev.DailyChancePermille, errors);
        if (ev.MaxNegativeAtOnce < 1) errors.Add($"{file}: maxNegativeAtOnce must be >= 1");
        CheckIds(file, ev.Events.Select(e => e.Id).ToList(), errors);

        bool OpenHours(int minute) => minute >= economy.OpenMinuteOfDay && minute < economy.CloseMinuteOfDay;
        foreach (var e in ev.Events)
        {
            var at = $"{file}: event '{e.Id}'";
            CheckNameKey(at, e.NameKey, "event", e.Id, errors);
            if (e.Weight < 0) errors.Add($"{at}: weight must be >= 0");
            if (string.IsNullOrEmpty(e.Icon)) errors.Add($"{at}: needs an icon");
            if (!stageIds.Contains(e.MinStage)) errors.Add($"{at}: unknown minStage {e.MinStage}");
            foreach (var s in (e.Seasons ?? []).Where(s => !seasons.Contains(s))) errors.Add($"{at}: unknown season '{s}'");
            if (e.LeadDays < 0) errors.Add($"{at}: leadDays must be >= 0");
            if (e.DurationDays is { } days) CheckRange(at, "durationDays", days, 1, errors);
            if (e.CustomersPermille <= 0) errors.Add($"{at}: customersPermille must be > 0");
            if (e.CostPermille <= 0) errors.Add($"{at}: costPermille must be > 0");
            var demand = e.Demand ?? [];
            for (var i = 0; i < demand.Count; i++) CheckDemand($"{at}: demand[{i}]", demand[i], products, errors);

            switch (e.Kind)
            {
                case EventKind.Inspection when e.Inspection is not { } i:
                    errors.Add($"{at}: an inspection needs an 'inspection' block");
                    break;
                case EventKind.Inspection:
                    if (!OpenHours(e.Inspection.ArriveMinuteOfDay) || !OpenHours(e.Inspection.ArriveMinuteOfDay + e.Inspection.StayMinutes)) errors.Add($"{at}: the inspector must come and go during opening hours");
                    if (e.Inspection.StayMinutes <= 0) errors.Add($"{at}: inspection.stayMinutes must be > 0");
                    if (e.Inspection.RewardCents < 0 || e.Inspection.FinePerProblemCents < 0) errors.Add($"{at}: inspection reward and fine must be >= 0");
                    if (e.Inspection.ReputationReward < 0 || e.Inspection.ReputationPerProblem < 0) errors.Add($"{at}: inspection reputation points must be >= 0");
                    break;
                case EventKind.ShipDelay when e.DelayDays is null:
                    errors.Add($"{at}: a ship_delay event needs delayDays");
                    break;
                case EventKind.ShipDelay:
                    CheckRange(at, "delayDays", e.DelayDays.Value, 1, errors);
                    break;
                case EventKind.PowerCut when e.PowerCut is not { } p:
                    errors.Add($"{at}: a power cut needs a 'powerCut' block");
                    break;
                case EventKind.PowerCut:
                    if (!OpenHours(e.PowerCut.StartMinuteOfDay) || e.PowerCut.EndMinuteOfDay <= e.PowerCut.StartMinuteOfDay) errors.Add($"{at}: the power cut must start during opening hours and end after it starts");
                    if (e.PowerCut.AgeDays < 0 || e.PowerCut.WaterDropPermille <= 0 || e.PowerCut.GeneratorCostCents < 0) errors.Add($"{at}: powerCut needs ageDays >= 0, waterDropPermille > 0, generatorCostCents >= 0");
                    break;
                case EventKind.ClearanceOffer when e.Offer is not { } o:
                    errors.Add($"{at}: a clearance offer needs an 'offer' block");
                    break;
                case EventKind.ClearanceOffer:
                    CheckRange(at, "offer.units", e.Offer.Units, 1, errors);
                    if (e.Offer.CostPermille is < 1 or > PermilleWhole) errors.Add($"{at}: offer.costPermille must be between 1 and {PermilleWhole}");
                    if (e.Offer.DaysLeft < 1) errors.Add($"{at}: offer.daysLeft must be >= 1");
                    break;
                case EventKind.GroupOrder when e.GroupOrder is not { } g:
                    errors.Add($"{at}: a group order needs a 'groupOrder' block");
                    break;
                case EventKind.GroupOrder:
                    CheckRange(at, "groupOrder.products", e.GroupOrder.Products, 1, errors);
                    CheckRange(at, "groupOrder.units", e.GroupOrder.Units, 1, errors);
                    if (e.GroupOrder.RewardPermille <= 0) errors.Add($"{at}: groupOrder.rewardPermille must be > 0");
                    if (!OpenHours(e.GroupOrder.PickupMinuteOfDay)) errors.Add($"{at}: groupOrder.pickupMinuteOfDay must be during opening hours");
                    break;
            }
        }
    }

    private static void CheckDemand(string at, DemandFactorDef d, Dictionary<string, ProductDef> products, List<string> errors)
    {
        var selectors = (d.ProductId is null ? 0 : 1) + (d.Department is null ? 0 : 1) + (d.Category is null ? 0 : 1);
        if (selectors != 1) errors.Add($"{at}: set exactly one of productId, department, category");
        if (d.ProductId is { } id && !products.ContainsKey(id)) errors.Add($"{at}: unknown product '{id}'");
        if (d.Permille <= 0) errors.Add($"{at}: permille must be > 0");
    }
}
