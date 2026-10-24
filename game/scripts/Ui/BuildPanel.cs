using ChineseGrocery.Sim.Grid;
using Godot;

namespace ChineseGrocery.Game.Ui;

/// <summary>Build mode tools (before opening): pick a fixture to place, or act on the selected one.</summary>
public partial class BuildPanel : PanelBase
{
    protected override void Rebuild()
    {
        SetTitle(UiText.T("ui.build.title"));
        ClearBody();
        Body.AddChild(Text(UiText.T("ui.build.hint")));

        var sim = Host.Sim;
        foreach (var def in Host.Data.Fixtures.Where(f => f.UnlockStage <= sim.Economy.Stage))
        {
            Body.AddChild(Row(
                Text(UiText.T(def.NameKey), 120),
                Text(UiText.Money(def.PriceCents), 70),
                ActionButton("ui.build.place", () =>
                {
                    Ui.ClearTool();
                    Ui.PlacingFixtureId = def.Id;
                    Ui.Notify();
                })));
        }

        if (Ui.SelectedInstanceId is { } id && sim.Layout.Find(id) is { } placed)
        {
            var def = Host.Data.Fixtures.Single(f => f.Id == placed.FixtureId);
            Body.AddChild(new HSeparator());
            Body.AddChild(Text(UiText.F("ui.build.selected", ("fixture", UiText.T(def.NameKey)))));
            Body.AddChild(Row(
                ActionButton("ui.build.move", () =>
                {
                    Ui.ClearTool();
                    Ui.MovingInstanceId = id;
                    Ui.GhostRotation = placed.Rotation;
                    Ui.Notify();
                }),
                ActionButton("ui.build.rotate", () => Host.Send(new MoveFixture(id, placed.Origin, (Rotation)(((int)placed.Rotation + 1) % 4)))),
                ActionButton("ui.build.sell", () =>
                {
                    Host.Send(new SellFixture(id));
                    Ui.SelectedInstanceId = null;
                    Ui.Notify();
                })));
        }
    }
}
