using ChineseGrocery.Sim.Data;

namespace ChineseGrocery.Sim.Calendar;

/// <summary>
/// A random event that has happened (from events.json): known from <paramref name="AnnouncedDay"/>, in effect from
/// <paramref name="StartDay"/> to <paramref name="EndDay"/>. Kept until its last day is over.
/// </summary>
/// <param name="ProductId">Stockout and clearance offer: the product.</param>
/// <param name="Department">Cost rise: the department.</param>
/// <param name="Goods">Clearance offer (one entry) and group order: what and how many.</param>
/// <param name="UnitCostCents">Clearance offer: price per unit.</param>
/// <param name="BestBeforeDay">Clearance offer: last day the offered stock can be sold.</param>
/// <param name="GeneratorRented">Power cut: a generator keeps everything running.</param>
public sealed record StoreEvent(
    int Id,
    string EventId,
    int AnnouncedDay,
    int StartDay,
    int EndDay,
    StoreEventStatus Status = StoreEventStatus.Announced,
    string? ProductId = null,
    Department? Department = null,
    IReadOnlyList<StockEntryDef>? Goods = null,
    long UnitCostCents = 0,
    int BestBeforeDay = 0,
    bool GeneratorRented = false)
{
    public bool IsOn(int day) => StartDay <= day && day <= EndDay;
}
