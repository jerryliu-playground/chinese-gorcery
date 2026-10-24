using ChineseGrocery.Sim.Inventory;

namespace ChineseGrocery.Sim.Tasks;

/// <summary>Take one primal from storage to the cutting table and cut it into boxes, which go back to storage.</summary>
public sealed record CutMeatTask(int Id, int Priority, StockLocation Source, string PrimalId, int CuttingTableInstanceId)
    : SimTask(Id, TaskType.CutMeat, Priority);
