using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Staff;

/// <summary>Turn an applicant away.</summary>
public sealed record RejectApplicant(int ApplicantId) : SimCommand;
