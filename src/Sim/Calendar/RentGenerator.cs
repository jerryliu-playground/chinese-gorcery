using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Calendar;

/// <summary>Rents a generator for an announced power cut, before it starts.</summary>
public sealed record RentGenerator(int EventInstanceId) : SimCommand;
