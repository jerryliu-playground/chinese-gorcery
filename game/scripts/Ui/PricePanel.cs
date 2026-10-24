using ChineseGrocery.Sim.Economy;
using Godot;

namespace ChineseGrocery.Game.Ui;

/// <summary>Set selling prices (before opening), shown next to the reference price.</summary>
public partial class PricePanel : PanelBase
{
    protected override void Rebuild()
    {
        SetTitle(UiText.T("ui.price.title"));
        ClearBody();
        Body.AddChild(Text(UiText.T("ui.price.hint")));

        var primals = Host.Data.MeatCuts.Select(c => c.PrimalId).ToHashSet();
        foreach (var product in Host.Data.Products.Where(p => !primals.Contains(p.Id)))
        {
            var price = new SpinBox
            {
                MinValue = 0.01, MaxValue = 9999, Step = 0.01, Prefix = "$",
                Value = Host.Sim.Prices.PriceOf(product.Id) / 100.0, CustomMinimumSize = new Vector2(80, 0),
            };
            Body.AddChild(Row(
                Text(UiText.T(product.NameKey), 110),
                Text(UiText.F("ui.price.reference", ("price", UiText.Money(product.SuggestedPriceCents))), 90),
                price,
                ActionButton("ui.price.set", () => Host.Send(new SetPrice(product.Id, (long)Math.Round(price.Value * 100))))));
        }
    }
}
