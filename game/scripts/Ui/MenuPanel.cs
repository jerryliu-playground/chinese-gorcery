using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Deli;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Time;
using Godot;

namespace ChineseGrocery.Game.Ui;

/// <summary>
/// The daily menu (set before opening): what each deli station slot sells and how many portions to cook today. Lists
/// every dish with the ingredients in the store, flagging near-expiry stock, chilled fish and trimmings, which the
/// kitchen is best at saving.
/// </summary>
public partial class MenuPanel : PanelBase
{
    private readonly List<(StockLocation Slot, Label Status)> _slots = [];

    protected override void Rebuild()
    {
        SetTitle(UiText.T("ui.menu.title"));
        ClearBody();
        _slots.Clear();
        var sim = Host.Sim;
        var data = Host.Data;
        Body.AddChild(Text(UiText.T("ui.menu.hint")));

        Body.AddChild(Text(UiText.T("ui.menu.dishes")));
        foreach (var recipe in data.Recipes) Body.AddChild(Text("  " + DishLine(recipe)));

        var stations = sim.Layout.Fixtures.Where(f => DataValidator.IsDeliStation(Def(f).Kind)).ToList();
        Body.AddChild(Text(UiText.T("ui.menu.slots")));
        if (stations.Count == 0) Body.AddChild(Text("  " + UiText.T("ui.menu.none")));

        var preOpen = sim.Clock.Phase == DayPhase.PreOpen;
        foreach (var station in stations)
        {
            var def = Def(station);
            var number = stations.Where(s => s.FixtureId == station.FixtureId).ToList().IndexOf(station) + 1;
            var choices = data.Recipes.Where(r => r.Station == def.Kind).ToList();
            for (var slot = 0; slot < def.Capacity; slot++)
            {
                var location = new StockLocation(station.InstanceId, slot);
                var current = sim.Menu.GetValueOrDefault(location);
                var picker = new OptionButton { CustomMinimumSize = new Vector2(120, 0), Disabled = !preOpen };
                picker.AddItem(UiText.T("ui.menu.empty_slot"));
                foreach (var r in choices) picker.AddItem(UiText.T(r.NameKey));
                picker.Selected = current is null ? 0 : choices.FindIndex(r => r.Id == current.DishId) + 1;
                var target = new SpinBox { MinValue = 0, MaxValue = 999, Value = current?.TargetPortions ?? 0, CustomMinimumSize = new Vector2(64, 0), Editable = preOpen };

                var name = def.Capacity > 1
                    ? UiText.F("ui.menu.slot", ("station", UiText.T(def.NameKey) + number), ("n", slot + 1))
                    : UiText.T(def.NameKey) + number;
                Body.AddChild(Row(
                    Text(name, 90),
                    picker,
                    Text(UiText.T("ui.menu.target")),
                    target,
                    ActionButton("ui.menu.set", () =>
                    {
                        var dish = picker.Selected <= 0 ? null : choices[picker.Selected - 1].Id;
                        Host.Send(new SetMenuItem(location.FixtureInstanceId, location.Slot, dish, (int)target.Value));
                    }, preOpen)));

                var status = Text("", 0);
                _slots.Add((location, status));
                Body.AddChild(status);
            }
        }

        Refresh();
    }

    protected override void Refresh()
    {
        foreach (var (slot, label) in _slots)
        {
            label.Text = Host.Sim.Menu.GetValueOrDefault(slot) is { } item
                ? "    " + UiText.F("ui.menu.cooked", ("cooked", Host.Sim.CookedToday(slot)), ("target", item.TargetPortions))
                  + "  " + UiText.F("ui.menu.on_station", ("count", Host.Sim.Inventory.Count(slot, item.DishId)))
                : "";
        }
    }

    private FixtureDef Def(Sim.Grid.PlacedFixture f) => Host.Data.Fixtures.Single(d => d.Id == f.FixtureId);

    /// <summary>"Dumplings: ground pork 3/1 (trimmings, 3 near expiry), napa cabbage 8/1 - makes 30".</summary>
    private string DishLine(RecipeDef recipe)
    {
        var sim = Host.Sim;
        var data = Host.Data;
        var chilled = data.Products.Select(p => p.Live?.DiesAs).OfType<string>().ToHashSet();
        var trimmings = data.MeatCuts.Select(c => c.GroundProductId).ToHashSet();
        var parts = recipe.Ingredients.Select(x =>
        {
            var candidates = sim.IngredientCandidates(x.ProductId);
            var flags = new List<string>();
            if (chilled.Contains(x.ProductId)) flags.Add(UiText.T("ui.menu.flag.chilled"));
            if (trimmings.Contains(x.ProductId)) flags.Add(UiText.T("ui.menu.flag.trimmings"));
            var near = candidates.Where(c => sim.IsNearExpiry(c.Lot)).Sum(c => c.Lot.Quantity);
            if (near > 0) flags.Add(UiText.F("ui.menu.flag.near_expiry", ("n", near)));
            var text = UiText.F("ui.menu.ingredient", ("name", UiText.T(data.Products.Single(p => p.Id == x.ProductId).NameKey)),
                ("have", candidates.Sum(c => c.Lot.Quantity)), ("need", x.Quantity));
            return flags.Count == 0 ? text : $"{text} ({string.Join(", ", flags)})";
        });

        var portions = sim.PortionsPossible(recipe.Id);
        var outcome = portions > 0 ? UiText.F("ui.menu.can_make", ("portions", portions)) : UiText.T("ui.menu.short");
        return $"{UiText.T(recipe.NameKey)}: {string.Join(", ", parts)} - {outcome}";
    }
}
