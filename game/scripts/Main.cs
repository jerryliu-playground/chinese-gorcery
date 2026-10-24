using ChineseGrocery.Game.Ui;
using ChineseGrocery.Game.View;
using Godot;

namespace ChineseGrocery.Game;

/// <summary>Boot scene: starts a game and wires the Sim host, the store view and the HUD together.</summary>
public partial class Main : Node2D
{
    private const int TopBarHeight = 18;
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
        var tour = Dev.ScreenshotTour.RequestedDir() is not null;
        if (_host.StartNewGame(tour ? Dev.ScreenshotTour.Seed : null) is { } error)
        {
            GetNode<Label>("Error").Text = UiText.T("ui.error.data_invalid") + "\n" + error;
            _host.SetProcess(false);
            return;
        }

        GetNode<Label>("Error").Visible = false;
        var ui = new UiState();
        var feel = FeelConfig.Load();
        _store = new StoreView { Name = "Store" };
        _store.Init(_host, ui, feel);
        AddChild(_store);

        var layer = new CanvasLayer { Name = "UiLayer" };
        AddChild(layer);
        var hud = new Hud { Name = "Hud" };
        layer.AddChild(hud);
        hud.Init(_host, ui, feel);

        var audio = new Audio.AudioManager { Name = "Audio" };
        AddChild(audio);
        audio.Init(_host, feel);
        _store.DisplayClicked += hud.ShowShelf;
        _store.FixtureSelected += _ => ui.Notify();

        if (tour)
        {
            AddChild(new Dev.ScreenshotTour { Name = "ScreenshotTour", Host = _host, Ui = ui, Store = _store, Hud = hud });
        }

        GD.Print($"Data OK: {_host.Data.Products.Count} products, {_host.Data.Fixtures.Count} fixtures");
    }

    public override void _Process(double delta)
    {
        if (_store is null) return;

        // Keep the store centred right of the tool column as it grows with stage upgrades. The back wall gets up to a
        // cell above the floor; on a big floor it is cropped, and the message line may then overlap the bottom row.
        var size = _host.Sim.Layout.Size;
        var viewport = GetViewportRect().Size;
        var floorHeight = size.H * StoreView.Cell;
        var below = viewport.Y - TopBarHeight;
        _store.WallHeight = (int)Mathf.Clamp(below - floorHeight, 0, StoreView.Cell);
        var spare = Mathf.Max(0, below - MessageLineHeight - floorHeight - _store.WallHeight);
        _store.Position = new Vector2(
            Hud.ToolColumnWidth + Mathf.Floor((viewport.X - Hud.ToolColumnWidth - size.W * StoreView.Cell) / 2),
            TopBarHeight + _store.WallHeight + Mathf.Floor(spare / 2));
    }
}
