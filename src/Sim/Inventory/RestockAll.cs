using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Inventory;

/// <summary>PreOpen only: instantly fill every assigned shelf / display slot from storage, oldest stock first.</summary>
public sealed record RestockAll : SimCommand;
