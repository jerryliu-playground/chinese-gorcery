using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Game.Ui;

/// <summary>
/// The end-of-day report; confirming it starts the next day. Starting it with nothing on order first asks whether to
/// order (goods ordered after the next day starts only come the day after).
/// </summary>
public partial class ReportPanel : PanelBase
{
    private bool _askingAboutOrders;
    private DailyReport? _report;

    public DailyReport? Report
    {
        get => _report;
        set
        {
            _report = value;
            _askingAboutOrders = false;
        }
    }

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
            ("ui.report.produce_loss", UiText.Money(r.ProduceLossCents)),
            ("ui.report.seafood_loss", UiText.Money(r.SeafoodLossCents)),
            ("ui.report.seafood_deaths", r.SeafoodDeaths.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("ui.report.deli_loss", UiText.Money(r.DeliLossCents)),
            ("ui.report.kitchen_rescued", UiText.Money(r.KitchenRescuedCents)),
            ("ui.report.cash", UiText.Money(r.CashEndCents)),
            ("ui.report.customers", r.Customers.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("ui.report.angry", r.AngryLeaves.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("ui.report.not_found", r.ItemsNotFound.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("ui.report.too_expensive", r.ItemsTooExpensive.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        ];
        foreach (var (key, value) in lines) Body.AddChild(Row(Text(UiText.T(key), 150), Text(value)));
        if (ReputationLine(r.Day) is { } reputation) Body.AddChild(Row(Text(UiText.T("ui.report.reputation"), 150), Text(reputation)));

        Body.AddChild(Text(UiText.T("ui.report.sales")));
        if (r.Sales.Count == 0) Body.AddChild(Text(UiText.T("ui.report.no_sales")));
        foreach (var s in r.Sales)
        {
            var name = UiText.T(Host.Data.Products.Single(p => p.Id == s.ProductId).NameKey);
            Body.AddChild(Row(Text("  " + name, 150), Text(UiText.F("ui.report.sold_line", ("units", s.Units), ("revenue", UiText.Money(s.RevenueCents))))));
        }

        var isLatest = Host.Sim.Clock.Phase == DayPhase.Closed && Host.Sim.Reports.Count > 0 && Host.Sim.Reports[^1] == r;
        if (_askingAboutOrders && isLatest)
        {
            Footer.AddChild(Text(UiText.T("ui.report.no_orders_yet")));
            Footer.AddChild(Row(
                ActionButton("ui.report.order_first", () =>
                {
                    _askingAboutOrders = false;
                    OrderRequested?.Invoke();
                }),
                ActionButton("ui.report.next_day_anyway", StartNextDay)));
            return;
        }

        Footer.AddChild(Row(
            ActionButton("ui.report.order", () => OrderRequested?.Invoke(), isLatest),
            ActionButton("ui.report.next_day", RequestNextDay, isLatest)));
    }

    /// <summary>Reputation before and after that day's closing, e.g. "40 → 42".</summary>
    private string? ReputationLine(int day)
    {
        var history = Host.Sim.ReputationHistory;
        var at = history.ToList().FindIndex(h => h.Day == day);
        if (at < 0) return null;

        var before = at > 0 ? history[at - 1].Reputation : Host.Data.Reputation?.StartReputation ?? history[at].Reputation;
        return UiText.F("ui.report.reputation_change", ("before", before), ("after", history[at].Reputation));
    }

    /// <summary>What "next day" does: starts it, or first asks if nothing is on order.</summary>
    public void RequestNextDay()
    {
        if (Host.Sim.PendingOrders.Count > 0)
        {
            StartNextDay();
            return;
        }

        _askingAboutOrders = true;
        Rebuild();
    }

    private void StartNextDay()
    {
        _askingAboutOrders = false;
        Host.Send(new StartNextDay());
        Close();
    }
}
