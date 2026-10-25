using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Staff;

/// <summary>A manager could not order all they wanted (no cash or no room).</summary>
public sealed record AutoOrderShort(string ProductId, int Wanted, int Ordered) : SimEvent;
