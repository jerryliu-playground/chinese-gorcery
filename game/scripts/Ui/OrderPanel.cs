using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Inventory;
using Godot;

namespace ChineseGrocery.Game.Ui;

/// <summary>
/// Order from the wholesaler; goods arrive before the next day's opening. Shows how full each storage area is, how many
/// units one storage slot takes, what is in stock and on order, how many sold on the last day open, and how many more
/// fit; the quantity starts at one storage slot.
/// </summary>
public partial class OrderPanel : PanelBase
{
    private readonly Dictionary<string, (Label Facts, int Sold)> _rows = [];
    private bool _importing;

    /// <summary>Switches to the importer's goods (used by the screenshot tour).</summary>
    public void ShowImports()
    {
        _importing = true;
        Rebuild();
    }
    private readonly List<(int Storage, Label Space)> _spaces = [];

    protected override void Rebuild()
    {
        SetTitle(UiText.T("ui.order.title"));
        ClearBody();
        _rows.Clear();
        _spaces.Clear();
        var imports = Host.Data.Economy.Imports;
        var canImport = imports is not null && Host.Sim.Economy.Stage >= imports.UnlockStage;
        _importing &= canImport;
        Body.AddChild(Row(
            ActionButton("ui.order.local", () => { _importing = false; Rebuild(); }, _importing),
            ActionButton("ui.order.import", () => { _importing = true; Rebuild(); }, canImport && !_importing)));
        Body.AddChild(Text(UiText.T(_importing ? "ui.order.import_hint" : "ui.order.hint")));

        foreach (var storage in Host.Sim.Layout.Fixtures.Where(f => DataValidator.IsStorageArea(Def(f).Kind)))
        {
            var label = Text("", 300);
            _spaces.Add((storage.InstanceId, label));
            var cost = imports is null ? "" : UiText.Money(imports.StorageUpgradeCostCents);
            Body.AddChild(imports is null
                ? label
                : Row(label, ActionButton(UiText.F("ui.order.more_room", ("slots", imports.StorageUpgradeSlots), ("cost", cost)), () => Host.Send(new Sim.Imports.UpgradeStorage(storage.InstanceId)))));
        }

        if (_importing)
        {
            AddImportRows(imports!);
            Refresh();
            return;
        }

        var lastDay = Host.Sim.Reports.Count > 0 ? Host.Sim.Reports[^1].Sales : [];
        // Festival goods are only listed while the wholesaler has them (the days before their festival).
        foreach (var product in Host.Data.Products.Where(p => p.Orderable && Host.Sim.InOrderWindow(p.Id)))
        {
            var qty = new SpinBox { MinValue = 1, MaxValue = 999, Value = product.UnitsPerStorageSlot, CustomMinimumSize = new Vector2(64, 0) };
            var cost = Host.Sim.CostOf(product);
            var price = Text(UiText.Money(cost), 60);
            if (cost != product.CostCents) price.AddThemeColorOverride("font_color", new Color(0.95f, 0.55f, 0.4f));
            Body.AddChild(Row(
                Text(UiText.T(product.NameKey), 120),
                price,
                qty,
                ActionButton("ui.order.place", () => Host.Send(new PlaceOrder(product.Id, (int)qty.Value)))));

            // The facts behind the choice, on a line of their own so the columns above stay put.
            var facts = Text("");
            facts.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.66f));
            _rows[product.Id] = (facts, lastDay.FirstOrDefault(s => s.ProductId == product.Id)?.Units ?? 0);
            Body.AddChild(Row(new Control { CustomMinimumSize = new Vector2(16, 0) }, facts));
        }

        Refresh();
    }

    /// <summary>Importable goods: import price, minimum order and sailing time; the quantity starts at the minimum.</summary>
    private void AddImportRows(ImportSettingsDef settings)
    {
        foreach (var product in Host.Data.Products.Where(p => p.Import is not null))
        {
            var import = product.Import!;
            var qty = new SpinBox { MinValue = import.MinUnits, MaxValue = 9999, Value = import.MinUnits, CustomMinimumSize = new Vector2(64, 0) };
            Body.AddChild(Row(
                Text(UiText.T(product.NameKey), 120),
                Text(UiText.Money(import.CostCents), 60),
                qty,
                ActionButton("ui.order.place", () => Host.Send(new Sim.Imports.PlaceImportOrder(product.Id, (int)qty.Value)))));
            var atSea = Host.Sim.Shipments.Where(s => s.ProductId == product.Id).Sum(s => s.Quantity);
            var facts = Text(UiText.F("ui.order.import_facts", ("min", import.MinUnits), ("from", settings.MinDays), ("to", settings.MaxDays),
                ("local", product.Orderable ? UiText.Money(product.CostCents) : UiText.T("ui.order.import_only")), ("at_sea", atSea)));
            facts.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.66f));
            Body.AddChild(Row(new Control { CustomMinimumSize = new Vector2(16, 0) }, facts));
        }
    }

    protected override void Refresh()
    {
        var sim = Host.Sim;
        foreach (var (id, (facts, sold)) in _rows)
        {
            var product = Host.Data.Products.Single(p => p.Id == id);
            facts.Text = UiText.F("ui.order.facts",
                ("per_slot", product.UnitsPerStorageSlot), ("stock", sim.Inventory.Total(id)),
                ("pending", sim.PendingOrders.Where(o => o.ProductId == id).Sum(o => o.Quantity)), ("sold", sold), ("room", sim.RoomFor(id)));
            if (sim.IsOutOfStock(id)) facts.Text += " · " + UiText.T("ui.order.out_of_stock");
            else if (sim.CostOf(product) != product.CostCents) facts.Text += " · " + UiText.T("ui.order.price_up");

        }

        foreach (var (storage, label) in _spaces)
        {
            var placed = sim.Layout.Find(storage);
            if (placed is null) continue;

            var def = Def(placed);
            label.Text = UiText.F("ui.order.space", ("name", UiText.T(def.NameKey)), ("used", sim.SlotsUsed(storage)), ("total", sim.StorageCapacityOf(storage)));
        }
    }

    private FixtureDef Def(Sim.Grid.PlacedFixture f) => Host.Data.Fixtures.Single(d => d.Id == f.FixtureId);
}
