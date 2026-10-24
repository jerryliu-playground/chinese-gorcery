using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Inventory;
using Godot;

namespace ChineseGrocery.Game.Ui;

/// <summary>Choose which product each slot of a shelf or display holds; restock tasks keep assigned slots filled.</summary>
public partial class ShelfPanel : PanelBase
{
    private readonly List<(StockLocation Slot, Label Count)> _counts = [];

    protected override void Rebuild()
    {
        ClearBody();
        _counts.Clear();
        if (Ui.SelectedInstanceId is not { } id || Host.Sim.Layout.Find(id) is not { } placed)
        {
            Close();
            return;
        }

        var def = Host.Data.Fixtures.Single(f => f.Id == placed.FixtureId);
        SetTitle(UiText.T(def.NameKey));
        Body.AddChild(Text(UiText.T("ui.shelf.hint")));

        var primals = Host.Data.MeatCuts.Select(c => c.PrimalId).ToHashSet();
        var choices = Host.Data.Products.Where(p => def.Storage.Contains(p.Storage) && !primals.Contains(p.Id)).ToList();
        for (var slot = 0; slot < def.Capacity; slot++)
        {
            var location = new StockLocation(id, slot);
            var picker = new OptionButton { CustomMinimumSize = new Vector2(130, 0) };
            picker.AddItem(UiText.T("ui.shelf.empty_slot"));
            foreach (var p in choices) picker.AddItem(UiText.T(p.NameKey));
            picker.Selected = Host.Sim.Shelves.ProductAt(location) is { } current ? choices.FindIndex(p => p.Id == current) + 1 : 0;
            picker.ItemSelected += index => Host.Send(new AssignShelfSlot(id, location.Slot, index == 0 ? null : choices[(int)index - 1].Id));

            var count = Text("", 60);
            _counts.Add((location, count));
            Body.AddChild(Row(Text(UiText.F("ui.shelf.slot", ("n", slot + 1)), 50), picker, count));
        }

        Refresh();
    }

    protected override void Refresh()
    {
        foreach (var (slot, label) in _counts)
        {
            var productId = Host.Sim.Shelves.ProductAt(slot);
            label.Text = productId is null
                ? ""
                : $"{Host.Sim.Inventory.Count(slot, productId)}/{Host.Data.Products.Single(p => p.Id == productId).UnitsPerShelfSlot}";
        }
    }
}
