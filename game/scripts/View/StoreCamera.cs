using ChineseGrocery.Game.Ui;
using ChineseGrocery.Sim.Grid;
using Godot;

namespace ChineseGrocery.Game.View;

/// <summary>
/// Places the store in the screen area right of the tool column. A store that fits is centred (the back wall gets up
/// to a cell above the floor, cropped if need be). On an axis where the floor does not fit, the view scrolls: drag with
/// the middle mouse button, or use the wheel (Shift for sideways); it also follows the player when they walk near the
/// edge of the view. The store sits in a clipping control that covers its screen area.
/// </summary>
public partial class StoreCamera : Node
{
    public const int TopBarHeight = 18;
    public const int MessageLineHeight = 18;
    private const int FollowMarginCells = 2;
    private const float WheelStep = StoreView.Cell;

    private SimHost _host = null!;
    private StoreView _store = null!;
    private Control _clip = null!;
    private Vector2 _scroll;
    private bool _dragging;
    private GridPoint? _lastPlayerCell;

    public void Init(SimHost host, StoreView store, Control clip)
    {
        _host = host;
        _store = store;
        _clip = clip;
    }

    /// <summary>Scrolls so the cell is as close to the middle of the view as the floor allows (scrolling stores only).</summary>
    public void CenterOn(GridPoint cell)
    {
        var area = ViewArea();
        _scroll = new Vector2((cell.X + 0.5f) * StoreView.Cell - area.Size.X / 2, (cell.Y + 0.5f) * StoreView.Cell + StoreView.Cell - area.Size.Y / 2);
        Place();
    }

    public override void _Process(double delta)
    {
        if (_host?.Sim is null) return;

        FollowPlayer();
        Place();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Middle } button:
                _dragging = button.Pressed;
                break;
            case InputEventMouseMotion motion when _dragging:
                _scroll -= motion.Relative;
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } wheel:
                var step = wheel.ButtonIndex == MouseButton.WheelUp ? -WheelStep : WheelStep;
                _scroll += wheel.ShiftPressed ? new Vector2(step, 0) : new Vector2(0, step);
                break;
            default:
                return;
        }

        GetViewport().SetInputAsHandled();
    }

    /// <summary>Screen rectangle the store may use: right of the tool column, below the top bar.</summary>
    private Rect2 ViewArea()
    {
        var viewport = _store.GetViewportRect().Size;
        return new Rect2(Hud.ToolColumnWidth, TopBarHeight, viewport.X - Hud.ToolColumnWidth, viewport.Y - TopBarHeight - MessageLineHeight);
    }

    /// <summary>When the player has moved, scroll just enough to keep them a couple of cells inside the view.</summary>
    private void FollowPlayer()
    {
        var cell = _host.Sim.Player.Position;
        if (cell == _lastPlayerCell) return;

        _lastPlayerCell = cell;
        var area = ViewArea();
        var margin = FollowMarginCells * StoreView.Cell;
        var x = cell.X * StoreView.Cell;
        var y = cell.Y * StoreView.Cell + StoreView.Cell;
        _scroll.X = Mathf.Clamp(_scroll.X, x + StoreView.Cell + margin - area.Size.X, x - margin);
        _scroll.Y = Mathf.Clamp(_scroll.Y, y + StoreView.Cell + margin - area.Size.Y, y - margin);
    }

    private void Place()
    {
        var size = _host.Sim.Layout.Size;
        var area = ViewArea();
        var floor = new Vector2(size.W * StoreView.Cell, size.H * StoreView.Cell);
        var below = area.Size.Y + MessageLineHeight;
        float x, y;

        if (floor.X <= area.Size.X)
        {
            _scroll.X = 0;
            x = area.Position.X + Mathf.Floor((area.Size.X - floor.X) / 2);
        }
        else
        {
            _scroll.X = Mathf.Clamp(_scroll.X, 0, floor.X - area.Size.X);
            x = area.Position.X - Mathf.Round(_scroll.X);
        }

        if (floor.Y <= below)
        {
            // Fits: the wall gets what is left, up to a cell; on a big floor the message line may overlap the bottom row.
            _scroll.Y = 0;
            _store.WallHeight = (int)Mathf.Clamp(below - floor.Y, 0, StoreView.Cell);
            var spare = Mathf.Max(0, below - MessageLineHeight - floor.Y - _store.WallHeight);
            y = area.Position.Y + _store.WallHeight + Mathf.Floor(spare / 2);
        }
        else
        {
            // Scrolls: the full wall sits above the floor and scrolls with it.
            _store.WallHeight = StoreView.Cell;
            _scroll.Y = Mathf.Clamp(_scroll.Y, 0, floor.Y + StoreView.Cell - area.Size.Y);
            y = area.Position.Y + StoreView.Cell - Mathf.Round(_scroll.Y);
        }

        // Everything right of the tool column and below the top bar, message line included.
        _clip.Position = area.Position;
        _clip.Size = new Vector2(area.Size.X, below);
        _store.Position = new Vector2(x, y) - area.Position;
        _store.VisibleArea = new Rect2(-_store.Position, _clip.Size);
    }
}
