using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Imports;

public sealed record PalletArrived(int PalletId, string ProductId, int Quantity, bool OnTheFloor) : SimEvent;
