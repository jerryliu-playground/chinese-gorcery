using ChineseGrocery.Game.Ui;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Tasks;
using Godot;
using GridRotation = ChineseGrocery.Sim.Grid.Rotation;

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
    private readonly HashSet<string> _facingLeft = [];

    private readonly Art _art = new();
    private FeelConfig _feel = new();

    /// <summary>Developer screenshot tool only: pretend the mouse is over this cell.</summary>
    public GridPoint? DebugHover { get; set; }

    /// <summary>Height of the wall strip drawn above the top row (up to one cell), set by the layout.</summary>
    public int WallHeight { get; set; } = Cell;

    /// <summary>Raised when a display fixture is clicked in play mode (to open its slots).</summary>
    public event Action<int>? DisplayClicked;

    /// <summary>Raised when a placed fixture is clicked in build mode.</summary>
    public event Action<int>? FixtureSelected;

    public void Init(SimHost host, UiState ui, FeelConfig feel)
    {
        _host = host;
        _ui = ui;
        _feel = feel;
        _art.LogMissing(host.Data);
        _after = Walkers();
        host.Ticked += () =>
        {
            _before = _after;
            _after = Walkers();
            foreach (var (key, now) in _after)
            {
                if (!_before.TryGetValue(key, out var was) || was.X == now.X) continue;
                if (now.X < was.X) _facingLeft.Add(key);
                else _facingLeft.Remove(key);
            }
        };
        host.GameReplaced += () =>
        {
            _after = Walkers();
            _before = [];
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
        _hover = DebugHover ?? CellAt(GetLocalMousePosition());
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
        var stage = Data.Stages[sim.Economy.Stage - 1];
        DrawFloor(sim.Layout.Size, stage);
        DrawWall(sim.Layout.Size, stage);
        foreach (var f in sim.Layout.Fixtures) DrawFixture(f);
        if (_ui.Mode == ToolMode.Build) DrawUseCells();
        DrawGhost();
        DrawCarryTarget();
        foreach (var c in sim.Customers.OrderBy(c => c.Position.Y)) DrawCustomer(c);
        foreach (var w in sim.Workers) DrawWorker(w);
    }

    // ---------- sprites ----------

    /// <summary>Draws a 32x32 sprite into a cell-sized box, optionally turned by a fixture rotation or mirrored.</summary>
    private void DrawSprite(Texture2D texture, Vector2 topLeft, GridRotation rotation = GridRotation.None, bool flipX = false, Color? modulate = null, Vector2? scale = null)
    {
        var half = new Vector2(Cell, Cell) / 2;
        var s = scale ?? Vector2.One;
        DrawSetTransform(topLeft + half, (int)rotation * Mathf.Pi / 2, new Vector2(flipX ? -s.X : s.X, s.Y));
        DrawTexture(texture, -half, modulate ?? Colors.White);
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    private static Vector2 TopLeftOf(GridPoint cell) => new(cell.X * Cell, cell.Y * Cell);

    private void DrawFloor(GridSize size, StageDef stage)
    {
        var tile = _art.Get(stage.FloorSprite);
        for (var y = 0; y < size.H; y++)
        for (var x = 0; x < size.W; x++)
        {
            if (tile is not null) DrawTexture(tile, TopLeftOf(new GridPoint(x, y)));
            else DrawRect(new Rect2(x * Cell, y * Cell, Cell, Cell), (x + y) % 2 == 0 ? Palette.FloorA : Palette.FloorB);
        }

        DrawRect(new Rect2(0, 0, size.W * Cell, size.H * Cell), Palette.Outline, filled: false, width: 2);
    }

    /// <summary>The back wall above the top row, cropped to the room the screen leaves, with its decorations.</summary>
    private void DrawWall(GridSize size, StageDef stage)
    {
        if (WallHeight <= 0) return;

        var tile = _art.Get(stage.WallSprite);
        var cropTop = Cell - WallHeight;
        for (var x = 0; x < size.W; x++)
        {
            var dest = new Rect2(x * Cell, -WallHeight, Cell, WallHeight);
            if (tile is not null) DrawTextureRectRegion(tile, dest, new Rect2(0, cropTop, Cell, WallHeight));
            else DrawRect(dest, Palette.Outline.Lightened(0.3f));
        }

        foreach (var deco in stage.Decorations ?? [])
        {
            if (_art.Get(deco.Sprite) is not { } texture) continue;

            var top = deco.Cell.Y * Cell + (deco.Cell.Y < 0 ? cropTop : 0);
            var height = deco.Cell.Y < 0 ? WallHeight : Cell;
            DrawTextureRectRegion(texture, new Rect2(deco.Cell.X * Cell, top, Cell, height), new Rect2(0, Cell - height, Cell, height));
        }
    }

    private static Rect2 Area(GridPoint origin, GridSize size, int inset = 0) =>
        new(origin.X * Cell + inset, origin.Y * Cell + inset, size.W * Cell - 2 * inset, size.H * Cell - 2 * inset);

    /// <summary>The fixture's cells in data order (unrotated row by row), each with its place on the floor.</summary>
    private static IEnumerable<(int Index, GridPoint Cell)> CellsOf(FixtureDef def, PlacedFixture f)
    {
        var i = 0;
        for (var y = 0; y < def.Size.H; y++)
        for (var x = 0; x < def.Size.W; x++)
        {
            var turned = LayoutRules.Rotate(new GridPoint(x, y), def.Size, f.Rotation);
            yield return (i++, new GridPoint(f.Origin.X + turned.X, f.Origin.Y + turned.Y));
        }
    }

    private void DrawFixture(PlacedFixture f)
    {
        var def = Def(f);
        if (_art.Get(def.Sprite?.Main) is null)
        {
            DrawFixtureBlock(f, def);
            return;
        }

        var sprite = def.Sprite!;
        var cells = CellsOf(def, f).ToList();
        var slotsPerCell = (int)Math.Ceiling(def.Capacity / (double)cells.Count);
        foreach (var (index, cell) in cells)
        {
            var name = sprite.Main;
            if (DataValidator.IsDisplay(def.Kind))
            {
                var stocked = Enumerable.Range(index * slotsPerCell, slotsPerCell)
                    .Any(slot => _host.Sim.Shelves.ProductAt(new StockLocation(f.InstanceId, slot)) is { } p && _host.Sim.Inventory.Count(new StockLocation(f.InstanceId, slot), p) > 0);
                name = !stocked ? sprite.Empty ?? sprite.Main : (cell.X + cell.Y) % 2 == 1 ? sprite.Alternate ?? sprite.Main : sprite.Main;
            }
            else if (index > 0)
            {
                name = sprite.Rest ?? sprite.Main;
            }

            DrawSprite(_art.Get(name) ?? _art.Get(sprite.Main)!, TopLeftOf(cell), f.Rotation);
        }

        if (_art.Get(sprite.Decoration) is { } ornament)
        {
            // Sits on the last counter cell, poking up above it.
            DrawSprite(ornament, TopLeftOf(cells[^1].Cell) + new Vector2(4, -14));
        }

        if (_ui.SelectedInstanceId == f.InstanceId && _ui.Mode == ToolMode.Build)
        {
            DrawRect(Area(f.Origin, LayoutRules.RotatedSize(def, f.Rotation), 1), Palette.Selection, filled: false, width: 1);
        }
    }

    /// <summary>Fallback for fixtures without art: a coloured block (storage areas show how full they are).</summary>
    private void DrawFixtureBlock(PlacedFixture f, FixtureDef def)
    {
        var rect = Area(f.Origin, LayoutRules.RotatedSize(def, f.Rotation), 2);
        DrawRect(rect, Palette.Fixture(def.Kind));
        DrawRect(rect, _ui.SelectedInstanceId == f.InstanceId && _ui.Mode == ToolMode.Build ? Palette.Selection : Palette.Outline, filled: false, width: 1);
        if (DataValidator.IsStorageArea(def.Kind)) DrawStorageFill(f, def, rect);
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

    /// <summary>Preview of the fixture being placed or moved: its sprites over a green (fits) or red (does not) footprint.</summary>
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
        var tint = ok ? Palette.GhostOk : Palette.GhostBad;
        DrawRect(Area(cell, LayoutRules.RotatedSize(def, _ui.GhostRotation)), tint);
        if (_art.Get(def.Sprite?.Main) is { } texture)
        {
            foreach (var (_, c) in CellsOf(def, candidate)) DrawSprite(texture, TopLeftOf(c), candidate.Rotation, modulate: new Color(1, 1, 1, 0.6f));
        }

        foreach (var c in LayoutRules.InteractionCells(def, cell, _ui.GhostRotation)) DrawRect(Area(c, new GridSize(1, 1), 10), Palette.WorkCell);
    }

    /// <summary>Outlines where the goods the player carries should go: the shelf slot or the cutting table.</summary>
    private void DrawCarryTarget()
    {
        var sim = _host.Sim;
        if (sim.Player.Step != WorkStep.Carrying || sim.Player.TaskId is not { } id) return;

        var (instanceId, slot) = sim.Tasks.Find(id) switch
        {
            RestockTask r => (r.Target.FixtureInstanceId, (int?)r.Target.Slot),
            CutMeatTask c => (c.CuttingTableInstanceId, null),
            _ => (0, null),
        };
        if (sim.Layout.Find(instanceId) is not { } f) return;

        var def = Def(f);
        var rect = Area(f.Origin, LayoutRules.RotatedSize(def, f.Rotation), 1);
        if (slot is { } s)
        {
            // The cell that holds this slot.
            var cells = CellsOf(def, f).ToList();
            var perCell = (int)Math.Ceiling(def.Capacity / (double)cells.Count);
            rect = Area(cells[Math.Min(cells.Count - 1, s / perCell)].Cell, new GridSize(1, 1), 1);
        }

        var pulse = 0.6f + 0.4f * Mathf.Sin((float)Time.GetTicksMsec() / 150f);
        DrawRect(rect, Palette.Selection with { A = pulse }, filled: false, width: 2);
    }

    private Vector2 CenterOf(GridPoint cell) => new(cell.X * Cell + Cell / 2f, cell.Y * Cell + Cell / 2f);

    private bool IsWalking(string key) =>
        _before.TryGetValue(key, out var was) && _after.TryGetValue(key, out var now) && was != now;

    /// <summary>One front-facing frame: walking is a small hop, mirrored when heading left.</summary>
    private void DrawCharacter(string key, Vector2 center, SpriteDef? sprite, Color fallback)
    {
        var hop = 0f;
        if (IsWalking(key) && _feel.WalkBounceSeconds > 0)
        {
            var phase = (float)(Time.GetTicksMsec() / 1000.0 / _feel.WalkBounceSeconds) * Mathf.Pi;
            hop = -Mathf.Abs(Mathf.Sin(phase)) * _feel.WalkBounceHeight;
        }

        var topLeft = center - new Vector2(Cell, Cell) / 2 + new Vector2(0, hop);
        if (_art.Get(sprite?.Main) is { } texture)
        {
            DrawSprite(texture, topLeft, flipX: _facingLeft.Contains(key), modulate: Art.ParseTint(sprite!.Tint));
        }
        else
        {
            DrawCircle(center + new Vector2(0, hop), 6, fallback);
            DrawArc(center + new Vector2(0, hop), 6, 0, Mathf.Tau, 12, Palette.Outline, 1);
        }
    }

    private void DrawProductIcon(string productId, Vector2 center, float size)
    {
        var product = Data.Products.Single(p => p.Id == productId);
        var rect = new Rect2(center - new Vector2(size, size) / 2, new Vector2(size, size));
        if (_art.Get(product.Sprite) is { } icon) DrawTextureRect(icon, rect, false);
        else DrawRect(rect.Grow(-size / 4), Palette.Department(product.Department));
    }

    private void DrawCustomer(Sim.Customers.Customer c)
    {
        var types = Data.CustomerTypes.ToList();
        var type = types.Single(t => t.Id == c.TypeId);

        // Spread customers who share a cell so a queue reads as a group.
        var offset = new Vector2((c.Id % 3 - 1) * 6, (c.Id / 3 % 3 - 1) * 3);
        var key = "c" + c.Id;
        var at = DrawPosition(key, c.Position) + offset;
        DrawCharacter(key, at, type.Sprite, Palette.Customer(types.IndexOf(type), c.State));
        if (c.Basket.Count > 0) DrawProductIcon(c.Basket[^1].Lot.ProductId, at + new Vector2(9, 6), 12);
    }

    private void DrawWorker(Worker w)
    {
        var key = "w" + w.Id;
        var at = DrawPosition(key, w.Position);
        var sprite = w.IsPlayer ? Data.Employees.Player.Sprite : Data.Employees.Roles.Single(r => r.Id == w.RoleId).Sprite;
        DrawCharacter(key, at, sprite, w.IsPlayer ? Palette.Player : Palette.Staff);

        if (w.IsPlayer)
        {
            // Facing marker, under the feet.
            DrawRect(new Rect2(at + new Vector2(_facing.X * 12 - 2, 13 + _facing.Y * 2), new Vector2(4, 2)), Palette.Selection);
        }

        if (w.Carrying.Count > 0) DrawProductIcon(w.Carrying[0].ProductId, at + new Vector2(0, -18), 14);

        if (w.IsWorking && w.WorkTicksLeft > 0)
        {
            DrawRect(new Rect2(at + new Vector2(-10, 14), new Vector2(20, 3)), Palette.Outline);
            DrawRect(new Rect2(at + new Vector2(-10, 14), new Vector2(20f / (w.WorkTicksLeft + 1), 3)), Palette.Progress);
        }
    }
}
