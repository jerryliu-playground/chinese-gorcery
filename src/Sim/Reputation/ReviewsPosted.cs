using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Reputation;

/// <summary>The day's reviews.</summary>
public sealed record ReviewsPosted(int Day, IReadOnlyList<Review> Reviews) : SimEvent;
