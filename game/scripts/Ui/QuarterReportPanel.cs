using System.Globalization;
using ChineseGrocery.Sim.Economy;

namespace ChineseGrocery.Game.Ui;

/// <summary>The season report: money and waste over the season, and how each festival period sold against ordinary days.</summary>
public partial class QuarterReportPanel : PanelBase
{
    public QuarterReport? Report { get; set; }

    protected override void Rebuild()
    {
        ClearBody();
        if (Report is not { } q)
        {
            Close();
            return;
        }

        var season = UiText.T(Host.Data.Calendar!.Seasons[q.SeasonIndex].NameKey);
        SetTitle(UiText.F("ui.quarter.title", ("season", season), ("year", q.Year)));
        (string Key, string Value)[] lines =
        [
            ("ui.quarter.days", UiText.F("ui.quarter.days_value", ("first", q.FirstDay), ("last", q.LastDay))),
            ("ui.report.revenue", UiText.Money(q.RevenueCents)),
            ("ui.report.profit", UiText.Money(q.ProfitCents)),
            ("ui.report.loss", UiText.Money(q.LossCents)),
            ("ui.report.customers", q.Customers.ToString(CultureInfo.InvariantCulture)),
            ("ui.quarter.other_days", UiText.Money(q.OtherDaysAverageRevenueCents)),
        ];
        foreach (var (key, value) in lines) Body.AddChild(Row(Text(UiText.T(key), 170), Text(value)));

        Body.AddChild(Text(UiText.T("ui.quarter.festivals")));
        if (q.Festivals.Count == 0) Body.AddChild(Text("  " + UiText.T("ui.quarter.no_festivals")));
        foreach (var f in q.Festivals)
        {
            var name = UiText.T(Host.Data.Festivals!.Festivals.Single(x => x.Id == f.FestivalId).NameKey);
            Body.AddChild(Text("  " + UiText.F("ui.quarter.festival_line", ("name", name), ("days", f.Days), ("avg", UiText.Money(f.AverageDailyRevenueCents)))));
            if (f.GoodsUnits > 0) Body.AddChild(Text("    " + UiText.F("ui.quarter.goods_line", ("units", f.GoodsUnits), ("revenue", UiText.Money(f.GoodsRevenueCents)))));
        }
    }
}
