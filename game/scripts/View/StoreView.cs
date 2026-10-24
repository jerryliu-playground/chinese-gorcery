using ChineseGrocery.Game.Ui;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Tasks;
using Godot;

namespace ChineseGrocery.Game.View;

/// <summary>
/// Draws the store floor, fixtures, stock and people, and turns mouse/keyboard input into Sim commands.
/// It reads Sim state and uses the Sim's pure layout rules for previews; it never changes state directly.
/// </summary>
public partial class StoreView : Node2D
{
    public const int Cell = 32;

    private SimHost _host = null!;
    private UiState _ui = null!;
    private GridPoint _facing = new(0, -1);
    private GridPoint? _hover;
    private long _lastMoveTick = -1;
    private bool _keyboardWalking;

    // Where each walker stood after the previous tick and the one before, to draw them sliding between ticks.
    private Dictionary<string, GridPoint> _before = [];
    private Dictionary<string, GridPoint> _after = [];

    /// <summary>Raised when a display fixture is clicked in play mode (to open its slots).</summary>
    public event Action<int>? DisplayClicked;

    /// <summary>Raised when a placed fixture is clicked in build mode.</summary>
    public event Action<int>? FixtureSelected;

    public void Init(SimHost host, UiState ui)
    {
        _host = host;
        _ui = ui;
        _after = Walkers();
        host.Ticked += () =>
        {
            _before = _after;
            _after = Walkers();
        };
    }

    private Dictionary<string, GridPoint> Walkers()
    {
        var sim = _host.Sim;
        var all = new Dictionary<string, GridPoint>();
        foreach (var w in sim.Workers) all["w" + w.Id] = w.Position;
        foreach (var c in sim.Customers) all["c" + c.Id] = c.Position;
        return all;
    }

    /// <summary>Pixel centre of a walker, slid between its last two tick positions (snapping on long jumps).</summary>
    private Vector2 DrawPosition(string key, GridPoint now)
    {
        var to = CenterOf(now);
        if (!_before.TryGetValue(key, out var was) || !_after.TryGetValue(key, out var latest) || latest != now) return to;

        var from = CenterOf(was);
        return from.DistanceTo(to) > 3 * Cell ? to : from.Lerp(to, Mathf.Clamp(_host.TickProgress, 0, 1));
    }

    private GameData Data => _host.Data;

    public override void _Process(double delta)
    {
        if (_host?.Sim is null) return;

        HandleHeldMovement();
        _hover = CellAt(GetLocalMousePosition());
        QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (_host?.Sim is null) return;

        if (e.IsActionPressed(InputSetup.Interact)) Interact();
        else if (e.IsActionPressed(InputSetup.Rotate)) RotateGhost();
        else if (e.IsActionPressed(InputSetup.Cancel)) CancelTool();
        else if (MakeInputLocal(e) is InputEventMouseButton { Pressed: true } click && CellAt(click.Position) is { } cell)
        {
            if (click.ButtonIndex == MouseButton.Left) LeftClick(cell);
            else if (click.ButtonIndex == MouseButton.Right) CancelTool();
            else return;
        }
        else return;

        GetViewport().SetInputAsHandled();
    }

    private GridPoint? CellAt(Vector2 local)
    {
        var cell = new GridPoint(Mathf.FloorToInt(local.X / Cell), Mathf.FloorToInt(local.Y / Cell));
        return LayoutRules.InBounds(_host.Sim.Layout.Size, cell) ? cell : null;
    }

    /// <summary>
    /// WASD / arrows: while held, keep a short walk queued ahead in that direction (re-aimed each tick) so the player
    /// moves at full walking speed; on release, stop where they are.
    /// </summary>
    private void HandleHeldMovement()
    {
        const int lookAhead = 3;
        if (_ui.Mode != ToolMode.Play) return;

        GridPoint? dir = Input.IsActionPressed(InputSetup.MoveUp) ? new(0, -1)
            : Input.IsActionPressed(InputSetup.MoveDown) ? new(0, 1)
            : Input.IsActionPressed(InputSetup.MoveLeft) ? new(-1, 0)
            : Input.IsActionPressed(InputSetup.MoveRight) ? new(1, 0)
            : null;
        var player = _host.Sim.Player;
        if (_lastMoveTick == _host.Sim.TickCount || player.IsWorking) return;

        if (dir is not { } d)
        {
            if (_keyboardWalking) _host.Send(new MovePlayer(player.Position));
            _keyboardWalking = false;
            _lastMoveTick = _host.Sim.TickCount;
            return;
        }

        _facing = d;
        var target = player.Position;
        for (var i = 0; i < lookAhead; i++)
        {
            var next = new GridPoint(target.X + d.X, target.Y + d.Y);
            if (!IsFree(next)) break;
            target = next;
        }

        if (target == player.Position) return;

        _host.Send(new MovePlayer(target));
        _keyboardWalking = true;
        _lastMoveTick = _host.Sim.TickCount;
    }

    private bool IsFree(GridPoint cell) =>
        LayoutRules.InBounds(_host.Sim.Layout.Size, cell) && FixtureAt(cell) is null;

    private PlacedFixture? FixtureAt(GridPoint cell) =>
        _host.Sim.Layout.Fixtures.FirstOrDefault(f => LayoutRules.Footprint(Def(f), f.Origin, f.Rotation).Contains(cell));

    private FixtureDef Def(PlacedFixture f) => Data.Fixtures.Single(d => d.Id == f.FixtureId);

    private FixtureDef Def(string id) => Data.Fixtures.Single(d => d.Id == id);

    /// <summary>Interacts with the fixture in front of the player if they stand at its work cell; otherwise lets the Sim choose.</summary>
    private void Interact()
    {
        if (_ui.Mode != ToolMode.Play) return;

        var p = _host.Sim.Player.Position;
        var faced = FixtureAt(new GridPoint(p.X + _facing.X, p.Y + _facing.Y));
        var usable = faced is not null && LayoutRules.InteractionCells(Def(faced), faced.Origin, faced.Rotation).Contains(p);
        _host.Send(new PlayerInteract(usable ? faced!.InstanceId : null));
    }

    private void LeftClick(GridPoint cell)
    {
        if (_ui.Mode == ToolMode.Play)
        {
            if (FixtureAt(cell) is { } fixture)
            {
                if (DataValidator.IsDisplay(Def(fixture).Kind)) DisplayClicked?.Invoke(fixture.InstanceId);
            }
            else
            {
                _host.Send(new MovePlayer(cell));
            }

            return;
        }

        if (_ui.PlacingFixtureId is { } placing)
        {
            _host.Send(new PlaceFixture(placing, cell, _ui.GhostRotation));
        }
        else if (_ui.MovingInstanceId is { } moving)
        {
            _host.Send(new MoveFixture(moving, cell, _ui.GhostRotation));
            _ui.ClearTool();
            _ui.Notify();
        }
        else if (FixtureAt(cell) is { } fixture)
        {
            _ui.SelectedInstanceId = fixture.InstanceId;
            FixtureSelected?.Invoke(fixture.InstanceId);
            _ui.Notify();
        }
    }

    private void RotateGhost()
    {
        if (_ui.PlacingFixtureId is null && _ui.MovingInstanceId is null) return;

        _ui.GhostRotation = (Rotation)(((int)_ui.GhostRotation + 1) % 4);
    }

    private void CancelTool()
    {
        _ui.ClearTool();
        _ui.Notify();
    }

    public override void _Draw()
    {
        if (_host?.Sim is null) return;

        var sim = _host.Sim;
        DrawFloor(sim.Layout.Size);
        foreach (var f in sim.Layout.Fixtures) DrawFixture(f);
        if (_ui.Mode == ToolMode.Build) DrawUseCells();
        DrawGhost();
        foreach (var c in sim.Customers) DrawCustomer(c);
        foreach (var w in sim.Workers) DrawWorker(w);
    }

    private void DrawFloor(GridSize size)
    {
        for (var y = 0; y < size.H; y++)
        for (var x = 0; x < size.W; x++)
        {
            DrawRect(new Rect2(x * Cell, y * Cell, Cell, Cell), (x + y) % 2 == 0 ? Palette.FloorA : Palette.FloorB);
        }

        DrawRect(new Rect2(0, 0, size.W * Cell, size.H * Cell), Palette.Outline, filled: false, width: 2);
    }

    private static Rect2 Area(GridPoint origin, GridSize size, int inset = 0) =>
        new(origin.X * Cell + inset, origin.Y * Cell + inset, size.W * Cell - 2 * inset, size.H * Cell - 2 * inset);

    private void DrawFixture(PlacedFixture f)
    {
        var def = Def(f);
        var rect = Area(f.Origin, LayoutRules.RotatedSize(def, f.Rotation), 2);
        DrawRect(rect, Palette.Fixture(def.Kind));
        DrawRect(rect, _ui.SelectedInstanceId == f.InstanceId && _ui.Mode == ToolMode.Build ? Palette.Selection : Palette.Outline, filled: false, width: 1);

        if (DataValidator.IsDisplay(def.Kind)) DrawSlots(f, def, rect);
        else if (DataValidator.IsStorageArea(def.Kind)) DrawStorageFill(f, def, rect);
    }

    /// <summary>One small bar per slot: colour by department, length by how full it is.</summary>
    private void DrawSlots(PlacedFixture f, FixtureDef def, Rect2 rect)
    {
        var slotW = (rect.Size.X - 4) / def.Capacity;
        for (var slot = 0; slot < def.Capacity; slot++)
        {
            var location = new StockLocation(f.InstanceId, slot);
            var box = new Rect2(rect.Position.X + 2 + slot * slotW + 1, rect.Position.Y + 3, slotW - 2, rect.Size.Y - 6);
            DrawRect(box, Palette.Outline.Darkened(0.2f));
            if (_host.Sim.Shelves.ProductAt(location) is not { } productId) continue;

            var product = Data.Products.Single(p => p.Id == productId);
            var fill = (float)_host.Sim.Inventory.Count(location, productId) / product.UnitsPerShelfSlot;
            var color = Palette.Department(product.Department);
            DrawRect(new Rect2(box.Position.X, box.End.Y - box.Size.Y * fill, box.Size.X, box.Size.Y * fill), color);
            DrawRect(box, color, filled: false, width: 1);
        }
    }

    private void DrawStorageFill(PlacedFixture f, FixtureDef def, Rect2 rect)
    {
        var lots = _host.Sim.Inventory.TryGet(StockLocation.Storage(f.InstanceId))?.Lots ?? [];
        var slots = StorageCapacity.SlotsNeeded(lots.Select(l => (Data.Products.Single(p => p.Id == l.ProductId), l.Quantity)));
        var fill = Math.Min(1f, (float)slots / def.Capacity);
        DrawRect(new Rect2(rect.Position.X + 3, rect.End.Y - 6, (rect.Size.X - 6) * fill, 3), Palette.Progress);
    }

    private void DrawUseCells()
    {
        foreach (var f in _host.Sim.Layout.Fixtures)
        {
            var def = Def(f);
            foreach (var c in LayoutRules.InteractionCells(def, f.Origin, f.Rotation)) DrawRect(Area(c, new GridSize(1, 1), 10), Palette.WorkCell);
            foreach (var c in LayoutRules.CustomerCells(def, f.Origin, f.Rotation)) DrawRect(Area(c, new GridSize(1, 1), 12), Palette.CustomerCell);
        }
    }

    /// <summary>Preview of the fixture being placed or moved: green when the layout rules accept it.</summary>
    private void DrawGhost()
    {
        if (_ui.Mode != ToolMode.Build || _hover is not { } cell) return;

        var sim = _host.Sim;
        PlacedFixture candidate;
        List<PlacedFixture> layout;
        if (_ui.PlacingFixtureId is { } id)
        {
            candidate = new PlacedFixture(sim.Layout.NextInstanceId, id, cell, 0, _ui.GhostRotation);
            layout = [.. sim.Layout.Fixtures, candidate];
        }
        else if (_ui.MovingInstanceId is { } moving && sim.Layout.Find(moving) is { } current)
        {
            candidate = current with { Origin = cell, Rotation = _ui.GhostRotation };
            layout = sim.Layout.Fixtures.Select(f => f.InstanceId == moving ? candidate : f).ToList();
        }
        else
        {
            return;
        }

        var def = Def(candidate.FixtureId);
        var ok = LayoutRules.Check(sim.Layout.Size, layout, Def, candidate.InstanceId) is null;
        DrawRect(Area(cell, LayoutRules.RotatedSize(def, _ui.GhostRotation)), ok ? Palette.GhostOk : Palette.GhostBad);
        foreach (var c in LayoutRules.InteractionCells(def, cell, _ui.GhostRotation)) DrawRect(Area(c, new GridSize(1, 1), 10), Palette.WorkCell);
    }

    private Vector2 CenterOf(GridPoint cell) => new(cell.X * Cell + Cell / 2f, cell.Y * Cell + Cell / 2f);

    private void DrawCustomer(Sim.Customers.Customer c)
    {
        var typeIndex = Data.CustomerTypes.ToList().FindIndex(t => t.Id == c.TypeId);

        // Spread customers who share a cell so a queue reads as a group.
        var offset = new Vector2((c.Id % 3 - 1) * 7, (c.Id / 3 % 3 - 1) * 7);
        var at = DrawPosition("c" + c.Id, c.Position) + offset;
        DrawCircle(at, 5, Palette.Customer(typeIndex, c.State));
        DrawArc(at, 5, 0, Mathf.Tau, 12, Palette.Outline, 1);
        if (c.Basket.Count > 0) DrawRect(new Rect2(at + new Vector2(3, 1), new Vector2(4, 4)), Colors.White);
    }

    private void DrawWorker(Worker w)
    {
        var at = DrawPosition("w" + w.Id, w.Position);
        var color = w.IsPlayer ? Palette.Player : Palette.Staff;
        DrawRect(new Rect2(at - new Vector2(8, 8), new Vector2(16, 16)), color);
        DrawRect(new Rect2(at - new Vector2(8, 8), new Vector2(16, 16)), Palette.Outline, filled: false, width: 1);

        if (w.IsPlayer)
        {
            // Facing marker.
            DrawRect(new Rect2(at + new Vector2(_facing.X * 9 - 2, _facing.Y * 9 - 2), new Vector2(4, 4)), Palette.Outline);
        }

        if (w.Carrying.Count > 0)
        {
            var product = Data.Products.Single(p => p.Id == w.Carrying[0].ProductId);
            DrawRect(new Rect2(at + new Vector2(-5, -14), new Vector2(10, 6)), Palette.Department(product.Department));
        }

        if (w.IsWorking && w.WorkTicksLeft > 0)
        {
            DrawRect(new Rect2(at + new Vector2(-10, 11), new Vector2(20, 3)), Palette.Outline);
            DrawRect(new Rect2(at + new Vector2(-10, 11), new Vector2(20f / (w.WorkTicksLeft + 1), 3)), Palette.Progress);
        }
    }
}
