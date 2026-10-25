using ChineseGrocery.Sim.Customers;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;

namespace ChineseGrocery.Sim.Save;

public sealed record CustomerSave(
    int Id,
    string TypeId,
    GridPoint Position,
    IReadOnlyList<GridPoint> Path,
    int MoveProgress,
    CustomerState State,
    IReadOnlyList<string> ShoppingList,
    IReadOnlyList<BasketItem> Basket,
    StockLocation? TargetSlot,
    string? TargetProductId,
    int BrowseTicksLeft,
    int? CheckoutInstanceId,
    long QueueNumber,
    int WaitedSeconds,
    CustomerExit? Exit,
    int ItemsNotFound,
    int ItemsTooExpensive,
    string? WaitingFor = null,
    int ItemsBought = 0,
    int Hearts = 0,
    string? SeekingTrend = null,
    bool? TrendFound = null,
    bool Confused = false,
    int LongestWaitSeconds = 0,
    int? Satisfaction = null);
