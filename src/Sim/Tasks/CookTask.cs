using ChineseGrocery.Sim.Inventory;

namespace ChineseGrocery.Sim.Tasks;

/// <summary>Cook a dish at a stove from whatever ingredients the kitchen picks, then carry it to its station slot.</summary>
public sealed record CookTask(int Id, int Priority, int StoveInstanceId, StockLocation Target, string DishId)
    : SimTask(Id, TaskType.Cook, Priority);
