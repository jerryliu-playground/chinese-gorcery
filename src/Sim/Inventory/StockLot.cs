using System.Text.Json.Serialization;

namespace ChineseGrocery.Sim.Inventory;

/// <summary>Units of one product that share an expiry day and purchase cost.</summary>
/// <param name="BestBeforeDay">Last day the goods can be sold; discarded at that day's closing.</param>
/// <param name="PriceBonusPermille">Dishes: how much more than the set price they sell for, from the cook's skill.</param>
public sealed record StockLot(
    string ProductId,
    int Quantity,
    int BestBeforeDay,
    long UnitCostCents,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int PriceBonusPermille = 0)
{
    /// <summary>Whether two lots are the same batch (everything but the quantity matches).</summary>
    public bool SameBatch(StockLot other) =>
        ProductId == other.ProductId && BestBeforeDay == other.BestBeforeDay && UnitCostCents == other.UnitCostCents
        && PriceBonusPermille == other.PriceBonusPermille;
}
