using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Imports;

public sealed record ShipmentsDelayed(int Days) : SimEvent;
