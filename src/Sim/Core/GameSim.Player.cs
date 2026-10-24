using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Tasks;

namespace ChineseGrocery.Sim.Core;

/// <summary>Player commands: walking and interacting. Interacting runs the same task logic staff use.</summary>
public sealed partial class GameSim
{
    private Rejection? HandleMovePlayer(MovePlayer move)
    {
        if (Player.IsWorking) return Reject(RejectReason.Busy);
        if (GridPaths.FindPath(IsWalkable, Player.Position, move.Target) is not { } path) return Reject(RejectReason.Unreachable);

        Player.SetPath(path);
        return null;
    }

    private Rejection? HandlePlayerInteract(PlayerInteract interact)
    {
        if (Player.IsWorking || Player.Path.Count > 0) return Reject(RejectReason.Busy);

        var here = Layout.Fixtures
            .Where(f => LayoutRules.InteractionCells(_fixtures[f.FixtureId], f.Origin, f.Rotation).Contains(Player.Position))
            .ToList();
        if (interact.FixtureInstanceId is { } id)
        {
            here = here.Where(f => f.InstanceId == id).ToList();
            if (here.Count == 0) return Reject(RejectReason.NotAtFixture);
        }

        return here.Any(f => TryStartWorkAt(Player, f)) ? null : Reject(RejectReason.NothingToDo);
    }
}
