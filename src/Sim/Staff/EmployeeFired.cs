using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Staff;

public sealed record EmployeeFired(int WorkerId, long SeveranceCents) : SimEvent;
