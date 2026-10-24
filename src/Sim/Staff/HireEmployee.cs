using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Staff;

/// <summary>PreOpen only: hire someone for a role; attributes are rolled within the role's ranges, wage starts at market.</summary>
public sealed record HireEmployee(string RoleId) : SimCommand;
