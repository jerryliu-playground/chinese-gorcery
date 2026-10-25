using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Imports;

public sealed record ShipmentOrdered(int ShipmentId, string ProductId, int Quantity, int ArrivalDay) : SimEvent;
