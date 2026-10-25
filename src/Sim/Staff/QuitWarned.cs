using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Staff;

/// <summary>A worker is so unhappy they may quit at the next closing.</summary>
public sealed record QuitWarned(int WorkerId) : SimEvent;
