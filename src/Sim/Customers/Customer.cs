using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;

namespace ChineseGrocery.Sim.Customers;

/// <summary>
/// A shopper: walks to shelves for each item on the list, takes it if the price is acceptable, queues at a checkout,
/// pays when served and walks out. What happens on the way adds up to a satisfaction score as they leave (reputation).
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

    /// <summary>The checkout, or seafood counter, the customer is heading to or queuing at.</summary>
    public int? CheckoutInstanceId { get; internal set; }

    /// <summary>At a seafood counter: the live product being killed to order for them.</summary>
    public string? WaitingFor { get; internal set; }
    public long QueueNumber { get; internal set; }
    public int WaitedSeconds { get; internal set; }

    /// <summary>Walking carried over between ticks, in cell-seconds (60 = one cell).</summary>
    public int MoveProgress { get; internal set; }
    public CustomerExit? Exit { get; internal set; }
    public int ItemsNotFound { get; internal set; }
    public int ItemsTooExpensive { get; internal set; }

    /// <summary>Units paid for at the checkout.</summary>
    public int ItemsBought { get; internal set; }

    /// <summary>Hearts shown (finding a trending product).</summary>
    public int Hearts { get; internal set; }

    /// <summary>The trending product this customer came in for, if any.</summary>
    public string? SeekingTrend { get; internal set; }

    /// <summary>Whether they found the trending product they came for (null: not decided yet, or not seeking one).</summary>
    public bool? TrendFound { get; internal set; }

    /// <summary>Puzzled by price labels without English (shown once as a question mark).</summary>
    public bool Confused { get; internal set; }

    /// <summary>The longest they stood in any line (seconds).</summary>
    public int LongestWaitSeconds { get; internal set; }

    /// <summary>Satisfaction (0-100), set as they leave.</summary>
    public int? Satisfaction { get; internal set; }

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

    /// <summary>Puts an item at the top of the list (what they came in for).</summary>
    internal void PutFirstOnList(string productId) => _shoppingList.Insert(0, productId);

    internal void AddToBasket(BasketItem item) => _basket.Add(item);

    internal List<BasketItem> EmptyBasket()
    {
        var items = _basket.ToList();
        _basket.Clear();
        return items;
    }
}
