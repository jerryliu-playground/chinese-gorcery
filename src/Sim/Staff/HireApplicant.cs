using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Staff;

/// <summary>Before opening: hire one of today's applicants at their asking wage.</summary>
public sealed record HireApplicant(int ApplicantId) : SimCommand;
