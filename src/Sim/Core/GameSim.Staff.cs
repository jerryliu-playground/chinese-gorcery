using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Tasks;

namespace ChineseGrocery.Sim.Core;

/// <summary>Staff: roles, speed and experience.</summary>
public sealed partial class GameSim
{
    private RoleDef? RoleOf(Worker worker) => null;

    private int EffectiveSpeed(Worker worker) => worker.WorkSpeedPermille;

    private void GainExperience(Worker worker)
    {
    }
}
