using ChineseGrocery.Game.Ui;
using ChineseGrocery.Game.View;
using Godot;

namespace ChineseGrocery.Game;

/// <summary>Boot scene: starts a game and wires the Sim host, the store view and the HUD together.</summary>
public partial class Main : Node2D
{
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

        // The store is clipped to its screen area so a scrolled floor never shows under the tool column or top bar.
        var clip = new Control { Name = "StoreClip", ClipContents = true, MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(clip);
        clip.AddChild(_store);

        var layer = new CanvasLayer { Name = "UiLayer" };
        AddChild(layer);
        var hud = new Hud { Name = "Hud" };
        layer.AddChild(hud);
        hud.Init(_host, ui, feel);

        var audio = new Audio.AudioManager { Name = "Audio" };
        AddChild(audio);
        audio.Init(_host, feel);
        var camera = new StoreCamera { Name = "Camera" };
        AddChild(camera);
        camera.Init(_host, _store, clip);
        _store.DisplayClicked += hud.ShowShelf;
        _store.FixtureSelected += _ => ui.Notify();

        if (tour)
        {
            AddChild(new Dev.ScreenshotTour { Name = "ScreenshotTour", Host = _host, Ui = ui, Store = _store, Hud = hud, Camera = camera });
        }

        GD.Print($"Data OK: {_host.Data.Products.Count} products, {_host.Data.Fixtures.Count} fixtures");
    }
}
