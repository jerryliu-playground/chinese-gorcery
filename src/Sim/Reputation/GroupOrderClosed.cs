using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Reputation;

/// <summary>A group order left the books: delivered and paid, cancelled as late or short, declined, or the offer lapsed.</summary>
public sealed record GroupOrderClosed(int OrderId, GroupOrderOutcome Outcome, long PaymentCents) : SimEvent;
