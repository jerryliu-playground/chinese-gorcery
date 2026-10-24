using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;

namespace ChineseGrocery.Sim.Tasks;

/// <summary>
/// Anyone who executes tasks: the player character or a staff member. Both run the same task logic;
/// the player just does not claim tasks automatically.
/// </summary>
public sealed class Worker(int id, bool isPlayer, GridPoint position, int moveCellsPerMinute, int workSpeedPermille, int knifeSkill)
{
    private readonly List<GridPoint> _path = [];
    private readonly List<StockLot> _carrying = [];

    public int Id { get; } = id;
    public bool IsPlayer { get; } = isPlayer;
    public GridPoint Position { get; internal set; } = position;
    public IReadOnlyList<GridPoint> Path => _path;
    public int MoveCellsPerMinute { get; } = moveCellsPerMinute;

    /// <summary>Walking carried over between ticks, in cell-seconds (60 = one cell).</summary>
    public int MoveProgress { get; internal set; }
    public int WorkSpeedPermille { get; internal set; } = workSpeedPermille;
    public int KnifeSkill { get; internal set; } = knifeSkill;

    /// <summary>The staff role, or null for the player.</summary>
    public string? RoleId { get; internal init; }
    public long WageCentsPerDay { get; internal set; }
    public int MoralePermille { get; internal set; }
    public int Level { get; internal set; } = 1;
    public int Xp { get; internal set; }
    public int? TaskId { get; internal set; }
    public WorkStep Step { get; internal set; } = WorkStep.Idle;
    public int WorkTicksLeft { get; internal set; }
    public int? ServingCustomerId { get; internal set; }
    public IReadOnlyList<StockLot> Carrying => _carrying;

    /// <summary>In a timed step (picking, shelving, serving) and unable to walk away.</summary>
    public bool IsWorking => Step is WorkStep.Picking or WorkStep.Shelving or WorkStep.Serving or WorkStep.Cutting or WorkStep.Tidying;

    internal void SetPath(IEnumerable<GridPoint> path)
    {
        _path.Clear();
        _path.AddRange(path);
    }

    internal void PopStep() => _path.RemoveAt(0);

    internal void Carry(IEnumerable<StockLot> lots) => _carrying.AddRange(lots);

    internal List<StockLot> DropAll()
    {
        var lots = _carrying.ToList();
        _carrying.Clear();
        return lots;
    }

    internal IReadOnlyList<StockLot> RemoveExpired(int closingDay)
    {
        var expired = _carrying.Where(l => l.BestBeforeDay <= closingDay).ToList();
        _carrying.RemoveAll(l => l.BestBeforeDay <= closingDay);
        return expired;
    }
}
