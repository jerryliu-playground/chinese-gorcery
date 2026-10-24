using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Core;

/// <summary>Upgrading the store to the next stage.</summary>
public sealed partial class GameSim
{
    private Rejection? HandleUpgradeStage()
    {
        if (Clock.Phase != DayPhase.PreOpen) return Reject(RejectReason.WrongPhase);
        if (Economy.Stage >= Data.Stages.Count) return Reject(RejectReason.MaxStage);

        var next = Data.Stages[Economy.Stage];
        if (Economy.CumulativeRevenueCents < next.RequiredCumulativeRevenueCents) return Reject(RejectReason.NotEnoughRevenue);
        if (!Economy.CanAfford(next.UpgradeCostCents)) return Reject(RejectReason.InsufficientCash);

        // The expansion is counted as fixture spending, so cash still reconciles as revenue - purchases - wages - fixtures.
        Economy.Spend(ExpenseCategory.Fixtures, next.UpgradeCostCents);
        Economy.AdvanceStage();
        Layout.Size = next.StoreSize;
        _events.Add(new StageUpgraded(next.Id, next.UpgradeCostCents));
        return null;
    }
}
