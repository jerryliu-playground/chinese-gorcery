using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Staff;
using ChineseGrocery.Sim.Tasks;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Core;

/// <summary>Staff: hiring, wages and morale, experience. Their task-taking lives with the task logic.</summary>
public sealed partial class GameSim
{
    private Rejection? HandleHireEmployee(HireEmployee hire)
    {
        if (Clock.Phase != DayPhase.PreOpen) return Reject(RejectReason.WrongPhase);
        if (Data.Employees.Roles.FirstOrDefault(r => r.Id == hire.RoleId) is not { } role) return Reject(RejectReason.UnknownRole);
        if (role.UnlockStage > Economy.Stage) return Reject(RejectReason.RoleLocked);
        if (_workers.Count(w => w.RoleId == role.Id) >= RoleLimit(role)) return Reject(RejectReason.RoleFull);

        var knife = Rng.NextInt(role.KnifeSkill.Min, role.KnifeSkill.Max + 1);
        var speed = Rng.NextInt(role.SpeedPermille.Min, role.SpeedPermille.Max + 1);
        AddStaff(role, knife, speed, role.MarketWageCentsPerDay);
        return null;
    }

    private void AddStaff(RoleDef role, int skill, int speed, long wageCentsPerDay)
    {
        var worker = new Worker(_workers.Max(w => w.Id) + 1, isPlayer: false, PlayerStartCell(), role.MoveCellsPerMinute, speed, skill)
        {
            RoleId = role.Id,
            WageCentsPerDay = wageCentsPerDay,
        };
        RecomputeMorale(worker);
        _workers.Add(worker);
        _events.Add(new EmployeeHired(worker.Id, role.Id, skill, speed, worker.WageCentsPerDay));
    }

    private Rejection? HandleSetWage(SetWage set)
    {
        if (Clock.Phase != DayPhase.PreOpen) return Reject(RejectReason.WrongPhase);
        if (_workers.Find(w => w.Id == set.WorkerId) is not { RoleId: not null } worker) return Reject(RejectReason.UnknownWorker);
        if (set.WageCentsPerDay < 0) return Reject(RejectReason.InvalidWage);

        worker.WageCentsPerDay = set.WageCentsPerDay;
        RecomputeMorale(worker);
        _events.Add(new WageChanged(worker.Id, worker.WageCentsPerDay, worker.MoralePermille));
        return null;
    }

    private RoleDef? RoleOf(Worker worker) =>
        worker.RoleId is { } id ? Data.Employees.Roles.Single(r => r.Id == id) : null;

    /// <summary>Work speed after morale; the player has no morale.</summary>
    private int EffectiveSpeed(Worker worker) =>
        RoleOf(worker) is { } role ? Morale.Speed(role, worker.WorkSpeedPermille, worker.MoralePermille) : worker.WorkSpeedPermille;

    private void GainExperience(Worker worker)
    {
        if (RoleOf(worker) is not { } role) return;

        worker.Xp += role.XpPerTask;
        while (worker.Xp >= role.XpPerLevel && worker.Level < role.MaxLevel)
        {
            worker.Xp -= role.XpPerLevel;
            worker.Level++;
            worker.KnifeSkill += role.LevelUpKnifeSkill;
            worker.WorkSpeedPermille += role.LevelUpSpeedPermille;
            worker.MoraleAdjustPermille += Data.Employees.Morale?.LevelUpBonusPermille ?? 0;
            RecomputeMorale(worker);
            _events.Add(new EmployeeLeveledUp(worker.Id, worker.Level, worker.KnifeSkill, worker.WorkSpeedPermille));
        }
    }

    private void PayWages()
    {
        foreach (var worker in _workers.Where(w => w.RoleId is not null))
        {
            Economy.Pay(ExpenseCategory.Wages, WageFor(worker));
        }
    }
}
