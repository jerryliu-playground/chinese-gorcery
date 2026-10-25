using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Deli;

public sealed record DeliServed(int StationInstanceId, int CustomerId, string DishId, int WorkerId) : SimEvent;
