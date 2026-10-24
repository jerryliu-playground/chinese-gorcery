using ChineseGrocery.Sim.Inventory;

namespace ChineseGrocery.Sim.Tasks;

/// <summary>Carry a product from a storage area to a shelf / display slot that is running low.</summary>
public sealed record RestockTask(int Id, int Priority, StockLocation Source, StockLocation Target, string ProductId)
    : SimTask(Id, TaskType.Restock, Priority);
