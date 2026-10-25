using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Time;
using ChineseGrocery.SimRunner;

namespace ChineseGrocery.Sim.Tests.Inventory;

/// <summary>The parents' store opens day 1 with stocked shelves and a backroom load (playtest, 2026-10-24).</summary>
public class OpeningStockTests
{
    private static readonly GameData Real = SimTestKit.RealData;

    [Fact]
    public void Starting_shelves_are_assigned_and_full_with_the_rest_in_the_backroom()
    {
        var sim = new GameSim(Real, 1);
        var backroom = StockLocation.Storage(sim.InstanceOf("backroom"));

        foreach (var (entry, index) in Real.Economy.StartingLayout.Select((e, i) => (e, i)).Where(x => x.e.Slots is not null))
        {
            var instance = sim.Layout.Fixtures[index].InstanceId;
            for (var slot = 0; slot < entry.Slots!.Count; slot++)
            {
                var productId = entry.Slots[slot]!;
                var location = new StockLocation(instance, slot);
                var full = Real.Products.Single(p => p.Id == productId).UnitsPerShelfSlot;
                var stocked = Real.Economy.StartingStock!.Single(s => s.ProductId == productId).Quantity;

                Assert.Equal(productId, sim.Shelves.ProductAt(location));
                Assert.Equal(Math.Min(full, stocked), sim.Inventory.Count(location, productId));
                Assert.Equal(Math.Max(0, stocked - full), sim.Inventory.Count(backroom, productId));
            }
        }

        Assert.Equal(Real.Economy.StartingCashCents, sim.Economy.CashCents);
        // A new game also queues the opening dialogue.
        var events = sim.DrainEvents();
        Assert.IsType<DayStarted>(events[0]);
        Assert.All(events.Skip(1), e => Assert.IsType<Sim.Story.DialogueQueued>(e));
    }

    [Fact]
    public void Day_one_customers_find_things_to_buy()
    {
        var sim = new GameSim(Real, 1);

        sim.OpenAndRunToClose();

        Assert.True(sim.Reports[0].Customers > 0);
        Assert.True(sim.Reports[0].RevenueCents == 0, "nobody has served yet without a cashier");
        Assert.True(sim.Reports[0].ItemsNotFound < sim.Reports[0].Customers);
    }

    [Fact]
    public void Whole_game_with_opening_stock_balances_and_accounts_for_every_unit()
    {
        var sim = new GameSim(Real, 3);
        var bot = new PlayerBot(stockTarget: 40);
        for (var d = 0; d < 12; d++) bot.PlayDay(sim);

        Assert.True(sim.Reports[0].RevenueCents > 0, "day 1 now sells from opening stock");
        Assert.All(sim.Reports, r => Assert.Equal(r.RevenueCents - r.PurchaseCostCents - r.WagesCents - r.FixtureSpendCents - r.LoanPaymentCents, r.CashEndCents - r.CashStartCents));

        // Opening stock is free, so it joins what was paid on the "came in" side.
        var openingValue = Real.Economy.StartingStock!.Sum(s => s.Quantity * Real.Products.Single(p => p.Id == s.ProductId).CostCents);
        var cameIn = sim.Reports.Sum(r => r.PurchaseCostCents) + openingValue;
        var accounted = sim.PendingOrders.Sum(o => o.Quantity * o.UnitCostCents)
                        + sim.Inventory.Locations.SelectMany(l => l.Lots).Sum(l => l.Quantity * l.UnitCostCents)
                        + sim.Workers.SelectMany(w => w.Carrying).Sum(l => l.Quantity * l.UnitCostCents)
                        + sim.Reports.Sum(r => r.CostOfGoodsSoldCents + r.LossCents);
        Assert.Equal(cameIn, accounted);
    }

    private static GameData WithEconomy(Func<EconomyDef, EconomyDef> change) => Real with { Economy = change(Real.Economy) };

    [Fact]
    public void Opening_stock_that_does_not_fit_the_backroom_is_reported()
    {
        var data = WithEconomy(e => e with { StartingStock = [new StockEntryDef("rice_jasmine", 10_000)] });

        Assert.Contains(DataValidator.Validate(data), e => e.Contains("startingStock: needs"));
    }

    [Fact]
    public void Opening_slots_must_suit_the_shelf()
    {
        var layout = Real.Economy.StartingLayout.Select(e => e.Slots is null ? e : e with { Slots = ["pork_belly"] }).ToList();
        var data = WithEconomy(e => e with { StartingLayout = layout });

        Assert.Contains(DataValidator.Validate(data), e => e.Contains("'pork_belly' cannot be kept on a shelf"));
    }

    [Fact]
    public void Opening_stock_needs_storage_that_can_keep_it()
    {
        var data = WithEconomy(e => e with { StartingStock = [new StockEntryDef("pork_primal", 1)] });

        Assert.Contains(DataValidator.Validate(data), e => e.Contains("no starting storage area can keep 'pork_primal'"));
    }
}
