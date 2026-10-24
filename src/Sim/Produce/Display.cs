using ChineseGrocery.Sim.Data;

namespace ChineseGrocery.Sim.Produce;

/// <summary>Display score arithmetic: weighting the parts and turning the score into a buy-chance factor.</summary>
public static class Display
{
    public static int Total(DisplaySettingsDef settings, int fullness, int freshness, int tidiness, int pairing)
    {
        var w = settings.Weights;
        var sum = w.Fullness + w.Freshness + w.Tidiness + w.Pairing;
        return (w.Fullness * fullness + w.Freshness * freshness + w.Tidiness * tidiness + w.Pairing * pairing) / sum;
    }

    /// <summary>The buy-chance factor for a score, read off the configured points with straight lines in between.</summary>
    public static int BuyFactorPermille(DisplaySettingsDef settings, int score)
    {
        var points = settings.BuyChancePoints;
        if (score <= points[0].Score) return points[0].Permille;

        for (var i = 1; i < points.Count; i++)
        {
            var (a, b) = (points[i - 1], points[i]);
            if (score <= b.Score) return a.Permille + (b.Permille - a.Permille) * (score - a.Score) / (b.Score - a.Score);
        }

        return points[^1].Permille;
    }
}
