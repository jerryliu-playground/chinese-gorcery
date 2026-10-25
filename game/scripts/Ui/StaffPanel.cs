using ChineseGrocery.Sim.Staff;
using Godot;

namespace ChineseGrocery.Game.Ui;

/// <summary>Hire staff and set wages (before opening). Wage against the market wage sets morale, which sets work speed.</summary>
public partial class StaffPanel : PanelBase
{
    private int _shownStaff = -1;

    protected override void Rebuild()
    {
        SetTitle(UiText.T("ui.staff.title"));
        ClearBody();
        var sim = Host.Sim;
        _shownStaff = sim.Workers.Count;

        var roles = Host.Data.Employees.Roles;
        if (!roles.Any(r => r.UnlockStage <= sim.Economy.Stage)) Body.AddChild(Text(UiText.T("ui.staff.none_unlocked")));

        foreach (var role in roles.Where(r => r.UnlockStage <= sim.Economy.Stage))
        {
            var hired = sim.Workers.Count(w => w.RoleId == role.Id);
            Body.AddChild(Row(
                Text(UiText.T(role.NameKey), 90),
                Text(UiText.F("ui.staff.market_wage", ("wage", UiText.Money(role.MarketWageCentsPerDay))), 140),
                ActionButton("ui.staff.hire", () => Host.Send(new HireEmployee(role.Id)), hired < role.MaxCount)));
        }

        foreach (var worker in sim.Workers.Where(w => w.RoleId is not null))
        {
            var role = roles.Single(r => r.Id == worker.RoleId);
            Body.AddChild(Text(UiText.F("ui.staff.stats",
                ("role", UiText.T(role.NameKey)), ("level", worker.Level),
                ("skill_name", UiText.T(role.SkillNameKey ?? "ui.staff.skill.knife")), ("knife", worker.KnifeSkill),
                ("speed", worker.WorkSpeedPermille / 10), ("morale", worker.MoralePermille / 10))));

            var wage = new SpinBox { MinValue = 0, MaxValue = 9999, Step = 1, Prefix = "$", Value = worker.WageCentsPerDay / 100.0, CustomMinimumSize = new Vector2(80, 0) };
            Body.AddChild(Row(
                Text(UiText.T("ui.staff.wage"), 90),
                wage,
                ActionButton("ui.staff.set_wage", () => Host.Send(new SetWage(worker.Id, (long)Math.Round(wage.Value * 100))))));
        }
    }

    protected override void Refresh()
    {
        if (Host.Sim.Workers.Count != _shownStaff) Rebuild();
    }
}
