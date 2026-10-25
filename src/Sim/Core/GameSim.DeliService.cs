using ChineseGrocery.Sim.Customers;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Deli;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Tasks;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Core;

/// <summary>
/// Selling hot food: takeaway only. Customers queue at the station holding their dish and a worker packs a portion;
/// at mealtimes extra customers come in just for hot food, while any station has some.
/// </summary>
public sealed partial class GameSim
{
    private IEnumerable<Stockpile> StationStock() => Inventory.Locations.Where(p => IsDeliStation(p.Location.FixtureInstanceId) && p.Lots.Count > 0);

    /// <summary>Extra arrivals from the mealtime curve, each wanting only hot food.</summary>
    private void SpawnRushArrivals()
    {
        if (Data.Economy.Deli is not { } deli || Clock.Phase != DayPhase.Open || Clock.MinuteOfDay >= Data.Economy.CloseMinuteOfDay) return;
        if (!StationStock().Any()) return;

        var bucket = deli.RushArrivals.Last(b => b.StartMinuteOfDay <= Clock.MinuteOfDay);
        var perHourTimesSeconds = bucket.CustomersPerHour * Data.Economy.SecondsPerTick * ArrivalPermille() / 1000;
        if (perHourTimesSeconds == 0) return;

        var count = perHourTimesSeconds / SecondsPerHour;
        if (Rng.NextInt(0, SecondsPerHour) < perHourTimesSeconds % SecondsPerHour) count++;

        for (var i = 0; i < count; i++)
        {
            var typeId = bucket.TypeWeights[PickWeighted(bucket.TypeWeights.Select(w => w.Weight).ToList())].TypeId;
            var type = Data.CustomerTypes.Single(t => t.Id == typeId);
            SpawnCustomer(type, HotFoodList(type));
        }
    }

    /// <summary>One or two dishes from what the stations have, by the type's product weights.</summary>
    private List<string> HotFoodList(CustomerTypeDef type)
    {
        var dishes = StationStock().SelectMany(p => p.Lots.Select(l => l.ProductId)).Distinct().Order(StringComparer.Ordinal).ToList();
        var weights = dishes.Select(type.WeightOf).ToList();
        var list = new List<string>();
        if (weights.Sum() <= 0) return list;

        for (var n = Rng.NextInt(1, 3); n > 0; n--) list.Add(dishes[PickWeighted(weights)]);
        return list;
    }

    /// <summary>
    /// The customer goes to queue at the nearest station holding the dish, if its price is acceptable. Returns false
    /// (noting why) otherwise.
    /// </summary>
    private bool TryQueueForDish(Customer customer, CustomerTypeDef type, string dishId)
    {
        var distances = GridPaths.Distances(IsWalkable, customer.Position);
        var stop = StationStock()
            .Where(p => p.Count(dishId) > 0)
            .Select(p => Layout.Find(p.Location.FixtureInstanceId)!)
            .Select(f => (f.InstanceId, Cell: LayoutRules.CustomerCells(_fixtures[f.FixtureId], f.Origin, f.Rotation).First()))
            .Where(s => distances.ContainsKey(s.Cell))
            .OrderBy(s => distances[s.Cell])
            .ThenBy(s => s.InstanceId)
            .Select(s => ((int, GridPoint)?)s)
            .FirstOrDefault();
        if (stop is not { } station)
        {
            NoteNotFound(customer, dishId);
            return false;
        }

        var lot = StationStock().Where(p => p.Location.FixtureInstanceId == station.Item1).SelectMany(p => p.Lots).First(l => l.ProductId == dishId);
        var reference = Prices.ReferenceOf(dishId) * (1000 + lot.PriceBonusPermille) / 1000;
        if (Rng.NextInt(0, 1000) >= PriceAcceptance.BuyChancePermille(type, EffectivePrice(lot), reference))
        {
            NoteTooExpensive(customer, dishId);
            return false;
        }

        customer.CheckoutInstanceId = station.Item1;
        customer.WaitingFor = dishId;
        customer.SetPath(GridPaths.FindPath(IsWalkable, customer.Position, station.Item2)!);
        customer.State = CustomerState.ToCounter;
        return true;
    }

    /// <summary>
    /// One ServeDeli task per station, for the earliest customer in line whose dish is there now. Customers whose dish
    /// has run out and is not being cooked for this station give up on it.
    /// </summary>
    private void RefreshServeTasks()
    {
        Tasks.RemoveOpen(t => t is ServeDeliTask s && WaitingAtCounter(s.CustomerId) is null);

        foreach (var station in Layout.Fixtures.Where(f => DataValidator.IsDeliStation(_fixtures[f.FixtureId].Kind)).Select(f => f.InstanceId))
        {
            var line = _customers.Where(c => c.State == CustomerState.AtCounter && c.CheckoutInstanceId == station).OrderBy(c => c.QueueNumber).ToList();
            foreach (var stranded in line.Where(c => DishAt(station, c.WaitingFor!) == 0 && !IsBeingCookedFor(station, c.WaitingFor!)).ToList())
            {
                NoteNotFound(stranded, stranded.WaitingFor);
                StopWaiting(stranded);
            }

            if (Tasks.Tasks.Any(t => t is ServeDeliTask s && s.StationInstanceId == station)) continue;
            if (line.FirstOrDefault(c => c.State == CustomerState.AtCounter && DishAt(station, c.WaitingFor!) > 0) is not { } next) continue;

            Tasks.Add(id => new ServeDeliTask(id, PriorityOf(TaskType.ServeDeli), station, next.Id, next.WaitingFor!));
        }
    }

    private int DishAt(int station, string dishId) =>
        Inventory.Locations.Where(p => p.Location.FixtureInstanceId == station).Sum(p => p.Count(dishId));

    private bool IsBeingCookedFor(int station, string dishId) =>
        Tasks.Tasks.Any(t => t is CookTask c && c.Target.FixtureInstanceId == station && c.DishId == dishId);

    private bool StartPacking(Worker worker, ServeDeliTask task)
    {
        if (WaitingAtCounter(task.CustomerId) is not { } customer) return false;

        customer.State = CustomerState.BeingServed;
        BeginStep(worker, WorkStep.Packing, Data.Economy.Tasks.ServeDeliMinutes);
        return true;
    }

    /// <summary>A portion goes into the customer's box, priced as the dish sells today; they carry on shopping.</summary>
    private void FinishPacking(Worker worker, ServeDeliTask task)
    {
        var customer = _customers.Find(c => c.Id == task.CustomerId && c.State == CustomerState.BeingServed);
        var pile = Inventory.Locations.FirstOrDefault(p => p.Location.FixtureInstanceId == task.StationInstanceId && p.Count(task.DishId) > 0);
        if (customer is null || pile is null)
        {
            if (customer is not null) customer.State = CustomerState.AtCounter;
            FinishTask(worker, completed: false);
            return;
        }

        var lot = pile.Lots.First(l => l.ProductId == task.DishId);
        customer.AddToBasket(new BasketItem(pile.TakeFrom(lot, 1), pile.Location, EffectivePrice(lot)));
        StopWaiting(customer);
        _events.Add(new DeliServed(task.StationInstanceId, customer.Id, task.DishId, worker.Id));
        FinishTask(worker, completed: true);
    }
}
