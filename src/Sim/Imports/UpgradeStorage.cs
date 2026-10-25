using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Imports;

/// <summary>Buy more room for a storage area.</summary>
public sealed record UpgradeStorage(int InstanceId) : SimCommand;
