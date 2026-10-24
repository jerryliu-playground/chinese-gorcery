using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Tasks;

namespace ChineseGrocery.Sim.Save;

public sealed record WorkerSave(
    int Id,
    bool IsPlayer,
    string? RoleId,
    GridPoint Position,
    IReadOnlyList<GridPoint> Path,
    int MoveCellsPerMinute,
    int MoveProgress,
    int WorkSpeedPermille,
    int KnifeSkill,
    long WageCentsPerDay,
    int MoralePermille,
    int Level,
    int Xp,
    int? TaskId,
    WorkStep Step,
    int WorkTicksLeft,
    int? ServingCustomerId,
    IReadOnlyList<StockLot> Carrying);
