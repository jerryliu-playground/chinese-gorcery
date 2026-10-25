using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;

namespace ChineseGrocery.Sim.Imports;

/// <summary>
/// A delivered shipment still wrapped on its pallet: in a storage area with room for it, or, when none had room, on a
/// floor cell by the door (blocking it). Its goods cannot be shelved until unloaded into storage.
/// </summary>
public sealed record Pallet(int Id, IReadOnlyList<StockLot> Lots, int? StorageInstanceId, GridPoint? FloorCell);
