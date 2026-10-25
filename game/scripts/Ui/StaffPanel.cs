using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Staff;
using ChineseGrocery.Sim.Tasks;
using ChineseGrocery.Sim.Time;
using Godot;

namespace ChineseGrocery.Game.Ui;

/// <summary>
/// Staff management: today's applicants, the staff with their shift, days off, task limits, wage and morale, a shift
/// timeline, managers and what they automate per area, and task priorities. Hiring and wages are set before opening;
/// the rest any time.
/// </summary>
public partial class StaffPanel : PanelBase
{
    private static readonly Department[] Areas = [Department.DryGoods, Department.Produce, Department.Meat, Department.Seafood, Department.Deli];

    private string _shown = "";

    protected override void Rebuild()
    {
        SetTitle(UiText.T("ui.staff.title"));
        ClearBody();
        var sim = Host.Sim;
        _shown = Signature();
        var preOpen = sim.Clock.Phase == DayPhase.PreOpen;
        var roles = Host.Data.Employees.Roles;

        Body.AddChild(Text(UiText.T("ui.staff.applicants")));
        if (sim.Applicants.Count == 0) Body.AddChild(Text("  " + UiText.T("ui.staff.no_applicants")));
        foreach (var a in sim.Applicants)
        {
            var role = roles.Single(r => r.Id == a.RoleId);
            Body.AddChild(Row(
                Text(UiText.T(role.NameKey), 70),
                Text(UiText.F("ui.staff.applicant", ("skill_name", UiText.T(role.SkillNameKey ?? "ui.staff.skill.knife")), ("skill", a.Skill),
                    ("speed", a.SpeedPermille / 10), ("wage", UiText.Money(a.AskingWageCentsPerDay))), 230),
                ActionButton("ui.staff.hire", () => Host.Send(new HireApplicant(a.Id)), preOpen),
                ActionButton("ui.staff.reject", () => Host.Send(new RejectApplicant(a.Id)))));
        }

        var staff = sim.Workers.Where(w => w.RoleId is not null).ToList();
        Body.AddChild(Text(UiText.T("ui.staff.team")));
        foreach (var worker in staff) AddWorker(worker, roles.Single(r => r.Id == worker.RoleId), preOpen);

        Body.AddChild(Text(UiText.T("ui.staff.timeline")));
        foreach (var worker in staff)
        {
            var role = roles.Single(r => r.Id == worker.RoleId);
            Body.AddChild(Row(Text(UiText.T(role.NameKey) + worker.Id, 80), ShiftBar(worker, sim.Clock.Weekday)));
        }

        Body.AddChild(Text(UiText.T("ui.staff.managers")));
        var managers = staff.Where(w => roles.Single(r => r.Id == w.RoleId).Manager).ToList();
        foreach (var area in Areas) AddArea(area, managers);

        Body.AddChild(Text(UiText.T("ui.staff.priorities")));
        foreach (var type in Enum.GetValues<TaskType>())
        {
            var spin = new SpinBox { MinValue = 0, MaxValue = 99, Value = sim.PriorityOf(type), CustomMinimumSize = new Vector2(60, 0) };
            Body.AddChild(Row(
                Text(UiText.T("task." + UiText.Snake(type) + ".name"), 110),
                spin,
                ActionButton("ui.staff.set", () => Host.Send(new SetTaskPriority(type, (int)spin.Value))),
                ActionButton("ui.staff.reset", () => Host.Send(new SetTaskPriority(type, null)))));
        }
    }

    private void AddWorker(Worker worker, RoleDef role, bool preOpen)
    {
        var sim = Host.Sim;
        var state = worker.QuitWarning ? UiText.T("ui.staff.quit_warning") : worker.IsPresent ? "" : UiText.T("ui.staff.away");
        Body.AddChild(Row(
            Text(UiText.F("ui.staff.stats",
                ("role", UiText.T(role.NameKey)), ("level", worker.Level),
                ("skill_name", UiText.T(role.SkillNameKey ?? "ui.staff.skill.knife")), ("knife", worker.KnifeSkill),
                ("speed", worker.WorkSpeedPermille / 10), ("morale", worker.MoralePermille / 10)), 300),
            Text(state, 50),
            ActionButton("ui.staff.fire", () => Host.Send(new FireEmployee(worker.Id)))));
        Body.AddChild(Text("  " + UiText.F("ui.staff.morale_why",
            ("wage", Morale.FromWage(role, worker.WageCentsPerDay) / 10), ("work", worker.MoraleAdjustPermille / 10),
            ("streak", worker.WorkStreakDays), ("tasks", worker.TasksToday))));

        var shift = new OptionButton();
        foreach (var s in Enum.GetValues<Shift>()) shift.AddItem(UiText.T("ui.staff.shift." + UiText.Snake(s)));
        shift.Selected = (int)worker.Shift;
        shift.ItemSelected += i => Host.Send(new SetShift(worker.Id, (Shift)(int)i));

        var days = new HBoxContainer();
        for (var d = 0; d < 7; d++)
        {
            var day = d;
            var box = new CheckBox { Text = "ui.weekday.short." + d, ButtonPressed = worker.RestDays.Contains(d) };
            box.Toggled += on => Host.Send(new SetRestDays(worker.Id, on ? [.. worker.RestDays, day] : worker.RestDays.Where(x => x != day).ToList()));
            days.AddChild(box);
        }

        var tasks = new OptionButton();
        tasks.AddItem(UiText.T("ui.staff.tasks_all"));
        foreach (var t in role.TaskTypes) tasks.AddItem(UiText.T("task." + UiText.Snake(t) + ".name"));
        tasks.Selected = worker.AllowedTasks is { Count: 1 } only ? role.TaskTypes.ToList().IndexOf(only[0]) + 1 : 0;
        tasks.ItemSelected += i => Host.Send(new SetWorkerTasks(worker.Id, i == 0 ? null : [role.TaskTypes[(int)i - 1]]));
        tasks.Disabled = role.TaskTypes.Count == 0;
        Body.AddChild(Row(Text("  " + UiText.T("ui.staff.shift"), 50), shift, Text(UiText.T("ui.staff.rest_days")), days));

        var wage = new SpinBox { MinValue = 0, MaxValue = 9999, Step = 1, Prefix = "$", Value = worker.WageCentsPerDay / 100.0, CustomMinimumSize = new Vector2(80, 0) };
        Body.AddChild(Row(
            Text("  " + UiText.T("ui.staff.only"), 50), tasks,
            Text(UiText.T("ui.staff.wage")), wage,
            ActionButton("ui.staff.set_wage", () => Host.Send(new SetWage(worker.Id, (long)Math.Round(wage.Value * 100))), preOpen)));
    }

    /// <summary>A one-line timeline: morning and evening blocks lit when the worker is in (dark on today's rest day).</summary>
    private static Control ShiftBar(Worker worker, int weekday)
    {
        var bar = new HBoxContainer();
        var resting = worker.RestDays.Contains(weekday);
        foreach (var half in new[] { Shift.Morning, Shift.Evening })
        {
            var on = !resting && (worker.Shift == Shift.Full || worker.Shift == half);
            bar.AddChild(new ColorRect { CustomMinimumSize = new Vector2(90, 8), Color = on ? new Color(0.35f, 0.8f, 0.4f) : new Color(0.25f, 0.25f, 0.28f) });
        }

        return bar;
    }

    private void AddArea(Department area, List<Worker> managers)
    {
        var sim = Host.Sim;
        var picker = new OptionButton();
        picker.AddItem(UiText.T("ui.staff.no_manager"));
        foreach (var m in managers) picker.AddItem(UiText.T("role.manager.name") + m.Id);
        var current = sim.ManagerOf(area);
        picker.Selected = current is null ? 0 : managers.IndexOf(current) + 1;
        picker.ItemSelected += i => { if (i > 0) Host.Send(new AssignManager(managers[(int)i - 1].Id, area)); else if (current is not null) Host.Send(new AssignManager(current.Id, null)); };
        picker.Disabled = managers.Count == 0;

        var automation = sim.Automation.GetValueOrDefault(area) ?? new DepartmentAutomation(false, false);
        var order = new CheckBox { Text = "ui.staff.auto_order", ButtonPressed = automation.AutoOrder };
        var price = new CheckBox { Text = "ui.staff.auto_price", ButtonPressed = automation.AutoPrice };
        order.Toggled += on => Host.Send(new SetAutomation(area, on, price.ButtonPressed));
        price.Toggled += on => Host.Send(new SetAutomation(area, order.ButtonPressed, on));
        Body.AddChild(Row(Text(UiText.T("ui.area." + UiText.Snake(area)), 70), picker, order, price));

        if (!automation.AutoOrder) return;

        var area_ = GameSim.AreaOf(area);
        foreach (var product in Host.Data.Products.Where(p => p.Orderable && area_.Contains(p.Department)))
        {
            var spin = new SpinBox { MinValue = 0, MaxValue = 999, Value = sim.OrderTargets.GetValueOrDefault(product.Id), CustomMinimumSize = new Vector2(64, 0) };
            Body.AddChild(Row(
                Text("    " + UiText.T(product.NameKey), 120),
                Text(UiText.T("ui.staff.target"), 50),
                spin,
                ActionButton("ui.staff.set", () => Host.Send(new SetOrderTarget(product.Id, (int)spin.Value)))));
        }
    }

    /// <summary>What the panel shows that can change without the player touching it.</summary>
    private string Signature()
    {
        var sim = Host.Sim;
        return string.Join("|", sim.Workers.Select(w => $"{w.Id}:{w.IsPresent}:{w.QuitWarning}:{w.Shift}:{w.ManagedDepartment}:{string.Join(",", w.RestDays)}"))
               + string.Join(",", sim.Applicants.Select(a => a.Id)) + string.Join(",", sim.Automation.Select(a => $"{a.Key}{a.Value}"))
               + sim.Clock.Day + sim.Clock.Phase;
    }

    protected override void Refresh()
    {
        if (Signature() != _shown) Rebuild();
    }
}
