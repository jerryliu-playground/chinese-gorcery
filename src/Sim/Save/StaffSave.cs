using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Staff;
using ChineseGrocery.Sim.Tasks;

namespace ChineseGrocery.Sim.Save;

/// <summary>Staff management state outside the workers themselves.</summary>
public sealed record StaffSave(
    RngState Rng,
    IReadOnlyList<Applicant> Applicants,
    int NextApplicantId,
    IReadOnlyDictionary<TaskType, int> PriorityOverrides,
    IReadOnlyDictionary<Department, DepartmentAutomation> Automation,
    IReadOnlyDictionary<string, int> OrderTargets);
