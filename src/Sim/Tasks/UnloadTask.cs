namespace ChineseGrocery.Sim.Tasks;

/// <summary>Unwrap a pallet into a storage area with room for it.</summary>
public sealed record UnloadTask(int Id, int Priority, int PalletId, int StorageInstanceId) : SimTask(Id, TaskType.Unload, Priority);
