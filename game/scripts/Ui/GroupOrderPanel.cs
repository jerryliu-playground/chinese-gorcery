using ChineseGrocery.Sim.Reputation;
using Godot;

namespace ChineseGrocery.Game.Ui;

/// <summary>
/// Restaurant group orders: an offer to accept or decline (goods, quantities, deadline, payment), and the running
/// order with how much is already set aside, when it is due and what it pays.
/// </summary>
public partial class GroupOrderPanel : PanelBase
{
    private string _shown = "";

    protected override void Rebuild()
    {
        SetTitle(UiText.T("ui.group.title"));
        ClearBody();
        var sim = Host.Sim;
        _shown = Signature();

        if (sim.GroupOrders.Count == 0)
        {
            var none = Text(UiText.T("ui.group.none"));
            none.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            none.CustomMinimumSize = new Vector2(290, 0);
            Body.AddChild(none);
            return;
        }

        foreach (var order in sim.GroupOrders) AddOrder(order);
    }

    private void AddOrder(GroupOrder order)
    {
        var sim = Host.Sim;
        var buyer = UiText.T(Host.Data.CustomerTypes.Single(t => t.Id == order.CustomerTypeId).NameKey);
        var offered = order.Status == GroupOrderStatus.Offered;
        Body.AddChild(Text(offered
            ? UiText.F("ui.group.offer", ("buyer", buyer))
            : UiText.F("ui.group.active", ("day", order.DueDay ?? 0), ("days", (order.DueDay ?? 0) - sim.Clock.Day))));

        foreach (var line in order.Lines)
        {
            var name = UiText.T(Host.Data.Products.Single(p => p.Id == line.ProductId).NameKey);
            var have = order.ReservedOf(line.ProductId);
            var progress = offered ? "" : UiText.F("ui.group.progress", ("have", have), ("need", line.Units));
            var row = Row(
                Text("  " + name, 110),
                Text(UiText.F("ui.group.line", ("qty", line.Units), ("price", UiText.Money(line.UnitPriceCents))), 120),
                Text(progress));
            if (!offered && have >= line.Units) row.Modulate = new Color(0.7f, 1f, 0.7f);
            Body.AddChild(row);
        }

        if (offered) Body.AddChild(Text(UiText.F("ui.group.deadline", ("days", order.DeadlineDays))));
        Body.AddChild(Text(UiText.F("ui.group.reward", ("money", UiText.Money(order.PaymentCents)))));

        if (offered)
        {
            Body.AddChild(Row(
                ActionButton("ui.group.accept", () => Host.Send(new AcceptGroupOrder(order.Id))),
                ActionButton("ui.group.decline", () => Host.Send(new DeclineGroupOrder(order.Id)))));
            return;
        }

        var note = Text(UiText.T("ui.group.reserved_note"));
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        note.CustomMinimumSize = new Vector2(290, 0);
        note.Modulate = new Color(1, 1, 1, 0.7f);
        Body.AddChild(note);
    }

    private string Signature() =>
        string.Join("|", Host.Sim.GroupOrders.Select(o => $"{o.Id}:{o.Status}:{o.Reserved.Sum(l => l.Quantity)}")) + Host.Sim.Clock.Day;

    protected override void Refresh()
    {
        if (Signature() != _shown) Rebuild();
    }
}
