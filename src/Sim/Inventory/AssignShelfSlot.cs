using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Inventory;

/// <summary>Any phase: sets (or with null, clears) the product a shelf / display slot holds. The slot must be empty to change.</summary>
public sealed record AssignShelfSlot(int InstanceId, int Slot, string? ProductId) : SimCommand;
