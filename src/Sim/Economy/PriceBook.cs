using ChineseGrocery.Sim.Data;

namespace ChineseGrocery.Sim.Economy;

/// <summary>Selling prices. A product the player never priced sells at its suggested (reference) price.</summary>
public sealed class PriceBook(IReadOnlyList<ProductDef> products)
{
    private readonly Dictionary<string, long> _suggested = products.ToDictionary(p => p.Id, p => p.SuggestedPriceCents, StringComparer.Ordinal);
    private readonly SortedDictionary<string, long> _set = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, long> Overrides => _set;

    public long PriceOf(string productId) => _set.TryGetValue(productId, out var price) ? price : _suggested[productId];

    public long ReferenceOf(string productId) => _suggested[productId];

    internal void Set(string productId, long priceCents) => _set[productId] = priceCents;
}
