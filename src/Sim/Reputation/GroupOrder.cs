using System.Text.Json.Serialization;
using ChineseGrocery.Sim.Inventory;

namespace ChineseGrocery.Sim.Reputation;

/// <summary>
/// A restaurant's bulk order. Once accepted, matching goods are taken out of storage into <paramref name="Reserved"/>
/// (so they are never shelved or sold) until every line is covered; it is delivered and paid at a closing on or before
/// <paramref name="DueDay"/>, otherwise cancelled at that day's closing.
/// </summary>
/// <param name="DeadlineDays">Days the buyer gives, counted from the day it is accepted.</param>
/// <param name="DueDay">Set on accepting: the last day it can be delivered (at closing).</param>
public sealed record GroupOrder(
    int Id,
    string CustomerTypeId,
    IReadOnlyList<GroupOrderLine> Lines,
    int OfferedDay,
    int DeadlineDays,
    GroupOrderStatus Status,
    int? DueDay,
    IReadOnlyList<StockLot> Reserved)
{
    [JsonIgnore]
    public long PaymentCents => Lines.Sum(l => l.Units * l.UnitPriceCents);

    public int ReservedOf(string productId) => Reserved.Where(l => l.ProductId == productId).Sum(l => l.Quantity);

    public int MissingOf(GroupOrderLine line) => Math.Max(0, line.Units - ReservedOf(line.ProductId));

    [JsonIgnore]
    public bool IsComplete => Lines.All(l => MissingOf(l) == 0);
}
