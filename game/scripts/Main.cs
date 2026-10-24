using ChineseGrocery.Game.Ui;
using ChineseGrocery.Game.View;
using Godot;

namespace ChineseGrocery.Game;

/// <summary>Boot scene: starts a game and wires the Sim host, the store view and the HUD together.</summary>
public partial class Main : Node2D
{
    private const int TopBarHeight = 20;
    private const int MessageLineHeight = 18;

    private SimHost _host = null!;
    private StoreView _store = null!;

    public override void _Ready()
    {
        InputSetup.Register();
        Language.Apply();
        GetNode<Label>("Error").Theme = UiTheme.Create();

        _host = new SimHost { Name = "SimHost" };
        AddChild(_host);
        if (_host.StartNewGame() is { } error)
        {
            GetNode<Label>("Error").Text = UiText.T("ui.error.data_invalid") + "\n" + error;
            _host.SetProcess(false);
            return;
        }

        GetNode<Label>("Error").Visible = false;
        var ui = new UiState();
        _store = new StoreView { Name = "Store" };
        _store.Init(_host, ui);
        AddChild(_store);

        var layer = new CanvasLayer { Name = "UiLayer" };
        AddChild(layer);
        var hud = new Hud { Name = "Hud" };
        layer.AddChild(hud);
        hud.Init(_host, ui);
        _store.DisplayClicked += hud.ShowShelf;
        _store.FixtureSelected += _ => ui.Notify();

        GD.Print($"Data OK: {_host.Data.Products.Count} products, {_host.Data.Fixtures.Count} fixtures");
    }

    public override void _Process(double delta)
    {
        if (_store is null) return;

        // Keep the store centred right of the tool column as it grows with stage upgrades.
        var size = _host.Sim.Layout.Size;
        var viewport = GetViewportRect().Size;
        var free = new Vector2(viewport.X - Hud.ToolColumnWidth, viewport.Y - TopBarHeight - MessageLineHeight);
        _store.Position = new Vector2(
            Hud.ToolColumnWidth + Mathf.Floor((free.X - size.W * StoreView.Cell) / 2),
            TopBarHeight + Mathf.Max(0, Mathf.Floor((free.Y - size.H * StoreView.Cell) / 2)));
    }
}
