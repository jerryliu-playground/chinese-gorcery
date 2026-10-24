using ChineseGrocery.Sim.Data;

namespace ChineseGrocery.Sim.Staff;

/// <summary>Morale comes from the wage against the market wage; it scales work speed. Formulas and numbers are placeholders in data.</summary>
public static class Morale
{
    private const int Full = 1000;

    public static int FromWage(RoleDef role, long wageCentsPerDay)
    {
        var ratio = role.MarketWageCentsPerDay == 0 ? Full : wageCentsPerDay * Full / role.MarketWageCentsPerDay;
        var morale = role.MoraleAtMarketWagePermille + (ratio - Full) * role.MoralePerWageRatioPermille / Full;
        return (int)Math.Clamp(morale, 0, Full);
    }

    /// <summary>Base speed scaled from <see cref="RoleDef.MinMoraleSpeedPermille"/> at zero morale up to full at full morale.</summary>
    public static int Speed(RoleDef role, int baseSpeedPermille, int moralePermille)
    {
        var factor = role.MinMoraleSpeedPermille + (long)(Full - role.MinMoraleSpeedPermille) * moralePermille / Full;
        return (int)Math.Max(1, baseSpeedPermille * factor / Full);
    }
}
