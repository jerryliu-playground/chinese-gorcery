using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;

namespace ChineseGrocery.Sim.Customers;

/// <summary>
/// A shopper: walks to shelves for each item on the list, takes it if the price is acceptable, queues at a checkout,
/// pays when served and walks out. Satisfaction counters are recorded but not used yet (no reputation in the MVP).
/// </summary>
public sealed class Customer(int id, string typeId, GridPoint position, IEnumerable<string> shoppingList)
{
    private readonly List<GridPoint> _path = [];
    private readonly List<string> _shoppingList = [.. shoppingList];
    private readonly List<BasketItem> _basket = [];

    public int Id { get; } = id;
    public string TypeId { get; } = typeId;
    public GridPoint Position { get; internal set; } = position;
    public IReadOnlyList<GridPoint> Path => _path;
    public CustomerState State { get; internal set; } = CustomerState.Shopping;
    public IReadOnlyList<string> ShoppingList => _shoppingList;
    public IReadOnlyList<BasketItem> Basket => _basket;

    /// <summary>Slot and product the customer is heading to or browsing.</summary>
    public (StockLocation Slot, string ProductId)? Target { get; internal set; }

    public int BrowseTicksLeft { get; internal set; }
    public int? CheckoutInstanceId { get; internal set; }
    public long QueueNumber { get; internal set; }
    public int WaitedMinutes { get; internal set; }
    public CustomerExit? Exit { get; internal set; }
    public int ItemsNotFound { get; internal set; }
    public int ItemsTooExpensive { get; internal set; }

    internal void SetPath(IEnumerable<GridPoint> path)
    {
        _path.Clear();
        _path.AddRange(path);
    }

    internal void PopStep() => _path.RemoveAt(0);

    internal string? NextListItem()
    {
        if (_shoppingList.Count == 0) return null;
        var item = _shoppingList[0];
        _shoppingList.RemoveAt(0);
        return item;
    }

    internal void AddToBasket(BasketItem item) => _basket.Add(item);

    internal List<BasketItem> EmptyBasket()
    {
        var items = _basket.ToList();
        _basket.Clear();
        return items;
    }
}
