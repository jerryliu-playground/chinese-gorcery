namespace ChineseGrocery.Sim.Produce;

/// <summary>A produce stand's display score (0..100) and the four parts it is made of, each 0..100.</summary>
public sealed record DisplayScore(int Fullness, int Freshness, int Tidiness, int Pairing, int Total);
