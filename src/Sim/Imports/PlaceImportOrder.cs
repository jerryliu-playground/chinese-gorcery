using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Imports;

/// <summary>Order from the overseas importer: paid now, delivered on a pallet after the sailing time.</summary>
public sealed record PlaceImportOrder(string ProductId, int Quantity) : SimCommand;
