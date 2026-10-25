using ChineseGrocery.Sim.Customers;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Reputation;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Core;

/// <summary>Customer arrivals and behaviour: shop the list, queue, get served, or give up and leave.</summary>
public sealed partial class GameSim
{
    private const int SecondsPerHour = 3600;

    private readonly List<Customer> _customers = [];
    private int _nextCustomerId = 1;
    private long _nextQueueNumber = 1;

    public IReadOnlyList<Customer> Customers => _customers;

    private void SpawnArrivals()
    {
        if (Clock.Phase != DayPhase.Open || Clock.MinuteOfDay >= Data.Economy.CloseMinuteOfDay) return;

        // Expected arrivals this tick = rate per hour (scaled by reputation and the calendar, less any lost to the
        // competition) x tick length; the fractional part is a seeded roll.
        var perHourTimesSeconds = (int)((long)CurrentArrivalRate() * Data.Economy.SecondsPerTick * ArrivalPermille() / 1000
            * CalendarArrivalPermille() / 1000 * CompetitionArrivalPermille() / 1000);
        var count = perHourTimesSeconds / SecondsPerHour;
        if (Rng.NextInt(0, SecondsPerHour) < perHourTimesSeconds % SecondsPerHour) count++;

        for (var i = 0; i < count; i++) SpawnCustomer();
        SpawnRushArrivals();
        SpawnWordOfMouth();
    }

    private int CurrentArrivalRate() =>
        Data.Economy.CustomerArrivals.Last(b => b.StartMinuteOfDay <= Clock.MinuteOfDay).CustomersPerHour;

    /// <summary>The next customer's type, by arrival weights that depend on the store's stars and departments.</summary>
    private void SpawnCustomer()
    {
        var types = Data.CustomerTypes;
        var type = types[PickWeighted(types.Select(ArrivalWeightNow).ToList())];
        SpawnCustomer(type, null);
    }

    /// <summary>A customer of the type comes in, with the given list or one drawn from what is on sale.</summary>
    private void SpawnCustomer(CustomerTypeDef type, List<string>? shoppingList)
    {
        var doors = EntranceCells();
        var door = doors[doors.Count == 1 ? 0 : Rng.NextInt(0, doors.Count)];

        var customer = new Customer(_nextCustomerId++, type.Id, door, shoppingList ?? BuildShoppingList(WithLabels(type)));
        SeekTrend(customer, type);
        _customers.Add(customer);
        Economy.Today.CountCustomer();
        _events.Add(new CustomerEntered(customer.Id, type.Id));
    }

    private int PickWeighted(IReadOnlyList<int> weights)
    {
        var roll = Rng.NextInt(0, weights.Sum());
        for (var i = 0; i < weights.Count; i++)
        {
            roll -= weights[i];
            if (roll < 0) return i;
        }

        throw new InvalidOperationException("Weights must sum to more than zero.");
    }

    /// <summary>
    /// Items drawn from what is on display right now: a department by the type's preferences, then a product in it by
    /// the type's product weights. On days when demand is not normal (season, festivals, events) the draws are scaled:
    /// a product's weight by its demand, a department's by how far its displayed goods' demand is above or below normal,
    /// and the list's length by the same amount on average, so a department in demand gets extra items while the others
    /// keep theirs. With normal demand the draws are exactly the plain ones.
    /// </summary>
    internal List<string> BuildShoppingList(CustomerTypeDef type)
    {
        var onDisplay = Inventory.Locations
            .Where(p => IsDisplay(p.Location.FixtureInstanceId) || IsDeliStation(p.Location.FixtureInstanceId))
            .SelectMany(p => p.Lots.Select(l => l.ProductId))
            .Where(id => !IsProduce(id) || type.ProduceCategories is not { } only || only.Contains(_products[id].Category!.Value))
            .Distinct()
            .Order(StringComparer.Ordinal)
            .GroupBy(id => _products[id].Department)
            .ToDictionary(g => g.Key, g => g.ToList());
        var wanted = type.Preferences.Where(p => p.Weight > 0 && onDisplay.ContainsKey(p.Department)).ToList();
        var demand = onDisplay.Values.SelectMany(ids => ids).ToDictionary(id => id, id => DemandOf(_products[id]), StringComparer.Ordinal);
        var (departmentWeights, lengthPermille) = DepartmentWeights(type, wanted, onDisplay, demand);

        var size = Rng.NextInt(type.ShoppingListSize.Min, type.ShoppingListSize.Max + 1);
        if (lengthPermille != 1000)
        {
            var scaled = (long)size * lengthPermille;
            size = (int)Math.Max(1, scaled / 1000 + (Rng.NextInt(0, 1000) < scaled % 1000 ? 1 : 0));
        }

        var list = new List<string>();
        for (var i = 0; i < size && wanted.Count > 0; i++)
        {
            var department = wanted[PickWeighted(departmentWeights)].Department;
            var products = onDisplay[department];
            var weights = products.Select(type.WeightOf).ToList();
            if (weights.Sum() <= 0) continue;

            // Products whose demand differs from each other are weighted by it.
            if (products.Select(id => demand[id].Permille).Distinct().Count() > 1)
            {
                weights = products.Select((id, k) => (int)Math.Min(int.MaxValue / products.Count, (long)weights[k] * demand[id].Permille)).ToList();
            }

            // Equal weights keep the plain uniform roll, so stores without weighted products play out as before.
            list.Add(products[weights.Distinct().Count() == 1 ? Rng.NextInt(0, products.Count) : PickWeighted(weights)]);
        }

        return list;
    }

    /// <summary>
    /// The type's department preferences, each scaled by the demand of the department's displayed goods against their
    /// normal level (weighted by the type's product weights), and how much longer the list gets for it (the
    /// preference-weighted average of those scales). Unscaled when no department is off normal.
    /// </summary>
    private (List<int> Weights, long LengthPermille) DepartmentWeights(
        CustomerTypeDef type, List<DepartmentWeight> wanted, Dictionary<Department, List<string>> onDisplay, Dictionary<string, (int Permille, int Baseline)> demand)
    {
        var plain = wanted.Select(p => p.Weight).ToList();
        var scales = wanted.Select(p =>
        {
            long now = 0, normal = 0;
            foreach (var id in onDisplay[p.Department])
            {
                now += (long)type.WeightOf(id) * demand[id].Permille;
                normal += (long)type.WeightOf(id) * demand[id].Baseline;
            }

            return normal <= 0 || now == normal ? 1000 : now * 1000 / normal;
        }).ToList();
        if (scales.All(s => s == 1000)) return (plain, 1000);

        var length = plain.Select((w, k) => w * scales[k]).Sum() / plain.Sum();
        return (plain.Select((w, k) => (int)Math.Max(1, w * scales[k])).ToList(), length);

    }

    private void UpdateCustomers()
    {
        foreach (var customer in _customers.ToList())
        {
            var type = Data.CustomerTypes.Single(t => t.Id == customer.TypeId);
            MoveCustomer(customer, type.MoveCellsPerMinute);
            if (customer.Path.Count > 0) continue;

            switch (customer.State)
            {
                case CustomerState.Shopping:
                    Shop(customer, type);
                    break;

                case CustomerState.ToCheckout or CustomerState.ToCounter:
                    customer.State = customer.State == CustomerState.ToCheckout ? CustomerState.Queued : CustomerState.AtCounter;
                    customer.QueueNumber = _nextQueueNumber++;
                    customer.WaitedSeconds = 0;
                    break;

                case CustomerState.Queued or CustomerState.AtCounter:
                    customer.WaitedSeconds += Data.Economy.SecondsPerTick;
                    customer.LongestWaitSeconds = Math.Max(customer.LongestWaitSeconds, customer.WaitedSeconds);
                    if (customer.WaitedSeconds > type.PatienceMinutes * 60)
                    {
                        _events.Add(new CustomerReaction(customer.Id, CustomerReactionKind.GaveUp));
                        ReturnBasket(customer);
                        Economy.Today.CountAngryLeave();
                        StartLeaving(customer, CustomerExit.Angry);
                    }

                    break;

                case CustomerState.Leaving:
                    _customers.Remove(customer);
                    _events.Add(new CustomerLeft(customer.Id, customer.Exit!.Value));
                    break;
            }
        }
    }

    private void MoveCustomer(Customer customer, int cellsPerMinute)
    {
        var progress = customer.MoveProgress;
        var steps = StepsThisTick(ref progress, cellsPerMinute, customer.Path.Count > 0);
        customer.MoveProgress = progress;
        for (var i = 0; i < steps && customer.Path.Count > 0; i++)
        {
            customer.Position = customer.Path[0];
            customer.PopStep();
        }
    }

    private void Shop(Customer customer, CustomerTypeDef type)
    {
        if (customer.Target is { } target)
        {
            if (customer.BrowseTicksLeft > 0 && --customer.BrowseTicksLeft > 0) return;

            TryTake(customer, type, target.Slot, target.ProductId);
            customer.Target = null;
        }

        while (customer.NextListItem() is { } productId)
        {
            if (_products[productId].Live?.KillToOrder == true)
            {
                if (TryQueueAtCounter(customer, type, productId)) return;
                continue;
            }

            if (_products[productId].Department == Department.Deli)
            {
                if (TryQueueForDish(customer, type, productId)) return;
                continue;
            }

            if (NearestDisplayWith(productId, customer.Position) is { } stop)
            {
                customer.Target = (stop.Slot, productId);
                customer.SetPath(GridPaths.FindPath(IsWalkable, customer.Position, stop.Cell)!);
                customer.BrowseTicksLeft = WorkTicks(type.BrowseMinutesPerItem, 1000);
                return;
            }

            NoteNotFound(customer, productId);
        }

        if (customer.Basket.Count == 0)
        {
            if (customer.ItemsNotFound == 0 && customer.ItemsTooExpensive == 0) NoteNotFound(customer);
            StartLeaving(customer, CustomerExit.NothingBought);
            return;
        }

        if (ChooseCheckout(customer.Position) is not { } checkout)
        {
            ReturnBasket(customer);
            Economy.Today.CountAngryLeave();
            StartLeaving(customer, CustomerExit.Angry);
            return;
        }

        customer.CheckoutInstanceId = checkout.InstanceId;
        customer.SetPath(GridPaths.FindPath(IsWalkable, customer.Position, checkout.Cell)!);
        customer.State = CustomerState.ToCheckout;
    }

    private void TryTake(Customer customer, CustomerTypeDef type, StockLocation slot, string productId)
    {
        MaybePuzzle(customer, type);
        if (Inventory.Count(slot, productId) == 0)
        {
            NoteNotFound(customer, productId);
            return;
        }

        var batch = ChooseBatch(Inventory.Get(slot), productId, type);
        var price = EffectivePrice(batch);
        var chance = ApplyLabels(ApplyDisplay(PriceAcceptance.BuyChancePermille(type, price, Prices.ReferenceOf(productId)), slot), type);
        if (Rng.NextInt(0, 1000) >= chance)
        {
            NoteTooExpensive(customer, productId);
            return;
        }

        customer.AddToBasket(new BasketItem(Inventory.Get(slot).TakeFrom(batch, 1), slot, price));
        MessUp(slot);

        _events.Add(new CustomerReaction(customer.Id, CustomerReactionKind.TookItem, productId));
        NoteTaken(customer, type, productId);
        if (Inventory.Count(slot, productId) == 0)
        {
            _events.Add(new ProductSoldOut(slot, productId));
            NoteFact(ReviewFact.SoldOut, productId, type.Id);
        }
    }

    private void NoteTooExpensive(Customer customer, string productId)
    {
        customer.ItemsTooExpensive++;
        Economy.Today.CountItemTooExpensive();
        _events.Add(new CustomerReaction(customer.Id, CustomerReactionKind.TooExpensive, productId));
        NoteFact(ReviewFact.TooExpensive, productId, customer.TypeId);
    }

    /// <summary>
    /// Which batch a shopper takes: produce goes freshest first, except that bargain hunters go for the cheapest
    /// (marked-down) one; everything else goes oldest first.
    /// </summary>
    private StockLot ChooseBatch(Stockpile pile, string productId, CustomerTypeDef type)
    {
        var lots = pile.Lots.Where(l => l.ProductId == productId).ToList();
        if (!IsProduce(productId)) return lots.MinBy(l => l.BestBeforeDay)!;

        return type.PrefersDiscount
            ? lots.OrderBy(EffectivePrice).ThenBy(l => l.BestBeforeDay).First()
            : lots.MaxBy(l => l.BestBeforeDay)!;
    }

    /// <summary>
    /// Live fish and lobster are not taken from the tank: if some are in, the price is acceptable and a seafood counter
    /// can be reached, the customer goes to queue there. Returns false (noting why) otherwise.
    /// </summary>
    private bool TryQueueAtCounter(Customer customer, CustomerTypeDef type, string productId)
    {
        if (!Tanks().Any(t => TankSlots(t).Any(p => p.Count(productId) > 0)) || NearestCounter(customer.Position) is not { } counter)
        {
            NoteNotFound(customer, productId);
            return false;
        }

        if (Rng.NextInt(0, 1000) >= PriceAcceptance.BuyChancePermille(type, Prices.PriceOf(productId), Prices.ReferenceOf(productId)))
        {
            NoteTooExpensive(customer, productId);
            return false;
        }

        customer.CheckoutInstanceId = counter.InstanceId;
        customer.WaitingFor = productId;
        customer.SetPath(GridPaths.FindPath(IsWalkable, customer.Position, counter.Cell)!);
        customer.State = CustomerState.ToCounter;
        return true;
    }

    /// <summary>The seafood counter with the shortest walk, and where its customers stand.</summary>
    private (int InstanceId, GridPoint Cell)? NearestCounter(GridPoint from)
    {
        var distances = GridPaths.Distances(IsWalkable, from);
        return Layout.Fixtures
            .Where(f => _fixtures[f.FixtureId].Kind == FixtureKind.SeafoodCounter)
            .Select(f => (f.InstanceId, Cell: LayoutRules.CustomerCells(_fixtures[f.FixtureId], f.Origin, f.Rotation).First()))
            .Where(c => distances.ContainsKey(c.Cell))
            .OrderBy(c => distances[c.Cell])
            .ThenBy(c => c.InstanceId)
            .Select(c => ((int, GridPoint)?)c)
            .FirstOrDefault();
    }

    private void NoteNotFound(Customer customer, string? productId = null)
    {
        customer.ItemsNotFound++;
        Economy.Today.CountItemNotFound();
        _events.Add(new CustomerReaction(customer.Id, CustomerReactionKind.NotFound, productId));
        if (productId is not null) NoteNotFoundFacts(customer, productId);
    }

    /// <summary>The closest display slot holding the product, by walking distance, and the cell to stand on.</summary>
    private (StockLocation Slot, GridPoint Cell)? NearestDisplayWith(string productId, GridPoint from)
    {
        var distances = GridPaths.Distances(IsWalkable, from);
        (StockLocation Slot, GridPoint Cell, int Distance)? best = null;
        foreach (var pile in Inventory.Locations.Where(p => p.Count(productId) > 0 && IsDisplay(p.Location.FixtureInstanceId)))
        {
            var fixture = Layout.Find(pile.Location.FixtureInstanceId)!;
            foreach (var cell in LayoutRules.CustomerCells(_fixtures[fixture.FixtureId], fixture.Origin, fixture.Rotation))
            {
                if (distances.TryGetValue(cell, out var d) && (best is null || d < best.Value.Distance))
                {
                    best = (pile.Location, cell, d);
                }
            }
        }

        return best is { } b ? (b.Slot, b.Cell) : null;
    }

    /// <summary>The checkout with the shortest line (then the lowest id), and where its customers stand.</summary>
    private (int InstanceId, GridPoint Cell)? ChooseCheckout(GridPoint from)
    {
        var distances = GridPaths.Distances(IsWalkable, from);
        return Layout.Fixtures
            .Where(f => _fixtures[f.FixtureId].Kind == FixtureKind.Checkout)
            .Select(f => (f.InstanceId, Cell: LayoutRules.CustomerCells(_fixtures[f.FixtureId], f.Origin, f.Rotation).First()))
            .Where(c => distances.ContainsKey(c.Cell))
            .OrderBy(c => _customers.Count(x => x.CheckoutInstanceId == c.InstanceId && x.State is CustomerState.ToCheckout or CustomerState.Queued or CustomerState.BeingServed))
            .ThenBy(c => c.InstanceId)
            .Select(c => ((int, GridPoint)?)c)
            .FirstOrDefault();
    }

    private void StartLeaving(Customer customer, CustomerExit exit)
    {
        ScoreCustomer(customer, exit);
        customer.Exit = exit;
        customer.State = CustomerState.Leaving;
        customer.CheckoutInstanceId = null;
        customer.WaitingFor = null;
        var distances = GridPaths.Distances(IsWalkable, customer.Position);
        var door = EntranceCells().Where(distances.ContainsKey).MinBy(c => distances[c]);
        customer.SetPath(GridPaths.FindPath(IsWalkable, customer.Position, door)!);
    }

    /// <summary>Puts a customer's goods back: to the slot they came from if it still fits, else into storage, else they are lost.</summary>
    private void ReturnBasket(Customer customer)
    {
        foreach (var (lot, from, _) in customer.EmptyBasket())
        {
            var product = _products[lot.ProductId];
            if (product.Live?.KillToOrder == true && !IsDisplay(from.FixtureInstanceId))
            {
                // Already killed at the counter: it can only go on as chilled goods.
                KeepAsChilled(lot);
                continue;
            }

            if (IsDeliStation(from.FixtureInstanceId))
            {
                // Hot food goes back on its station (it is thrown away at closing if unsold).
                Inventory.GetOrCreate(from).Add(lot);
                continue;
            }

            if (Shelves.ProductAt(from) == lot.ProductId && Inventory.Count(from, lot.ProductId) + lot.Quantity <= product.UnitsPerShelfSlot)
            {
                Inventory.GetOrCreate(from).Add(lot);
            }
            else if (StorageRouting.FindDestination(Data, Layout.Fixtures, product) is { } storage)
            {
                Inventory.GetOrCreate(StockLocation.Storage(storage.InstanceId)).Add(lot);
            }
            else
            {
                var loss = checked(lot.Quantity * lot.UnitCostCents);
                Economy.RecordLoss(loss, product.Department);
                _events.Add(new ProductExpired(Clock.Day, lot.ProductId, lot.Quantity, null, loss));
            }
        }
    }

    private List<GridPoint> EntranceCells() =>
        Layout.Fixtures
            .Where(f => _fixtures[f.FixtureId].Kind == FixtureKind.Entrance)
            .SelectMany(f => LayoutRules.CustomerCells(_fixtures[f.FixtureId], f.Origin, f.Rotation))
            .Distinct()
            .ToList();

    private bool IsDisplay(int instanceId) =>
        Layout.Find(instanceId) is { } f && DataValidator.IsDisplay(_fixtures[f.FixtureId].Kind);

    /// <summary>Called when a worker finishes serving: the customer pays for the basket and heads out.</summary>
    private long CheckOut(Customer customer)
    {
        long total = 0;
        var items = customer.EmptyBasket();
        NoteCheckoutFacts(customer, items);
        foreach (var group in items.GroupBy(i => i.Lot.ProductId).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var units = group.Sum(i => i.Lot.Quantity);
            var revenue = checked(group.Sum(i => i.Lot.Quantity * (i.PriceCents >= 0 ? i.PriceCents : Prices.PriceOf(group.Key))));
            Economy.Earn(group.Key, units, revenue, group.Sum(i => i.Lot.Quantity * i.Lot.UnitCostCents));
            total += revenue;
        }

        StartLeaving(customer, CustomerExit.Paid);
        return total;
    }
}
