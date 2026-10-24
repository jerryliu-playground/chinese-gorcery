using ChineseGrocery.Sim.Inventory;

namespace ChineseGrocery.Sim.Save;

/// <summary>A fish tank's water (millionths of a quality point) and the dead animals still in it.</summary>
public sealed record TankSave(int TankInstanceId, long WaterMicro, IReadOnlyList<StockLot> Dead);
