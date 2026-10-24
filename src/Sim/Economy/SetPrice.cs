using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Economy;

/// <summary>PreOpen only: sets a product's selling price.</summary>
public sealed record SetPrice(string ProductId, long PriceCents) : SimCommand;
