using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Imports;

public sealed record PalletUnloaded(int PalletId, int WorkerId) : SimEvent;
