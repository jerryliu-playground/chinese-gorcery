using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Customers;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Reputation;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Tests.Reputation;

/// <summary>
/// Controlled reputation setups: only one customer type comes in (it always buys up to the reference price, a fixed
/// list size, no star or department conditions), steady arrivals, goods placed straight onto the first shelf.
/// </summary>
public static class ReputationScenario
{
    public const string Rice = "rice_jasmine";
    public static readonly GridPoint CashierCell = new(2, 4);

    public static ReputationDef Rules => SimTestKit.Data.Reputation!;

    public static GameData Only(
        string typeId,
        int perHour = 60,
        int listSize = 1,
        int patienceMinutes = 60,
        Department wants = Department.DryGoods,
        Func<ReputationDef, ReputationDef>? reputation = null,
        IReadOnlyList<ArrivalBucket>? arrivals = null)
    {
        var d = SimTestKit.Data;
        var economy = d.Economy with { CustomerArrivals = arrivals ?? [new ArrivalBucket(d.Economy.OpenMinuteOfDay, perHour)], Deli = null };
        var types = d.CustomerTypes.Select(t => t.Id == typeId
            ? t with
            {
                ArrivalWeight = 1,
                ArrivalWeightPerStar = 0,
                MinStars = 1,
                RequiresDepartments = null,
                Preferences = [new DepartmentWeight(wants, 1)],
                ShoppingListSize = new IntRange(listSize, listSize),
                PatienceMinutes = patienceMinutes,
                PriceComfortRatioPermille = 1000,
                PriceCeilingRatioPermille = 1500,
                MinBuyChancePermille = 0,
                ProduceCategories = null,
            }
            : t with { ArrivalWeight = 0 }).ToList();
        return d with { Economy = economy, CustomerTypes = types, Reputation = reputation is null ? d.Reputation : reputation(d.Reputation!) };
    }

    /// <summary>Puts units straight onto a slot of the first shelf (test shortcut) and assigns the slot.</summary>
    public static StockLocation Shelve(GameSim sim, string productId, int units, int slot = 0)
    {
        var at = new StockLocation(sim.InstanceOf("shelf"), slot);
        sim.Run(new AssignShelfSlot(at.FixtureInstanceId, at.Slot, productId));
        var cost = sim.Data.Products.Single(p => p.Id == productId).CostCents;
        sim.Inventory.GetOrCreate(at).Add(new StockLot(productId, units, 999, cost));
        return at;
    }

    public static StockLocation Backroom(GameSim sim) => StockLocation.Storage(sim.InstanceOf("backroom"));

    public static void PutInBackroom(GameSim sim, string productId, int units) =>
        sim.Inventory.GetOrCreate(Backroom(sim)).Add(new StockLot(productId, units, 999, sim.Data.Products.Single(p => p.Id == productId).CostCents));

    /// <summary>Opens with the player behind the register and runs to closing; returns every event of the day.</summary>
    public static List<SimEvent> PlayDay(GameSim sim, bool serve = true)
    {
        if (serve) sim.WalkPlayerTo(CashierCell);
        var events = new List<SimEvent>(sim.DrainEvents());
        sim.Run(new OpenStore());
        while (sim.Clock.Phase != DayPhase.Closed)
        {
            sim.Tick();
            events.AddRange(sim.DrainEvents());
        }

        events.AddRange(sim.DrainEvents());
        return events;
    }

    /// <summary>Same as <see cref="PlayDay"/>, noting the minute of the day each event happened.</summary>
    public static List<(int Minute, SimEvent Event)> PlayDayTimed(GameSim sim)
    {
        sim.WalkPlayerTo(CashierCell);
        sim.DrainEvents();
        sim.Run(new OpenStore());
        var events = new List<(int, SimEvent)>();
        while (sim.Clock.Phase != DayPhase.Closed)
        {
            var minute = sim.Clock.MinuteOfDay;
            sim.Tick();
            events.AddRange(sim.DrainEvents().Select(e => (minute, e)));
        }

        return events;
    }

    public static IEnumerable<int> Scores(IEnumerable<SimEvent> events) => events.OfType<CustomerSatisfied>().Select(s => s.Score);

    public static IEnumerable<CustomerReaction> Reactions(IEnumerable<SimEvent> events, CustomerReactionKind kind) =>
        events.OfType<CustomerReaction>().Where(r => r.Kind == kind);

    /// <summary>Sets reputation to an exact value through the public nudge.</summary>
    public static void SetReputation(GameSim sim, int value) => sim.NudgeReputation(value - sim.Reputation, "reputation.reason.test");
}
