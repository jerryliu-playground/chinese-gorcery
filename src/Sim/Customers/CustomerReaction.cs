using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Customers;

/// <summary>Display-only: a customer's moment worth showing (item missing, too pricey, picked up, or giving up in line).</summary>
public sealed record CustomerReaction(int CustomerId, CustomerReactionKind Kind, string? ProductId = null) : SimEvent;
