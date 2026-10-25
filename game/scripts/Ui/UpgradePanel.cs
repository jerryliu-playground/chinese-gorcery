using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Reputation;

namespace ChineseGrocery.Game.Ui;

/// <summary>
/// Shows what the next stage needs (lifetime revenue and cash) and lets the player upgrade before opening; below it,
/// one-off store upgrades (the bilingual price labels).
/// </summary>
public partial class UpgradePanel : PanelBase
{
    protected override void Rebuild()
    {
        SetTitle(UiText.T("ui.upgrade.title"));
        ClearBody();
        AddStage();
        AddLabels();
    }

    private void AddStage()
    {
        var sim = Host.Sim;
        var stages = Host.Data.Stages;

        Body.AddChild(Text(UiText.F("ui.upgrade.current", ("stage", UiText.T(stages[sim.Economy.Stage - 1].NameKey)))));
        if (sim.Economy.Stage >= stages.Count)
        {
            Body.AddChild(Text(UiText.T("ui.upgrade.max")));
            return;
        }

        var next = stages[sim.Economy.Stage];
        var ready = sim.Economy.CumulativeRevenueCents >= next.RequiredCumulativeRevenueCents && sim.Economy.CashCents >= next.UpgradeCostCents;
        Body.AddChild(Text(UiText.F("ui.upgrade.next", ("stage", UiText.T(next.NameKey)))));
        Body.AddChild(Text(UiText.F("ui.upgrade.revenue", ("have", UiText.Money(sim.Economy.CumulativeRevenueCents)), ("need", UiText.Money(next.RequiredCumulativeRevenueCents)))));
        Body.AddChild(Text(UiText.F("ui.upgrade.cost", ("cost", UiText.Money(next.UpgradeCostCents)), ("cash", UiText.Money(sim.Economy.CashCents)))));
        Body.AddChild(ActionButton("ui.upgrade.confirm", () =>
        {
            Host.Send(new UpgradeStage());
            Close();
        }, ready));
    }

    /// <summary>The bilingual price labels: what they do, what they cost, and a buy button (or that they are up).</summary>
    private void AddLabels()
    {
        if (Host.Data.Reputation?.BilingualLabels is not { } labels) return;

        var sim = Host.Sim;
        Body.AddChild(Text(" "));
        Body.AddChild(Text(UiText.T("ui.labels.title")));
        Body.AddChild(Text("  " + UiText.T("ui.labels.desc")));
        if (sim.HasBilingualLabels)
        {
            Body.AddChild(Text("  " + UiText.T("ui.labels.owned")));
            return;
        }

        Body.AddChild(Text("  " + UiText.F("ui.labels.cost", ("cost", UiText.Money(labels.CostCents)))));
        var ready = sim.Economy.Stage >= labels.UnlockStage && sim.Economy.CashCents >= labels.CostCents;
        Body.AddChild(ActionButton("ui.labels.buy", () =>
        {
            Host.Send(new BuyBilingualLabels());
            Close();
        }, ready));
    }
}
