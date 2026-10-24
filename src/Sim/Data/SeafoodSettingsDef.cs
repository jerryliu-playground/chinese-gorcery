namespace ChineseGrocery.Sim.Data;

/// <summary>economy.json "seafood": fish tank water quality (0-100) and how it kills the animals.</summary>
/// <param name="WaterDropBasePerHourMilli">Quality lost per open hour by any tank, in thousandths of a point.</param>
/// <param name="WaterDropPerUnitPerHourMilli">Extra loss per open hour for each live animal in the tank.</param>
/// <param name="WaterDropPerDeadPerHourMilli">Extra loss per open hour for each dead animal left in the tank.</param>
/// <param name="CleanBelow">A CleanTank task appears when quality falls below this.</param>
/// <param name="DeathMultiplierPoints">Water quality to death-chance multiplier (permille of the product's base), linear between points.</param>
public sealed record SeafoodSettingsDef(
    int WaterDropBasePerHourMilli,
    int WaterDropPerUnitPerHourMilli,
    int WaterDropPerDeadPerHourMilli,
    int CleanBelow,
    IReadOnlyList<QualityPoint> DeathMultiplierPoints);

public sealed record QualityPoint(int Quality, int Permille);
