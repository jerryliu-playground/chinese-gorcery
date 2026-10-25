namespace ChineseGrocery.Sim.Data;

/// <summary>economy.json "imports": when importing unlocks, how long ships take, unloading, and buying more storage room.</summary>
public sealed record ImportSettingsDef(
    int UnlockStage,
    int MinDays,
    int MaxDays,
    int UnloadMinutes,
    int StorageUpgradeSlots,
    long StorageUpgradeCostCents);
