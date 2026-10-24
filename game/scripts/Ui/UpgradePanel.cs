using ChineseGrocery.Sim.Economy;

namespace ChineseGrocery.Game.Ui;

/// <summary>Shows what the next stage needs (lifetime revenue and cash) and lets the player upgrade before opening.</summary>
public partial class UpgradePanel : PanelBase
{
    protected override void Rebuild()
    {
        SetTitle(UiText.T("ui.upgrade.title"));
        ClearBody();
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
}
