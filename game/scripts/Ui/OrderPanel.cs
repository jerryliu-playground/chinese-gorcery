using ChineseGrocery.Sim.Inventory;
using Godot;

namespace ChineseGrocery.Game.Ui;

/// <summary>Order from the wholesaler (before opening); goods arrive before the next day's opening.</summary>
public partial class OrderPanel : PanelBase
{
    private readonly Dictionary<string, Label> _have = [];

    protected override void Rebuild()
    {
        SetTitle(UiText.T("ui.order.title"));
        ClearBody();
        _have.Clear();
        Body.AddChild(Text(UiText.T("ui.order.hint")));

        foreach (var product in Host.Data.Products.Where(p => p.Orderable))
        {
            var qty = new SpinBox { MinValue = 1, MaxValue = 999, Value = 10, CustomMinimumSize = new Vector2(70, 0) };
            var have = Text("", 70);
            _have[product.Id] = have;
            Body.AddChild(Row(
                Text(UiText.T(product.NameKey), 110),
                Text(UiText.Money(product.CostCents), 55),
                have,
                qty,
                ActionButton("ui.order.place", () => Host.Send(new PlaceOrder(product.Id, (int)qty.Value)))));
        }

        Refresh();
    }

    protected override void Refresh()
    {
        var sim = Host.Sim;
        foreach (var (id, label) in _have)
        {
            label.Text = UiText.F("ui.order.have", ("stock", sim.Inventory.Total(id)), ("pending", sim.PendingOrders.Where(o => o.ProductId == id).Sum(o => o.Quantity)));
        }
    }
}
