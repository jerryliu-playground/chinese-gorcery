namespace ChineseGrocery.Sim.Staff;

/// <summary>Someone looking for work today; better skill and speed ask for a higher wage. Gone tomorrow if not hired.</summary>
public sealed record Applicant(int Id, string RoleId, int Skill, int SpeedPermille, long AskingWageCentsPerDay);
