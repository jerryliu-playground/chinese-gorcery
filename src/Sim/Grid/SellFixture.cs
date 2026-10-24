using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Grid;

/// <summary>PreOpen only: removes an empty fixture and refunds part of what was paid for it.</summary>
public sealed record SellFixture(int InstanceId) : SimCommand;
