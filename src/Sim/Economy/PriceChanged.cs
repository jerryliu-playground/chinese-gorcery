using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Economy;

public sealed record PriceChanged(string ProductId, long PriceCents) : SimEvent;
