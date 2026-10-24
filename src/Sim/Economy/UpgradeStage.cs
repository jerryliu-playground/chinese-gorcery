using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Economy;

/// <summary>PreOpen only: pay to move up a stage once lifetime revenue has reached that stage's target.</summary>
public sealed record UpgradeStage : SimCommand;
