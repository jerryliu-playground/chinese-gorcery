namespace ChineseGrocery.Sim.Inventory;

/// <summary>Goods held at one location. Lots are kept ordered by expiry; quantities never go below zero.</summary>
public sealed class Stockpile(string id)
{
    private readonly List<StockLot> _lots = [];

    public string Id { get; } = id;

    public IReadOnlyList<StockLot> Lots => _lots;

    public int Count(string productId) => _lots.Where(l => l.ProductId == productId).Sum(l => l.Quantity);

    internal void Add(StockLot lot)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lot.Quantity);

        var same = _lots.FindIndex(l => l.ProductId == lot.ProductId && l.BestBeforeDay == lot.BestBeforeDay && l.UnitCostCents == lot.UnitCostCents);
        if (same >= 0)
        {
            _lots[same] = _lots[same] with { Quantity = _lots[same].Quantity + lot.Quantity };
            return;
        }

        var at = _lots.FindIndex(l => Compare(lot, l) < 0);
        _lots.Insert(at < 0 ? _lots.Count : at, lot);
    }

    /// <summary>Removes <paramref name="quantity"/> units, earliest expiry first. Throws (changing nothing) if not enough are held.</summary>
    internal IReadOnlyList<StockLot> Take(string productId, int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        if (Count(productId) < quantity)
        {
            throw new InvalidOperationException($"Stockpile '{Id}' holds {Count(productId)} of '{productId}', cannot take {quantity}.");
        }

        var taken = new List<StockLot>();
        var remaining = quantity;
        for (var i = 0; remaining > 0; i++)
        {
            var lot = _lots[i];
            if (lot.ProductId != productId) continue;

            var n = Math.Min(remaining, lot.Quantity);
            taken.Add(lot with { Quantity = n });
            remaining -= n;
            if (n == lot.Quantity)
            {
                _lots.RemoveAt(i--);
            }
            else
            {
                _lots[i] = lot with { Quantity = lot.Quantity - n };
            }
        }

        return taken;
    }

    /// <summary>Removes and returns every lot whose best-before day is on or before <paramref name="closingDay"/>.</summary>
    internal IReadOnlyList<StockLot> RemoveExpired(int closingDay)
    {
        var expired = _lots.Where(l => l.BestBeforeDay <= closingDay).ToList();
        _lots.RemoveAll(l => l.BestBeforeDay <= closingDay);
        return expired;
    }

    private static int Compare(StockLot a, StockLot b)
    {
        var byDay = a.BestBeforeDay.CompareTo(b.BestBeforeDay);
        return byDay != 0 ? byDay : string.CompareOrdinal(a.ProductId, b.ProductId);
    }
}
