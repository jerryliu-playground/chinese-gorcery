using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;

namespace ChineseGrocery.Sim.Seafood;

/// <summary>How likely a live animal is to die at a day's close, from its base rate and the tank's water quality.</summary>
public static class Mortality
{
    private const int Certain = 1000;

    public static int ChancePermille(SeafoodSettingsDef settings, ProductDef product, int waterQuality)
    {
        var multiplier = Interpolation.Linear(settings.DeathMultiplierPoints.Select(p => (p.Quality, p.Permille)).ToList(), waterQuality);
        return (int)Math.Min(Certain, (long)product.Live!.DeathBasePermille * multiplier / 1000);
    }
}
