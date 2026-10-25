using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Deli;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Produce;
using ChineseGrocery.Sim.Tasks;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Core;

/// <summary>
/// The kitchen: the daily menu of deli station slots, picking ingredients (least fresh usable batch first, from
/// anywhere in the store), and Cook tasks that turn them into dishes at a stove and carry them to the station.
/// </summary>
public sealed partial class GameSim
{
    private readonly SortedDictionary<StockLocation, MenuItem> _menu = new();
    private readonly SortedDictionary<StockLocation, int> _cookedToday = new();

    public IReadOnlyDictionary<StockLocation, MenuItem> Menu => _menu;

    /// <summary>Portions cooked (or being cooked) today for a station slot.</summary>
    public int CookedToday(StockLocation slot) => _cookedToday.GetValueOrDefault(slot);

    private RecipeDef RecipeOf(string dishId) => Data.Recipes.First(r => r.Id == dishId);

    private bool IsDeliStation(int instanceId) =>
        Layout.Find(instanceId) is { } f && DataValidator.IsDeliStation(_fixtures[f.FixtureId].Kind);

    private Rejection? HandleSetMenuItem(SetMenuItem set)
    {
        if (Clock.Phase != DayPhase.PreOpen) return Reject(RejectReason.WrongPhase);
        if (Layout.Find(set.StationInstanceId) is not { } station) return Reject(RejectReason.UnknownFixtureInstance);

        var def = _fixtures[station.FixtureId];
        if (!DataValidator.IsDeliStation(def.Kind)) return Reject(RejectReason.NotADeliStation);
        if (set.Slot < 0 || set.Slot >= def.Capacity) return Reject(RejectReason.InvalidSlot);
        if (set.TargetPortions < 0) return Reject(RejectReason.InvalidQuantity);

        var slot = new StockLocation(set.StationInstanceId, set.Slot);
        if (set.DishId is not { } dishId)
        {
            _menu.Remove(slot);
            _events.Add(new MenuItemSet(slot, null));
            return null;
        }

        if (Data.Recipes.FirstOrDefault(r => r.Id == dishId) is not { } recipe) return Reject(RejectReason.UnknownProduct);
        if (recipe.Station != def.Kind) return Reject(RejectReason.WrongStation);
        if (_menu.GetValueOrDefault(slot)?.DishId is { } current && current != dishId && Inventory.Count(slot, current) > 0) return Reject(RejectReason.SlotNotEmpty);

        var item = new MenuItem(dishId, set.TargetPortions);
        _menu[slot] = item;
        _events.Add(new MenuItemSet(slot, item));
        return null;
    }

    /// <summary>
    /// Every batch of the product the kitchen may use, in the order it takes them: anywhere except the deli stations,
    /// never spoiled; batches on their last day first, then by freshness tier (ripe before normal before fresh), then
    /// the earliest arrival.
    /// </summary>
    public IReadOnlyList<IngredientCandidate> IngredientCandidates(string productId)
    {
        var product = _products[productId];
        return Inventory.Locations
            .Where(p => !IsDeliStation(p.Location.FixtureInstanceId))
            .SelectMany(p => p.Lots.Where(l => l.ProductId == productId && l.BestBeforeDay >= Clock.Day).Select(l => new IngredientCandidate(p.Location, l)))
            .OrderBy(c => c.Lot.BestBeforeDay == Clock.Day ? 0 : 1)
            .ThenByDescending(c => Freshness.TierOf(product, c.Lot, Clock.Day))
            .ThenBy(c => c.Lot.BestBeforeDay - product.ShelfLifeDays)
            .ThenBy(c => c.From)
            .ToList();
    }

    /// <summary>How many portions the ingredients in the store would make right now (whole cookings only).</summary>
    public int PortionsPossible(string dishId)
    {
        var recipe = RecipeOf(dishId);
        var cookings = recipe.Ingredients.Min(x => IngredientCandidates(x.ProductId).Sum(c => c.Lot.Quantity) / x.Quantity);
        return cookings * recipe.Portions;
    }

    /// <summary>Whether a batch counts as rescued when cooked: on its last day, or in its last freshness tier.</summary>
    public bool IsNearExpiry(StockLot lot) =>
        lot.BestBeforeDay == Clock.Day || Freshness.TierOf(_products[lot.ProductId], lot, Clock.Day) == FreshnessTier.Ripe;

    /// <summary>
    /// A Cook task per menu slot that is running low, can take a whole cooking, has not reached today's target and has
    /// ingredients, while a stove is free. Not after closing: dishes are only sold the day they are cooked.
    /// </summary>
    private void RefreshCookTasks()
    {
        if (Clock.Phase == DayPhase.Closed)
        {
            Tasks.RemoveOpen(t => t is CookTask);
            return;
        }

        Tasks.RemoveOpen(t => t is CookTask c && !IsCookingWanted(c.Target, c.DishId));

        var stoves = Layout.Fixtures.Where(f => _fixtures[f.FixtureId].Kind == FixtureKind.WokStove).Select(f => f.InstanceId).ToList();
        foreach (var (slot, item) in _menu)
        {
            if (Tasks.Tasks.Any(t => t is CookTask c && c.Target == slot) || !IsCookingWanted(slot, item.DishId)) continue;

            var stove = stoves.FirstOrDefault(s => !Tasks.Tasks.Any(t => t is CookTask c && c.StoveInstanceId == s));
            if (stove == 0) return;

            Tasks.Add(id => new CookTask(id, PriorityOf(TaskType.Cook), stove, slot, item.DishId));
        }
    }

    private bool IsCookingWanted(StockLocation slot, string dishId)
    {
        if (_menu.GetValueOrDefault(slot) is not { } item || item.DishId != dishId || !IsDeliStation(slot.FixtureInstanceId)) return false;

        var recipe = RecipeOf(dishId);
        var count = Inventory.Count(slot, dishId);
        return count * 1000L < (long)recipe.UnitsPerSlot * Data.Economy.Tasks.RestockBelowPermille
               && count + recipe.Portions <= recipe.UnitsPerSlot
               && CookedToday(slot) < item.TargetPortions
               && PortionsPossible(dishId) > 0;
    }

    /// <summary>At the stove: takes the ingredients (onto the stove, carried by the cook) and starts cooking.</summary>
    private bool StartCooking(Worker worker, CookTask task)
    {
        var recipe = RecipeOf(task.DishId);
        if (PortionsPossible(task.DishId) == 0) return false;

        foreach (var ingredient in recipe.Ingredients)
        {
            var left = ingredient.Quantity;
            foreach (var (from, lot) in IngredientCandidates(ingredient.ProductId))
            {
                var n = Math.Min(left, lot.Quantity);
                var taken = Inventory.Get(from).TakeFrom(lot, n);
                if (IsNearExpiry(lot)) Economy.Today.AddKitchenRescued(checked(n * lot.UnitCostCents));
                worker.Carry([taken]);
                if ((left -= n) == 0) break;
            }
        }

        _cookedToday[task.Target] = CookedToday(task.Target) + recipe.Portions;
        worker.Step = WorkStep.Cooking;
        worker.WorkTicksLeft = CookTicks(worker, recipe);
        return true;
    }

    /// <summary>Ticks for one cooking: the recipe time, faster with work speed and with each skill point.</summary>
    internal int CookTicks(Worker worker, RecipeDef recipe)
    {
        var skillPermille = 1000L + (long)Math.Max(0, worker.KnifeSkill - 1) * Data.Economy.Tasks.CookSkillSpeedPermille;
        return WorkTicks(recipe.CookMinutes, (int)(EffectiveSpeed(worker) * skillPermille / 1000));
    }

    /// <summary>How much more than the set price a worker's dishes sell for, from their skill.</summary>
    public int PriceBonusOf(Worker worker) => Math.Max(0, worker.KnifeSkill - 1) * Data.Economy.Tasks.CookSkillPricePermille;

    /// <summary>The ingredients become the dish's portions, together costing exactly what the ingredients cost.</summary>
    private void FinishCooking(Worker worker, CookTask task)
    {
        var used = worker.DropAll();
        if (used.Count == 0)
        {
            FinishTask(worker, completed: false);
            return;
        }

        var portions = RecipeOf(task.DishId).Portions;
        var total = used.Sum(l => checked(l.Quantity * l.UnitCostCents));
        var (each, extra) = (total / portions, (int)(total % portions));
        var dish = new StockLot(task.DishId, portions - extra, Clock.Day, each, PriceBonusOf(worker));
        worker.Carry(extra == 0 ? [dish] : [dish, dish with { Quantity = extra, UnitCostCents = each + 1 }]);
        worker.Step = WorkStep.Carrying;
    }

    private void PutOutDish(Worker worker, CookTask task)
    {
        var slot = Inventory.GetOrCreate(task.Target);
        var wasEmpty = slot.Count(task.DishId) == 0;
        var dishes = worker.DropAll();
        foreach (var lot in dishes) slot.Add(lot);
        _events.Add(new ItemsShelved(task.Target, task.DishId, dishes.Sum(l => l.Quantity), wasEmpty));
    }

    private void ForgetStation(int instanceId)
    {
        foreach (var slot in _menu.Keys.Where(s => s.FixtureInstanceId == instanceId).ToList())
        {
            _menu.Remove(slot);
            _cookedToday.Remove(slot);
        }
    }
}
