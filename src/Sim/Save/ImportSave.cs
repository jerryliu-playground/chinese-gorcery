using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Imports;

namespace ChineseGrocery.Sim.Save;

public sealed record ImportSave(
    RngState Rng,
    IReadOnlyList<Shipment> Shipments,
    IReadOnlyList<Pallet> Pallets,
    int NextShipmentId,
    int NextPalletId,
    IReadOnlyDictionary<int, int> StorageBonus);
