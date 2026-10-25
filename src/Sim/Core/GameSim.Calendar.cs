using ChineseGrocery.Sim.Calendar;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Economy;

namespace ChineseGrocery.Sim.Core;

/// <summary>
/// The calendar: dates and seasons, festivals and the demand around them, festival decorations, and the season reports.
/// Everything here follows from the day number and the data, except which decorations are owned and hung.
/// </summary>
public sealed partial class GameSim
{
    private const int NormalDemand = 1000;

    /// <summary>Owned decorations: the back-wall cell each hangs at, or null while stored.</summary>
    private readonly SortedDictionary<string, int?> _decorations = new(StringComparer.Ordinal);

    /// <summary>Today on the calendar (null without calendar data).</summary>
    public CalendarDate? Date => DateOf(Clock.Day);

    public CalendarDate? DateOf(int day) => Data.Calendar is { } c ? GameCalendar.DateOf(c, day) : null;

    public SeasonDef? Season => Date is { } d ? Data.Calendar!.Seasons[d.SeasonIndex] : null;

    /// <summary>The season of a day, 0 (spring) to 3 (winter); spring all year without calendar data.</summary>
    public int SeasonIndexOf(int day) => DateOf(day)?.SeasonIndex ?? 0;

    /// <summary>Seasons played since day 1 (0 for the first spring); counts on across years.</summary>
    public int SeasonsSinceStart(int day) =>
        Data.Calendar is { } c && DateOf(day) is { } d ? (d.Year - 1) * c.Seasons.Count + d.SeasonIndex : 0;

    public IReadOnlyList<OwnedDecoration> Decorations => _decorations.Select(kv => new OwnedDecoration(kv.Key, kv.Value)).ToList();

    /// <summary>The festival whose window (prep days, the day, the days after) covers the day, nearest first, with the offset from it.</summary>
    public (FestivalDef Festival, int Offset)? FestivalAround(int day)
    {
        (FestivalDef, int)? best = null;
        foreach (var (f, o) in WindowsOn(day))
        {
            if (best is null || Math.Abs(o) < Math.Abs(best.Value.Item2)) best = (f, o);
        }

        return best;
    }

    private int _windowsDay = int.MinValue;
    private List<(FestivalDef Festival, int Offset)> _windows = [];
    private HashSet<string>? _festivalGoods;

    /// <summary>Festivals whose window covers the day, with the offset from each (remembered for the last day asked).</summary>
    private List<(FestivalDef Festival, int Offset)> WindowsOn(int day)
    {
        if (day == _windowsDay) return _windows;

        _windows = [];
        if (Data.Calendar is { } c && Data.Festivals is { } fs)
        {
            foreach (var f in fs.Festivals)
            {
                if (GameCalendar.WindowOffset(c, fs, f, day) is { } o) _windows.Add((f, o));
            }
        }

        _windowsDay = day;
        return _windows;
    }

    /// <summary>Days until the festival's next occurrence (0 on the day).</summary>
    public int DaysUntil(FestivalDef festival) => GameCalendar.DaysUntil(Data.Calendar!, festival, Clock.Day);

    public bool IsFestivalGood(string productId)
    {
        _festivalGoods ??= Data.Calendar is null ? [] : (Data.Festivals?.Festivals.SelectMany(f => f.Goods) ?? []).ToHashSet(StringComparer.Ordinal);
        return _festivalGoods.Contains(productId);
    }

    /// <summary>Whether the wholesaler sells the product today: festival goods only while one of their festivals is 1 to the order window's days away.</summary>
    public bool InOrderWindow(string productId)
    {
        if (!IsFestivalGood(productId)) return true;

        var window = Data.Festivals!.OrderWindowDays;
        return Data.Festivals.Festivals.Where(f => f.Goods.Contains(productId)).Any(f => DaysUntil(f) is var d && d >= 1 && d <= window);
    }

    public FestivalDecorationDef? DecorationDef(string decorationId) => Data.Festivals?.Decorations.FirstOrDefault(d => d.Id == decorationId);

    /// <summary>Whether a decoration of the festival is hung.</summary>
    public bool IsDecorated(FestivalDef festival) =>
        _decorations.Any(kv => kv.Value is not null && DecorationDef(kv.Key)?.Festivals.Contains(festival.Id) == true);

    /// <summary>
    /// Demand for the product today as a multiple of normal (permille), and what normal is for it: 1000 for most goods,
    /// the off-season level for festival goods. It combines the season, every festival whose window covers the day (with
    /// the decoration bonus during prep and on the day) and events running today.
    /// </summary>
    public (int Permille, int Baseline) DemandOf(ProductDef product)
    {
        if (Data.Calendar is not { } c) return (NormalDemand, NormalDemand);

        var day = Clock.Day;
        var fs = Data.Festivals;
        long factor = NormalDemand;
        var baseline = NormalDemand;
        if (fs is not null && IsFestivalGood(product.Id))
        {
            baseline = fs.OffSeasonDemandPermille;
            factor = baseline;
            foreach (var (f, offset) in WindowsOn(day).Where(w => w.Festival.Goods.Contains(product.Id)))
            {
                var level = baseline + (long)(f.GoodsPeakPermille - baseline) * GameCalendar.SharePermille(fs, f, offset) / 1000;
                if (offset <= 0 && IsDecorated(f)) level = level * fs.DecorationBonusPermille / 1000;
                factor = Math.Max(factor, level);
            }
        }

        var season = c.Seasons[GameCalendar.DateOf(c, day).SeasonIndex].Id;
        foreach (var s in c.SeasonalDemand.Where(s => s.Season == season && s.Factor.Matches(product))) factor = factor * s.Permille / 1000;

        foreach (var (f, offset) in WindowsOn(day))
        {
            var share
 = GameCalendar.SharePermille(fs!, f, offset);
            foreach (var d in f.Demand.Where(d => d.Matches(product)))
            {
                var level = NormalDemand + (long)(d.Permille - NormalDemand) * share / 1000;
                if (offset <= 0 && IsDecorated(f)) level = level * fs!.DecorationBonusPermille / 1000;
                factor = factor * level / 1000;
            }
        }

        foreach (var (_, def) in EventsOn(day))
        {
            foreach (var d in (def.Demand ?? []).Where(d => d.Matches(product))) factor = factor * d.Permille / 1000;
        }

        return ((int)Math.Clamp(factor, 1, 1_000_000), baseline);
    }

    /// <summary>How many customers come today as a multiple of normal (permille): festival days and events change it.</summary>
    public int CalendarArrivalPermille()
    {
        long permille = NormalDemand;
        if (FestivalAround(Clock.Day) is { Offset: 0 } festival) permille = permille * festival.Festival.DayCustomersPermille / 1000;
        foreach (var (_, def) in EventsOn(Clock.Day)) permille = permille * def.CustomersPermille / 1000;
        return (int)permille;
    }

    /// <summary>On the first day of a festival's prep window, a reminder.</summary>
    private void RemindFestivals()
    {
        if (Data.Calendar is not { } c || Data.Festivals is not { } fs) return;

        foreach (var f in fs.Festivals.Where(f => GameCalendar.WindowOffset(c, fs, f, Clock.Day) == -GameCalendar.PrepDays(fs, f)))
        {
            _events.Add(new FestivalReminder(f.Id, GameCalendar.PrepDays(fs, f)));
        }
    }

    private Rejection? HandleBuyDecoration(BuyDecoration buy)
    {
        if (DecorationDef(buy.DecorationId) is not { } def) return Reject(RejectReason.UnknownDecoration);
        if (_decorations.ContainsKey(def.Id)) return Reject(RejectReason.AlreadyOwned);
        if (!Economy.CanAfford(def.CostCents)) return Reject(RejectReason.InsufficientCash);

        Economy.Spend(ExpenseCategory.Fixtures, def.CostCents);
        _decorations[def.Id] = null;
        _events.Add(new DecorationBought(def.Id, def.CostCents));
        return null;
    }

    private Rejection? HandleHangDecoration(HangDecoration hang)
    {
        if (!_decorations.ContainsKey(hang.DecorationId)) return Reject(RejectReason.NotOwned);

        var taken = Data.Stages[Economy.Stage - 1].Decorations?.Where(d => d.Cell.Y < 0).Select(d => d.Cell.X).ToHashSet() ?? [];
        taken.UnionWith(_decorations.Where(kv => kv.Key != hang.DecorationId && kv.Value is not null).Select(kv => kv.Value!.Value));
        int x;
        if (hang.WallX is { } wanted)
        {
            if (wanted < 0 || wanted >= Layout.Size.W) return Reject(RejectReason.OutOfBounds);
            if (taken.Contains(wanted)) return Reject(RejectReason.WallTaken);
            x = wanted;
        }
        else
        {
            var free = Enumerable.Range(0, Layout.Size.W).Where(c => !taken.Contains(c)).Select(c => (int?)c).FirstOrDefault();
            if (free is null) return Reject(RejectReason.WallTaken);
            x = free.Value;
        }

        _decorations[hang.DecorationId] = x;
        return null;
    }

    private Rejection? HandleStoreDecoration(StoreDecoration store)
    {
        if (!_decorations.ContainsKey(store.DecorationId)) return Reject(RejectReason.NotOwned);

        _decorations[store.DecorationId] = null;
        return null;
    }

    /// <summary>Every season that has been played to its end, in order.</summary>
    public IReadOnlyList<QuarterReport> QuarterReports()
    {
        if (Data.Calendar is not { } c || _reports.Count == 0) return [];

        var list = new List<QuarterReport>();
        var last = GameCalendar.DateOf(c, _reports[^1].Day);
        for (var year = 1; year <= last.Year; year++)
        {
            for (var season = 0; season < c.Seasons.Count; season++)
            {
                var (_, end) = GameCalendar.SeasonSpan(c, year, season);
                if (end > _reports[^1].Day) return list;

                list.Add(QuarterReportFor(year, season));
            }
        }

        return list;
    }

    /// <summary>One season's summary, from the daily reports of its days.</summary>
    public QuarterReport QuarterReportFor(int year, int seasonIndex)
    {
        var c = Data.Calendar ?? throw new InvalidOperationException("No calendar data.");
        var (first, last) = GameCalendar.SeasonSpan(c, year, seasonIndex);
        var days = _reports.Where(r => r.Day >= first && r.Day <= last).ToList();
        var festivals = new List<FestivalSales>();
        foreach (var f in Data.Festivals?.Festivals ?? [])
        {
            var window = days.Where(r => GameCalendar.WindowOffset(c, Data.Festivals!, f, r.Day) is not null).ToList();
            if (window.Count == 0) continue;

            var goods = window.SelectMany(r => r.Sales).Where(s => f.Goods.Contains(s.ProductId)).ToList();
            var revenue = window.Sum(r => r.RevenueCents);
            festivals.Add(new FestivalSales(f.Id, window.Count, revenue, revenue / window.Count, goods.Sum(s => s.Units), goods.Sum(s => s.RevenueCents)));
        }

        var other = days.Where(r => FestivalAround(r.Day) is null).ToList();
        return new QuarterReport(
            year, seasonIndex, first, last, days.Sum(r => r.RevenueCents), days.Sum(r => r.ProfitCents), days.Sum(r => r.LossCents),
            days.Sum(r => r.Customers), other.Count == 0 ? 0 : other.Sum(r => r.RevenueCents) / other.Count, festivals);
    }

    /// <summary>At closing: the day's festival and events go on its report; on a season's last day, the season report follows.</summary>
    private DailyReport AnnotateReport(DailyReport report)
    {
        var festival = FestivalAround(Clock.Day);
        var events = _storeEvents.Where(e => e.IsOn(Clock.Day) || e.AnnouncedDay == Clock.Day).Select(e => e.EventId).ToList();

        return report with
        {
            FestivalId = festival?.Festival.Id,
            FestivalDayOffset = festival?.Offset,
            EventIds = events.Count == 0 ? null : events,
        };
    }

    /// <summary>Spec 16.1: shoppers like a store dressed for the festival; counted once, at the close of the festival day.</summary>
    private void ReputationFromDecorations()
    {
        if (Data.Festivals is not { DecorationReputation: > 0 } fs || FestivalAround(Clock.Day) is not { Offset: 0 } today) return;

        if (IsDecorated(today.Festival)) NudgeReputation(fs.DecorationReputation, "reputation.reason.decorations");
    }

    private void ReportSeasonIfOver()
    {
        if (Data.Calendar is not { } c || Date is not { } today) return;
        if (today.DayOfSeason != c.Seasons[today.SeasonIndex].Days) return;

        _events.Add(new QuarterReportReady(QuarterReportFor(today.Year, today.SeasonIndex)));
    }
}
