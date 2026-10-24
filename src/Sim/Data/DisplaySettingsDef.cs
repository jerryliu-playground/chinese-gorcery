namespace ChineseGrocery.Sim.Data;

/// <summary>economy.json "display": how a produce stand's display score (0-100) is made and what it does.</summary>
/// <param name="NoNeighborPairing">Pairing score of a stand with no neighbouring produce stand.</param>
/// <param name="BuyChancePoints">Display score to buy-chance multiplier (permille), linear between points.</param>
/// <param name="MessPerItemTaken">Tidiness lost each time a customer takes an item from the stand.</param>
/// <param name="TidyBelow">A Tidy task appears when tidiness falls below this.</param>
public sealed record DisplaySettingsDef(
    DisplayWeights Weights,
    int NoNeighborPairing,
    IReadOnlyList<ScorePoint> BuyChancePoints,
    int MessPerItemTaken,
    int TidyBelow);

public sealed record DisplayWeights(int Fullness, int Freshness, int Tidiness, int Pairing);

public sealed record ScorePoint(int Score, int Permille);
