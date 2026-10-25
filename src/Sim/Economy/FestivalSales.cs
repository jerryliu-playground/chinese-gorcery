namespace ChineseGrocery.Sim.Economy;

/// <summary>Sales over the days of one festival's window that fall in a season.</summary>
/// <param name="GoodsUnits">Units of the festival's own goods sold in those days.</param>
public sealed record FestivalSales(string FestivalId, int Days, long RevenueCents, long AverageDailyRevenueCents, int GoodsUnits, long GoodsRevenueCents);
