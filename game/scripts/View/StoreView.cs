using ChineseGrocery.Game.Ui;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Deli;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Produce;
using ChineseGrocery.Sim.Seafood;
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
    private readonly Effects _effects = new() { Name = "Effects" };

    /// <summary>Developer screenshot tool only: pretend the mouse is over this cell.</summary>
    public GridPoint? DebugHover { get; set; }

    /// <summary>Height of the wall strip drawn above the top row (up to one cell), set by the layout.</summary>
    public int WallHeight { get; set; } = Cell;

    /// <summary>The part of the store on screen, in local coordinates; cells outside it cannot be hovered or clicked.</summary>
    public Rect2? VisibleArea { get; set; }

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
        AddChild(_effects);
        _effects.Init(host, feel, this);
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
        if (VisibleArea is { } visible && !visible.HasPoint(local)) return null;

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
                if (DataValidator.IsDisplay(Def(fixture).Kind) || DataValidator.IsDeliStation(Def(fixture).Kind)) DisplayClicked?.Invoke(fixture.InstanceId);
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
        if (_ui.Mode == ToolMode.Play) DrawPriceTag();
    }

    /// <summary>Hovering a shelf cell shows its slots' products, prices and stock (names in the UI language).</summary>
    private void DrawPriceTag()
    {
        if (_hover is not { } cell || FixtureAt(cell) is not { } f || !(DataValidator.IsDisplay(Def(f).Kind) || DataValidator.IsDeliStation(Def(f).Kind))) return;

        var def = Def(f);
        var cells = CellsOf(def, f).ToList();
        var perCell = (int)Math.Ceiling(def.Capacity / (double)cells.Count);
        var index = cells.First(c => c.Cell == cell).Index;
        var rows = Enumerable.Range(index * perCell, perCell)
            .Select(slot => new StockLocation(f.InstanceId, slot))
            .Where(slot => SlotProduct(slot) is not null)
            .Select(slot =>
            {
                var product = Data.Products.Single(p => p.Id == SlotProduct(slot));
                var stock = UiText.F("ui.tag.stock", ("have", _host.Sim.Inventory.Count(slot, product.Id)), ("max", product.UnitsPerShelfSlot));
                return ((string?)product.Id, Text: $"{UiText.T(product.NameKey)}  {UiText.Money(_host.Sim.Prices.PriceOf(product.Id))}  {stock}{FreshnessNote(slot, product)}");
            })
            .ToList();
        if (def.Kind == FixtureKind.FishTank) rows.AddRange(TankLines(f).Select(line => ((string?)null, line)));
        if (def.Kind == FixtureKind.ProduceStand)
        {
            var score = _host.Sim.DisplayScoreOf(f.InstanceId);
            rows.Add((null, UiText.F("ui.display.score", ("score", score.Total))));
            rows.Add((null, string.Join("  ", new[]
            {
                ("ui.display.fullness", score.Fullness), ("ui.display.freshness", score.Freshness),
                ("ui.display.tidiness", score.Tidiness), ("ui.display.pairing", score.Pairing),
            }.Select(part => UiText.F("ui.display.part", ("name", UiText.T(part.Item1)), ("value", part.Item2))))));
        }

        if (rows.Count == 0) return;

        var font = UiTheme.Font();
        const int lineHeight = 14;
        var width = rows.Max(r => font.GetStringSize(r.Text, HorizontalAlignment.Left, -1, UiTheme.FontSize).X) + 22;
        var height = rows.Count * lineHeight + 4;
        var floor = new Vector2(_host.Sim.Layout.Size.W * Cell, _host.Sim.Layout.Size.H * Cell);
        // Above the sale card or bad-water icon these may be wearing.
        var lift = def.Kind is FixtureKind.ProduceStand or FixtureKind.IceTable or FixtureKind.FishTank ? 14 : 0;
        var at = new Vector2(cell.X * Cell + Cell / 2f - width / 2, cell.Y * Cell - height - 2 - lift);
        at = new Vector2(Mathf.Clamp(at.X, 0, floor.X - width), at.Y < 0 ? (cell.Y + 1) * Cell + 2 : at.Y);

        var box = new Rect2(at, new Vector2(width, height));
        DrawRect(box, new Color(1, 0.97f, 0.88f, 0.95f));
        DrawRect(box, new Color(0.75f, 0.2f, 0.16f), filled: false, width: 1);
        for (var i = 0; i < rows.Count; i++)
        {
            var y = at.Y + 2 + i * lineHeight;
            if (rows[i].Item1 is { } productId) DrawIconAt(this, productId, new Vector2(at.X + 9, y + lineHeight / 2f), 12, 1);
            DrawString(font, new Vector2(at.X + 18, y + 11), rows[i].Text, HorizontalAlignment.Left, -1, UiTheme.FontSize, new Color(0.25f, 0.12f, 0.1f));
        }
    }

    /// <summary>Water quality, how many are in the tank (and dead), and the death risk tonight of its most fragile animal.</summary>
    private IEnumerable<string> TankLines(PlacedFixture tank)
    {
        var sim = _host.Sim;
        var quality = sim.WaterQualityOf(tank.InstanceId);
        var live = sim.Inventory.Locations.Where(p => p.Location.FixtureInstanceId == tank.InstanceId).SelectMany(p => p.Lots).ToList();
        var dead = sim.DeadIn(tank.InstanceId).Sum(l => l.Quantity);
        var line = $"{UiText.F("ui.water.quality", ("value", quality))}  {UiText.F("ui.water.count", ("count", live.Sum(l => l.Quantity)))}";
        if (dead > 0) line += "  " + UiText.F("ui.water.dead", ("count", dead));
        yield return line;

        if (Data.Economy.Seafood is not { } settings || live.Count == 0) yield break;

        var risk = live.Select(l => Data.Products.Single(p => p.Id == l.ProductId)).Where(p => p.Live is not null)
            .Max(p => Mortality.ChancePermille(settings, p, quality));
        yield return UiText.F("ui.water.risk", ("risk", (risk / 10.0).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)));
    }

    /// <summary>What a slot sells: its shelf assignment, or for a deli station today's menu item.</summary>
    private string? SlotProduct(StockLocation slot) => _host.Sim.Shelves.ProductAt(slot) ?? _host.Sim.Menu.GetValueOrDefault(slot)?.DishId;

    /// <summary>For produce: the freshest batch's tier, plus the sale price if a batch is marked down to clear.</summary>
    private string FreshnessNote(StockLocation slot, ProductDef product)
    {
        if (product.FreshnessDays is null || _host.Sim.Inventory.TryGet(slot) is not { } pile) return "";

        var lots = pile.Lots.Where(l => l.ProductId == product.Id).ToList();
        if (lots.Count == 0) return "";

        var day = _host.Sim.Clock.Day;
        var freshest = lots.MaxBy(l => l.BestBeforeDay)!;
        var note = "  " + UiText.T("ui.freshness." + Freshness.TierOf(product, freshest, day).ToString().ToLowerInvariant());
        if (lots.Where(l => Freshness.TierOf(product, l, day) >= FreshnessTier.Ripe).MinBy(_host.Sim.EffectivePrice) is { } sale)
        {
            note += $"  {UiText.T("ui.tag.sale")} {UiText.Money(_host.Sim.EffectivePrice(sale))}";
        }

        return note;
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
            if (def.Kind == FixtureKind.ProduceStand)
            {
                // The crates are filled below with the actual produce.
                name = sprite.Empty ?? sprite.Main;
            }
            else if (DataValidator.IsDeliStation(def.Kind))
            {
                // Steam table trays are filled below with the dishes actually there.
                var stocked = _host.Sim.Inventory.Holds(f.InstanceId);
                name = def.Kind == FixtureKind.SteamTable || !stocked ? sprite.Empty ?? sprite.Main : sprite.Main;
            }
            else if (def.Kind == FixtureKind.FishTank)
            {
                var stocked = _host.Sim.Inventory.Holds(f.InstanceId) || _host.Sim.DeadIn(f.InstanceId).Count > 0;
                name = !stocked ? sprite.Empty ?? sprite.Main : IsWaterBad(f.InstanceId) ? sprite.Dirty ?? sprite.Main : sprite.Main;
            }
            else if (DataValidator.IsDisplay(def.Kind))
            {
                var stocked = Enumerable.Range(index * slotsPerCell, slotsPerCell)
                    .Any(slot => _host.Sim.Shelves.ProductAt(new StockLocation(f.InstanceId, slot)) is { } p && _host.Sim.Inventory.Count(new StockLocation(f.InstanceId, slot), p) > 0);
                name = !stocked ? sprite.Empty ?? sprite.Main : (cell.X + cell.Y) % 2 == 1 ? sprite.Alternate ?? sprite.Main : sprite.Main;
            }
            else if (index > 0)
            {
                name = sprite.Rest ?? sprite.Main;
            }

            var (scale, lift, flash) = _effects.FixtureFeel(f.InstanceId);
            var glow = 1 + flash * 0.8f;
            DrawSprite(_art.Get(name) ?? _art.Get(sprite.Main)!, TopLeftOf(cell) + new Vector2(0, lift), f.Rotation, modulate: new Color(glow, glow, glow), scale: scale);
        }

        if (def.Kind == FixtureKind.CuttingTable) DrawChopping(f, cells[0].Cell);
        if (def.Kind == FixtureKind.SeafoodCounter) DrawChopping(f, cells[0].Cell);
        if (def.Kind == FixtureKind.FishTank) DrawTankExtras(f, TopLeftOf(cells[0].Cell));
        if (def.Kind == FixtureKind.SteamTable) DrawTrays(f, def, TopLeftOf(cells[0].Cell));
        if (def.Kind == FixtureKind.WokStove) DrawWokFire(f, TopLeftOf(cells[0].Cell));
        if (def.Kind == FixtureKind.IceTable && HasMarkdown(f, def)) DrawSaleCard(TopLeftOf(cells[0].Cell));
        if (def.Kind == FixtureKind.ProduceStand) DrawProduce(f, def, TopLeftOf(cells[0].Cell) + new Vector2(0, _effects.FixtureFeel(f.InstanceId).Lift));

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

    /// <summary>Left edge of each crate on the produce stand sprite, one per slot (tools/art/make_sprites.py).</summary>
    private static readonly int[] CrateX = [2, 17];

    private const int PileRows = 4;
    private const int PileColumns = 3;

    /// <summary>
    /// Fills each crate with piles in the product's colour, as many as the slot is full, oldest batch on top and tinted
    /// by its freshness. A messy stand has its piles knocked about and bits on the floor; a marked-down batch hangs a
    /// sale card over the stand; a high display score sparkles now and then.
    /// </summary>
    private void DrawProduce(PlacedFixture f, FixtureDef def, Vector2 topLeft)
    {
        var sim = _host.Sim;
        var messy = sim.TidinessOf(f.InstanceId) < (Data.Economy.Display?.TidyBelow ?? 0);
        var onSale = false;
        for (var slot = 0; slot < Math.Min(def.Capacity, CrateX.Length); slot++)
        {
            var location = new StockLocation(f.InstanceId, slot);
            if (sim.Shelves.ProductAt(location) is not { } productId) continue;

            var product = Data.Products.Single(p => p.Id == productId);
            var lots = (sim.Inventory.TryGet(location)?.Lots ?? []).Where(l => l.ProductId == productId).OrderByDescending(l => l.BestBeforeDay).ToList();
            var units = lots.Sum(l => l.Quantity);
            if (units == 0) continue;

            var tiers = lots.Select(l => (Tier: Freshness.TierOf(product, l, sim.Clock.Day), l.Quantity)).ToList();
            onSale |= tiers.Any(t => t.Tier >= FreshnessTier.Ripe);
            var piles = (int)Math.Ceiling(Math.Min(units, product.UnitsPerShelfSlot) * PileRows * PileColumns / (double)product.UnitsPerShelfSlot);
            var baseColour = Palette.Produce(product.Color);
            for (var i = 0; i < piles; i++)
            {
                // Fill bottom row first; pile i stands for the units at that share of the slot, freshest at the bottom.
                var unitAt = (long)i * units / piles;
                var tier = FreshnessTier.Fresh;
                foreach (var t in tiers)
                {
                    tier = t.Tier;
                    if ((unitAt -= t.Quantity) < 0) break;
                }

                var row = PileRows - 1 - i / PileColumns;
                var at = topLeft + new Vector2(CrateX[slot] + 2 + i % PileColumns * 3, 6 + row * 3);
                if (messy) at += new Vector2(Wobble(f.InstanceId, slot, i, 0), Wobble(f.InstanceId, slot, i, 1)) * _feel.MessJitter;
                DrawPile(at, Palette.Aged(baseColour, tier));
            }

            if (messy)
            {
                var colour = Palette.Aged(baseColour, tiers[^1].Tier);
                DrawRect(new Rect2(topLeft + new Vector2(CrateX[slot] + 1, 30), new Vector2(2, 1)), colour);
                DrawRect(new Rect2(topLeft + new Vector2(CrateX[slot] + 9, 31), new Vector2(2, 1)), colour);
                DrawRect(new Rect2(topLeft + new Vector2(CrateX[slot] + 13, 19), new Vector2(1, 2)), colour);
            }
        }

        if (onSale) DrawSaleCard(topLeft);
        DrawSparkle(f, topLeft);
    }

    private void DrawPile(Vector2 at, Color colour)
    {
        DrawRect(new Rect2(at, new Vector2(3, 3)), colour);
        DrawRect(new Rect2(at + new Vector2(2, 2), Vector2.One), colour.Darkened(0.35f));
        DrawRect(new Rect2(at, Vector2.One), colour.Lightened(0.5f));
    }

    /// <summary>A fixed -1, 0 or 1 per pile, so a messy stand looks the same from frame to frame.</summary>
    private static int Wobble(int instanceId, int slot, int pile, int axis) =>
        (int)((uint)(instanceId * 73856093 ^ slot * 19349663 ^ pile * 83492791 ^ axis * 2654435761u) % 3) - 1;

    private void DrawSaleCard(Vector2 topLeft)
    {
        var font = UiTheme.Font();
        var text = UiText.T("ui.tag.sale");
        var width = font.GetStringSize(text, HorizontalAlignment.Left, -1, UiTheme.FontSize).X + 4;
        var card = new Rect2(topLeft + new Vector2((Cell - width) / 2, -11), new Vector2(width, 13));
        DrawRect(card, Palette.SaleTag);
        DrawRect(card, Palette.Outline, filled: false, width: 1);
        DrawString(font, card.Position + new Vector2(2, 11), text, HorizontalAlignment.Left, -1, UiTheme.FontSize, Colors.White);
    }

    public bool IsSparkling(int standInstanceId)
    {
        if (!_effects.MinorEffectsOn || _host.Sim.DisplayScoreOf(standInstanceId).Total < _feel.SparkleScore) return false;

        return (_effects.Clock + standInstanceId * 0.77f) % Mathf.Max(0.1f, _feel.SparklePeriodSeconds) < _feel.SparkleSeconds;
    }

    /// <summary>A brief four-point glint on a well-kept stand, at a different spot each time.</summary>
    private void DrawSparkle(PlacedFixture f, Vector2 topLeft)
    {
        if (!IsSparkling(f.InstanceId)) return;

        var period = Mathf.Max(0.1f, _feel.SparklePeriodSeconds);
        var clock = _effects.Clock + f.InstanceId * 0.77f;
        var t = clock % period;
        var round = (int)(clock / period);
        var at = topLeft + new Vector2(4 + (uint)(round * 2654435761u + (uint)f.InstanceId) % 24, 4 + (uint)(round * 40503u) % 12);
        var size = Mathf.Sin(t / _feel.SparkleSeconds * Mathf.Pi) * 3;
        var glint = new Color(1, 0.9f, 0.4f);
        DrawLine(at - new Vector2(size, 0), at + new Vector2(size, 0), glint, 1);
        DrawLine(at - new Vector2(0, size), at + new Vector2(0, size), glint, 1);
        DrawRect(new Rect2(at - Vector2.One, new Vector2(2, 2)), Colors.White);
    }

    /// <summary>Left edge of each tray on the steam table sprite, one per slot (tools/art/make_sprites.py).</summary>
    private static readonly int[] TrayX = [2, 9, 16, 23];

    /// <summary>Each tray with food shows its dish's icon.</summary>
    private void DrawTrays(PlacedFixture table, FixtureDef def, Vector2 topLeft)
    {
        for (var slot = 0; slot < Math.Min(def.Capacity, TrayX.Length); slot++)
        {
            var dish = _host.Sim.Inventory.TryGet(new StockLocation(table.InstanceId, slot))?.Lots.FirstOrDefault()?.ProductId;
            if (dish is not null) DrawIconAt(this, dish, topLeft + new Vector2(TrayX[slot] + 3, 13), 9, 1);
        }
    }

    /// <summary>While someone cooks at this stove: a flickering flame and rising steam.</summary>
    private void DrawWokFire(PlacedFixture stove, Vector2 topLeft)
    {
        var cooking = _host.Sim.Workers.Any(w => w.Step == WorkStep.Cooking && w.TaskId is { } id && _host.Sim.Tasks.Find(id) is CookTask c && c.StoveInstanceId == stove.InstanceId);
        if (!cooking) return;

        var t = _effects.Clock;
        var flicker = Mathf.Sin(t * 20) * 1.5f;
        DrawRect(new Rect2(topLeft + new Vector2(4, 12 - flicker), new Vector2(10, 3 + flicker)), new Color(1f, 0.55f, 0.15f));
        DrawRect(new Rect2(topLeft + new Vector2(18, 12 + flicker), new Vector2(10, 3 - flicker)), new Color(1f, 0.55f, 0.15f));
        for (var i = 0; i < 3; i++)
        {
            var rise = (t * 8 + i * 5) % 12;
            DrawRect(new Rect2(topLeft + new Vector2(7 + i * 8, 6 - rise), new Vector2(2, 2)), new Color(1, 1, 1, 1 - rise / 12));
        }
    }

    private bool IsWaterBad(int tankInstanceId) =>
        _host.Sim.WaterQualityOf(tankInstanceId) < (Data.Economy.Seafood?.CleanBelow ?? 0);

    /// <summary>Dead animals floating belly-up at the surface (up to three), and the bad-water icon above a dirty tank.</summary>
    private void DrawTankExtras(PlacedFixture tank, Vector2 topLeft)
    {
        var dead = Math.Min(3, _host.Sim.DeadIn(tank.InstanceId).Sum(l => l.Quantity));
        for (var i = 0; i < dead; i++)
        {
            var at = topLeft + new Vector2(5 + i * 8, 7);
            DrawRect(new Rect2(at, new Vector2(6, 2)), new Color(0.82f, 0.82f, 0.78f));
            DrawRect(new Rect2(at + new Vector2(6, 0), new Vector2(1, 2)), new Color(0.6f, 0.6f, 0.58f));
        }

        if (IsWaterBad(tank.InstanceId) && _art.Get("ui_water_bad") is { } icon)
        {
            DrawTextureRect(icon, new Rect2(topLeft + new Vector2(8, -16), new Vector2(16, 16)), false);
        }
    }

    /// <summary>Whether a display holds a batch marked down to clear (ripe), which earns it a sale card.</summary>
    private bool HasMarkdown(PlacedFixture f, FixtureDef def) =>
        Enumerable.Range(0, def.Capacity).Any(slot => _host.Sim.Inventory.TryGet(new StockLocation(f.InstanceId, slot)) is { } pile
            && pile.Lots.Any(l => Data.Products.Single(p => p.Id == l.ProductId) is { FreshnessDays: not null } product
                                  && Freshness.TierOf(product, l, _host.Sim.Clock.Day) >= FreshnessTier.Ripe));

    /// <summary>While someone cuts at this table: a cleaver rising and falling, then a short pause.</summary>
    private void DrawChopping(PlacedFixture table, GridPoint cell)
    {
        var cutting = _host.Sim.Workers.Any(w => w.TaskId is { } id && _host.Sim.Tasks.Find(id) switch
        {
            CutMeatTask c => w.Step == WorkStep.Cutting && c.CuttingTableInstanceId == table.InstanceId,
            KillFishTask k => w.Step == WorkStep.Killing && k.CounterInstanceId == table.InstanceId,
            _ => false,
        });
        if (!cutting) return;

        var period = Mathf.Max(0.05f, _feel.ChopSeconds);
        var t = _effects.Clock % period / period;
        var swing = t < 0.6f ? Mathf.Sin(t / 0.6f * Mathf.Pi) : 0;
        var at = TopLeftOf(cell) + new Vector2(18, 4 - swing * 8);
        DrawRect(new Rect2(at, new Vector2(8, 6)), new Color(0.78f, 0.8f, 0.84f));
        DrawRect(new Rect2(at, new Vector2(8, 6)), Palette.Outline, filled: false, width: 1);
        DrawRect(new Rect2(at + new Vector2(8, 2), new Vector2(5, 2)), new Color(0.35f, 0.22f, 0.15f));
    }

    /// <summary>Where pop-ups over a fixture start: centre of its top edge.</summary>
    public Vector2 FixtureTop(int instanceId)
    {
        if (_host.Sim.Layout.Find(instanceId) is not { } f) return Vector2.Zero;

        var size = LayoutRules.RotatedSize(Def(f), f.Rotation);
        return new Vector2((f.Origin.X + size.W / 2f) * Cell, f.Origin.Y * Cell - 2);
    }

    /// <summary>Just above a customer's head, where they are drawn now; null once they have left.</summary>
    public Vector2? CustomerHead(int customerId) =>
        _host.Sim.Customers.FirstOrDefault(c => c.Id == customerId) is { } c ? CustomerDrawPosition(c) + new Vector2(0, -16) : null;

    private Vector2 CustomerDrawPosition(Sim.Customers.Customer c) =>
        DrawPosition("c" + c.Id, c.Position) + new Vector2((c.Id % 3 - 1) * 6, (c.Id / 3 % 3 - 1) * 3);

    /// <summary>Draws a product's icon (or a colour chip if it has none) on another canvas item, e.g. the effects layer.</summary>
    public void DrawIconAt(CanvasItem canvas, string productId, Vector2 center, float size, float alpha)
    {
        var product = Data.Products.Single(p => p.Id == productId);
        var rect = new Rect2(center - new Vector2(size, size) / 2, new Vector2(size, size));
        if (_art.Get(product.Sprite) is { } icon) canvas.DrawTextureRect(icon, rect, false, new Color(1, 1, 1, alpha));
        else canvas.DrawRect(rect.Grow(-size / 4), Palette.Department(product.Department) with { A = alpha });
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
        if (_art.Get(sprite?.Main, sprite?.Recolor) is { } texture)
        {
            DrawSprite(texture, topLeft, flipX: _facingLeft.Contains(key));
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

        // Customers who share a cell are spread a little so a queue reads as a group.
        var key = "c" + c.Id;
        var at = CustomerDrawPosition(c);
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
