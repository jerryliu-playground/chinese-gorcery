using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Inventory;

namespace ChineseGrocery.Sim.Tasks;

/// <summary>A worker put carried goods back into storage; their task is open again.</summary>
public sealed record GoodsReturned(int WorkerId, string ProductId, int Quantity, StockLocation Location) : SimEvent;
