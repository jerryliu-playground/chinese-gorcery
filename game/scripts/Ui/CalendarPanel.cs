using ChineseGrocery.Game.View;
using ChineseGrocery.Sim.Calendar;
using ChineseGrocery.Sim.Data;
using Godot;

namespace ChineseGrocery.Game.Ui;

/// <summary>
/// The calendar: this season's days (festival days, prep days and today marked), its festivals, the events known now
/// (with their answers), and the festival decorations to buy, hang or take down.
/// </summary>
public partial class CalendarPanel : PanelBase
{
    private static readonly Color FestivalColour = new(0.75f, 0.2f, 0.2f);
    private static readonly Color PrepColour = new(0.55f, 0.3f, 0.38f);
    private static readonly Color TodayColour = new(0.85f, 0.7f, 0.25f);
    private static readonly Color DayColour = new(0.22f, 0.22f, 0.27f);

    private readonly Art _art = new();

    protected override void Rebuild()
    {
        ClearBody();
        var sim = Host.Sim;
        if (sim.Date is not { } today || Host.Data.Calendar is not { } calendar)
        {
            Close();
            return;
        }

        var season = calendar.Seasons[today.SeasonIndex];
        SetTitle(UiText.F("ui.calendar.title", ("year", today.Year), ("season", UiText.T(season.NameKey))));
        Body.AddChild(Text(UiText.F("ui.calendar.today", ("season", UiText.T(season.NameKey)), ("day", today.DayOfSeason),
            ("weekday", UiText.T("ui.weekday.short." + sim.Clock.Weekday)), ("n", sim.Clock.Day))));
        Body.AddChild(DayGrid(today, season.Days));
        Body.AddChild(Text(UiText.T("ui.calendar.legend")));

        Festivals(today);
        Events();
        Decorations();
    }

    private GridContainer DayGrid(CalendarDate today, int days)
    {
        var grid = new GridContainer { Columns = 7 };
        grid.AddThemeConstantOverride("h_separation", 2);
        grid.AddThemeConstantOverride("v_separation", 2);
        var first = Host.Sim.Clock.Day - today.DayOfSeason + 1;
        for (var d = 1; d <= days; d++)
        {
            var around = Host.Sim.FestivalAround(first + d - 1);
            var colour = d == today.DayOfSeason ? TodayColour : around is { Offset: 0 } ? FestivalColour : around is { Offset: < 0 } ? PrepColour : DayColour;
            var cell = new PanelContainer { CustomMinimumSize = new Vector2(30, 14) };
            cell.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = colour });
            var label = Text(d.ToString(System.Globalization.CultureInfo.InvariantCulture));
            label.HorizontalAlignment = HorizontalAlignment.Center;
            cell.AddChild(label);
            grid.AddChild(cell);
        }

        return grid;
    }

    private string DateText(int day)
    {
        var date = Host.Sim.DateOf(day)!.Value;
        return UiText.F("ui.date.short", ("season", UiText.T(Host.Data.Calendar!.Seasons[date.SeasonIndex].NameKey)), ("day", date.DayOfSeason));
    }

    private void Festivals(CalendarDate today)
    {
        Body.AddChild(Text(UiText.T("ui.calendar.festivals")));
        var festivals = Host.Data.Festivals?.Festivals ?? [];
        var season = Host.Data.Calendar!.Seasons[today.SeasonIndex].Id;
        var here = festivals.Where(f => f.Season == season).OrderBy(f => f.Day).ToList();
        if (here.Count == 0)
        {
            Body.AddChild(Text("  " + UiText.T("ui.calendar.no_festivals")));
            if (festivals.MinBy(f => Host.Sim.DaysUntil(f)) is { } next)
            {
                Body.AddChild(Text("  " + UiText.F("ui.calendar.next_festival", ("name", UiText.T(next.NameKey)), ("days", Host.Sim.DaysUntil(next)))));
            }

            return;
        }

        foreach (var f in here)
        {
            var until = f.Day - today.DayOfSeason;
            var when = until > 0 ? UiText.F("ui.calendar.in_days", ("days", until)) : until == 0 ? UiText.T("ui.calendar.on_day") : UiText.T("ui.calendar.past");
            var day = Host.Sim.Clock.Day + until;
            Body.AddChild(Text("  " + UiText.F("ui.calendar.festival_line", ("name", UiText.T(f.NameKey)), ("date", DateText(day))) + "  " + when));
            if (f.Goods.Count == 0) continue;

            var goods = string.Join(", ", f.Goods.Select(id => UiText.T(Host.Data.Products.Single(p => p.Id == id).NameKey)));
            Body.AddChild(Text("    " + UiText.F("ui.calendar.goods", ("goods", goods))));
            if (until > 0) Body.AddChild(Text("    " + UiText.F("ui.calendar.order_window", ("date", DateText(day - Host.Data.Festivals!.OrderWindowDays)))));
        }
    }

    private void Events()
    {
        Body.AddChild(Text(UiText.T("ui.calendar.events")));
        if (Host.Sim.StoreEvents.Count == 0) Body.AddChild(Text("  " + UiText.T("ui.calendar.no_events")));
        foreach (var e in Host.Sim.StoreEvents)
        {
            var def = Host.Sim.EventDefOf(e);
            var line = UiText.F("ui.calendar.event_line", ("name", UiText.T(def.NameKey)), ("start", DateText(e.StartDay)), ("end", DateText(e.EndDay)))
                       + " · " + UiText.T("ui.store_event.status." + e.Status.ToString().ToLowerInvariant());
            var row = Row(Icon(def.Icon), Text(line));
            Body.AddChild(row);
            Body.AddChild(Text("    " + NoticeText.Describe(Host, e)));
            foreach (var button in NoticeText.Actions(Host, e, () => _dirty = true)) row.AddChild(button);
        }
    }

    private void Decorations()
    {
        if (Host.Data.Festivals is not { } festivals) return;

        Body.AddChild(Text(UiText.T("ui.calendar.decorations")));
        foreach (var d in festivals.Decorations)
        {
            var owned = Host.Sim.Decorations.FirstOrDefault(o => o.DecorationId == d.Id);
            var names = string.Join(", ", d.Festivals.Select(id => UiText.T(festivals.Festivals.Single(f => f.Id == id).NameKey)));
            var state = owned is null ? "" : "  " + UiText.T(owned.WallX is null ? "ui.calendar.deco_stored" : "ui.calendar.deco_hung");
            var row = Row(Icon(d.Sprite), Text(UiText.F("ui.calendar.deco_line", ("name", UiText.T(d.NameKey)), ("cost", UiText.Money(d.CostCents)), ("festivals", names)) + state, 230));
            if (owned is null) row.AddChild(ActionButton("ui.calendar.buy", () => Send(new BuyDecoration(d.Id))));
            else if (owned.WallX is null) row.AddChild(ActionButton("ui.calendar.hang", () => Send(new HangDecoration(d.Id))));
            else row.AddChild(ActionButton("ui.calendar.store", () => Send(new StoreDecoration(d.Id))));
            Body.AddChild(row);
        }
    }

    private bool _dirty;

    /// <summary>Commands apply on the next tick; the panel is rebuilt after it.</summary>
    private void Send(Sim.Core.SimCommand command)
    {
        Host.Send(command);
        _dirty = true;
    }

    protected override void Refresh()
    {
        if (!_dirty) return;

        _dirty = false;
        Rebuild();
    }

    private TextureRect Icon(string sprite) =>
        new() { Texture = _art.Get(sprite), CustomMinimumSize = new Vector2(16, 16), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
}
