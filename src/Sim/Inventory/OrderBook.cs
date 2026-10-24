namespace ChineseGrocery.Sim.Inventory;

/// <summary>Orders paid for but not yet delivered.</summary>
public sealed class OrderBook
{
    private readonly List<PendingOrder> _pending = [];

    public IReadOnlyList<PendingOrder> Pending => _pending;

    internal void Add(PendingOrder order) => _pending.Add(order);

    /// <summary>Removes and returns everything pending, in the order it was placed.</summary>
    internal IReadOnlyList<PendingOrder> TakeAll()
    {
        var all = _pending.ToList();
        _pending.Clear();
        return all;
    }
}
