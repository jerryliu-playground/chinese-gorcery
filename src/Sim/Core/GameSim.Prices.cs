using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Core;

/// <summary>Setting selling prices.</summary>
public sealed partial class GameSim
{
    private Rejection? HandleSetPrice(SetPrice price)
    {
        if (Clock.Phase != DayPhase.PreOpen) return Reject(RejectReason.WrongPhase);
        if (!_products.ContainsKey(price.ProductId)) return Reject(RejectReason.UnknownProduct);
        if (price.PriceCents <= 0) return Reject(RejectReason.InvalidPrice);

        Prices.Set(price.ProductId, price.PriceCents);
        _events.Add(new PriceChanged(price.ProductId, price.PriceCents));
        return null;
    }
}
