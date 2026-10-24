using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Inventory;

namespace ChineseGrocery.Sim.Seafood;

/// <summary>
/// Live seafood died: in a tank (it stays there until taken out, no loss yet) or left too long in storage (thrown away,
/// <paramref name="LossCents"/> written off).
/// </summary>
public sealed record SeafoodDied(StockLocation Where, string ProductId, int Count, long LossCents) : SimEvent;
