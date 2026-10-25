using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Staff;

public sealed record EmployeeQuit(int WorkerId, string RoleId) : SimEvent;
