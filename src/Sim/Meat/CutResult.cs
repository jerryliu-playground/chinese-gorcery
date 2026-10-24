namespace ChineseGrocery.Sim.Meat;

/// <summary>What one primal turns into. Loss is the weight (and cost) that ends up in no box.</summary>
public sealed record CutResult(IReadOnlyList<CutOutput> Outputs, int LossGrams, long LossCents);
