using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Game.Ui;

/// <summary>The end-of-day report; confirming it starts the next day.</summary>
public partial class ReportPanel : PanelBase
{
    public DailyReport? Report { get; set; }

    /// <summary>Raised when the player wants to order straight from the report (ordering is allowed after closing).</summary>
    public event Action? OrderRequested;

    protected override void Rebuild()
    {
        ClearBody();
        if (Report is not { } r)
        {
            Close();
            return;
        }

        SetTitle(UiText.F("ui.report.title", ("day", r.Day)));
        (string Key, string Value)[] lines =
        [
            ("ui.report.revenue", UiText.Money(r.RevenueCents)),
            ("ui.report.purchases", UiText.Money(r.PurchaseCostCents)),
            ("ui.report.wages", UiText.Money(r.WagesCents)),
            ("ui.report.fixtures", UiText.Money(r.FixtureSpendCents)),
            ("ui.report.profit", UiText.Money(r.ProfitCents)),
            ("ui.report.loss", UiText.Money(r.LossCents)),
            ("ui.report.cash", UiText.Money(r.CashEndCents)),
            ("ui.report.customers", r.Customers.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("ui.report.angry", r.AngryLeaves.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("ui.report.not_found", r.ItemsNotFound.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("ui.report.too_expensive", r.ItemsTooExpensive.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        ];
        foreach (var (key, value) in lines) Body.AddChild(Row(Text(UiText.T(key), 150), Text(value)));

        Body.AddChild(Text(UiText.T("ui.report.sales")));
        if (r.Sales.Count == 0) Body.AddChild(Text(UiText.T("ui.report.no_sales")));
        foreach (var s in r.Sales)
        {
            var name = UiText.T(Host.Data.Products.Single(p => p.Id == s.ProductId).NameKey);
            Body.AddChild(Row(Text("  " + name, 150), Text(UiText.F("ui.report.sold_line", ("units", s.Units), ("revenue", UiText.Money(s.RevenueCents))))));
        }

        var isLatest = Host.Sim.Clock.Phase == DayPhase.Closed && Host.Sim.Reports.Count > 0 && Host.Sim.Reports[^1] == r;
        Footer.AddChild(Row(
            ActionButton("ui.report.order", () => OrderRequested?.Invoke(), isLatest),
            ActionButton("ui.report.next_day", () =>
            {
                Host.Send(new StartNextDay());
                Close();
            }, isLatest)));
    }
}
