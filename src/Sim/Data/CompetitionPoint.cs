namespace ChineseGrocery.Sim.Data;

/// <summary>At this reputation, regular arrivals run at this share (per mille) of normal.</summary>
public sealed record CompetitionPoint(int Reputation, int ArrivalPermille);
