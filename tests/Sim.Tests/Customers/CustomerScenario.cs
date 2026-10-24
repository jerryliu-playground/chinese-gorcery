using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Tests.Customers;

/// <summary>
/// Controlled customer setups: one customer type (the first) that always buys at up to 100% of the reference price and
/// never above 150%, steady arrivals, and goods placed straight onto shelves.
/// Starting layout: checkout at (2,5) worked from (2,4), customers queue at (2,6).
/// </summary>
public static class CustomerScenario
{
    public const string Rice = "rice_jasmine";
    public static readonly GridPoint CashierCell = new(2, 4);

    public static GameData ScenarioData(int perHour = 60, int listSize = 1, int patienceMinutes = 30, Department wants = Department.DryGoods)
    {
        var d = SimTestKit.Data;
        var economy = d.Economy with { CustomerArrivals = [new ArrivalBucket(d.Economy.OpenMinuteOfDay, perHour)] };
        var types = d.CustomerTypes.Select((t, i) => i == 0
            ? t with
            {
                ArrivalWeight = 1,
                Preferences = [new DepartmentWeight(wants, 1)],
                ShoppingListSize = new IntRange(listSize, listSize),
                PatienceMinutes = patienceMinutes,
                PriceComfortRatioPermille = 1000,
                PriceCeilingRatioPermille = 1500,
                MinBuyChancePermille = 0,
            }
            : t with { ArrivalWeight = 0 }).ToList();
        return d with { Economy = economy, CustomerTypes = types };
    }

    /// <summary>Puts units straight onto the first shelf's slot 0 (test shortcut) and assigns the slot.</summary>
    public static StockLocation ShelveDirectly(GameSim sim, string productId, int units)
    {
        var slot = new StockLocation(sim.InstanceOf("shelf"), 0);
        sim.Run(new AssignShelfSlot(slot.FixtureInstanceId, slot.Slot, productId));
        var cost = sim.Data.Products.Single(p => p.Id == productId).CostCents;
        sim.Inventory.GetOrCreate(slot).Add(new StockLot(productId, units, 999, cost));
        return slot;
    }

    public static void TickUntil(GameSim sim, Func<bool> done, int max = 2000)
    {
        for (var i = 0; !done(); i++)
        {
            if (i == max) throw new InvalidOperationException("Condition never met.");
            sim.Tick();
        }
    }

    public static void Open(GameSim sim) => sim.Run(new OpenStore());

    public static bool AnyQueued(GameSim sim) => sim.Customers.Any(c => c.State == Sim.Customers.CustomerState.Queued);
}
