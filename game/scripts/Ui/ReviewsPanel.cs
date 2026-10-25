using ChineseGrocery.Sim.Reputation;
using Godot;

namespace ChineseGrocery.Game.Ui;

/// <summary>
/// Reputation at a glance: score and stars, this season's trend, whether the bilingual labels are up, the reputation
/// trend over recent days, and the latest customer reviews (newest first).
/// </summary>
public partial class ReviewsPanel : PanelBase
{
    private const int ReviewsShown = 12;

    private string _shown = "";

    /// <summary>Stars as filled and empty symbols (from the translation table).</summary>
    public static string StarText(int stars) =>
        string.Concat(Enumerable.Repeat(UiText.T("ui.reviews.star_on"), stars)) + string.Concat(Enumerable.Repeat(UiText.T("ui.reviews.star_off"), Math.Max(0, 5 - stars)));

    /// <summary>A review's text, with its product filled in.</summary>
    public static string ReviewText(Review review, Sim.Data.GameData data) =>
        UiText.F(review.TextKey, ("product", review.ProductId is { } id ? UiText.T(data.Products.Single(p => p.Id == id).NameKey) : ""));

    protected override void Rebuild()
    {
        SetTitle(UiText.T("ui.reviews.title"));
        ClearBody();
        var sim = Host.Sim;
        _shown = Signature();

        Body.AddChild(Text(UiText.F("ui.reviews.reputation", ("rep", sim.Reputation), ("stars", StarText(sim.Stars)))));
        Body.AddChild(Text(TrendText(Host) ?? UiText.T("ui.reviews.no_trend")));
        Body.AddChild(Text(UiText.T(sim.HasBilingualLabels ? "ui.reviews.labels_owned" : "ui.reviews.labels_missing")));

        Body.AddChild(Text(UiText.T("ui.reviews.chart")));
        var chart = new ReputationChart();
        Body.AddChild(chart);
        chart.SetData(sim.ReputationHistory, Host.Data.Reputation?.StarMinReputation ?? []);

        Body.AddChild(Text(UiText.T("ui.reviews.recent")));
        if (sim.Reviews.Count == 0) Body.AddChild(Text("  " + UiText.T("ui.reviews.none")));
        foreach (var review in sim.Reviews.Reverse().Take(ReviewsShown))
        {
            var who = review.CustomerTypeId is { } type ? UiText.T(Host.Data.CustomerTypes.Single(t => t.Id == type).NameKey) : UiText.T("ui.reviews.someone");
            Body.AddChild(Text(UiText.F("ui.reviews.header", ("day", review.Day), ("stars", StarText(review.Stars)), ("who", who))));
            var text = Text("  " + ReviewText(review, Host.Data));
            text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            text.CustomMinimumSize = new Vector2(290, 0);
            text.Modulate = review.Stars >= 4 ? new Color(0.7f, 1f, 0.7f) : review.Stars <= 2 ? new Color(1f, 0.72f, 0.65f) : Colors.White;
            Body.AddChild(text);
        }
    }

    /// <summary>"This season's trend: ..." or null when nothing trends.</summary>
    public static string? TrendText(SimHost host)
    {
        var trends = host.Sim.TrendingProducts;
        if (trends.Count == 0) return null;

        var names = trends.Select(id => UiText.T(host.Data.Products.Single(p => p.Id == id).NameKey));
        return UiText.F("ui.hud.trend", ("products", string.Join(UiText.T("ui.list.separator"), names)));
    }

    private string Signature()
    {
        var sim = Host.Sim;
        return $"{sim.Reputation}|{sim.Reviews.Count}|{sim.Reviews.LastOrDefault()}|{sim.HasBilingualLabels}|{string.Join(",", sim.TrendingProducts)}|{sim.ReputationHistory.Count}";
    }

    protected override void Refresh()
    {
        if (Signature() != _shown) Rebuild();
    }
}
