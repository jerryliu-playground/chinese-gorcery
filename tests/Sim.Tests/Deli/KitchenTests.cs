using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Deli;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Staff;
using ChineseGrocery.Sim.Tasks;
using ChineseGrocery.Sim.Time;
using static ChineseGrocery.Sim.Tests.Customers.CustomerScenario;
using static ChineseGrocery.Sim.Tests.Deli.DeliScenario;
using static ChineseGrocery.Sim.Tests.Seafood.SeafoodScenario;

namespace ChineseGrocery.Sim.Tests.Deli;

/// <summary>Spec 13.3-13.5: the daily menu, which ingredients the kitchen takes, cooking, and what happens at closing.</summary>
public class KitchenTests
{
    private static readonly GridPoint StoveCell = new(7, 4);
    private static readonly GridPoint SteamTableWorkCell = new(8, 4);

    private static RecipeDef Recipe(string id) => SimTestKit.Data.Recipes.Single(r => r.Id == id);

    private static RejectReason? Send(GameSim sim, SimCommand command)
    {
        sim.DrainEvents();
        sim.Run(command);
        return sim.DrainEvents().OfType<CommandRejected>().SingleOrDefault()?.Reason;
    }

    [Fact]
    public void The_kitchen_takes_the_least_fresh_usable_batch_first_then_the_oldest()
    {
        var sim = DeliStore();
        var backroom = Backroom(sim);
        // Ginger keeps fresh 4 days, normal 4, ripe 2 (placeholders).
        var fresh = Put(sim, backroom, "ginger", 1, ageDays: 0);
        var normalOld = Put(sim, backroom, "ginger", 1, ageDays: 6);
        var normalNew = Put(sim, backroom, "ginger", 1, ageDays: 5);
        var ripe = Put(sim, backroom, "ginger", 1, ageDays: 8);
        var lastDay = Put(sim, backroom, "ginger", 1, ageDays: 9);
        Put(sim, backroom, "ginger", 1, ageDays: 10); // spoiled: past its last day, never used

        var order = sim.IngredientCandidates("ginger").Select(c => c.Lot).ToList();

        Assert.Equal([lastDay, ripe, normalOld, normalNew, fresh], order);
    }

    [Fact]
    public void Ingredients_come_from_every_kind_of_stock_location()
    {
        var sim = DeliStore();
        var stand = Place(sim, "produce_stand", 6, 3, "scallion");
        var ice = Place(sim, "ice_table", 9, 3, ChilledFish);
        var tank = Tank(sim, 4, Shrimp);
        Put(sim, new StockLocation(stand, 0), "scallion", 2);
        Put(sim, new StockLocation(ice, 0), ChilledFish, 1);
        Put(sim, Backroom(sim), "ginger", 1);
        Put(sim, new StockLocation(tank, 0), Shrimp, 2);

        var sources = new[] { "scallion", ChilledFish, "ginger", Shrimp }
            .Select(id => sim.IngredientCandidates(id).First().From.FixtureInstanceId)
            .ToList();

        Assert.Equal([stand, ice, sim.InstanceOf("backroom"), tank], sources);
    }

    [Fact]
    public void The_menu_is_set_before_opening_for_a_slot_of_the_right_kind_of_station()
    {
        var sim = DeliStore();
        var table = SteamTable(sim);
        var roast = RoastStation(sim);

        Assert.Null(Send(sim, new SetMenuItem(table, 0, "steamed_fish", 6)));
        Assert.Equal(new MenuItem("steamed_fish", 6), sim.Menu[new StockLocation(table, 0)]);
        Assert.Equal(RejectReason.WrongStation, Send(sim, new SetMenuItem(roast, 0, "steamed_fish", 6)));
        Assert.Equal(RejectReason.InvalidSlot, Send(sim, new SetMenuItem(table, 4, "dumplings", 6)));
        Assert.Equal(RejectReason.UnknownProduct, Send(sim, new SetMenuItem(table, 1, "rice_jasmine", 6)));
        Assert.Equal(RejectReason.NotADeliStation, Send(sim, new SetMenuItem(sim.InstanceOf("shelf"), 0, "dumplings", 6)));

        sim.Run(new OpenStore());
        Assert.Equal(RejectReason.WrongPhase, Send(sim, new SetMenuItem(table, 1, "dumplings", 6)));
    }

    [Fact]
    public void A_cook_task_appears_only_while_the_slot_is_low_and_the_target_is_not_reached()
    {
        var sim = DeliStore();
        var stove = Stove(sim);
        var table = SteamTable(sim);
        var slot = new StockLocation(table, 0);
        Put(sim, Backroom(sim), "bok_choy", 12);

        sim.Run(new SetMenuItem(table, 0, "stir_fried_bok_choy", 0));
        Assert.Empty(sim.Tasks.Tasks.OfType<CookTask>());

        sim.Run(new SetMenuItem(table, 0, "stir_fried_bok_choy", 6));
        var task = Assert.Single(sim.Tasks.Tasks.OfType<CookTask>());
        Assert.Equal((stove, slot, "stir_fried_bok_choy"), (task.StoveInstanceId, task.Target, task.DishId));

        // A full slot needs nothing.
        Put(sim, slot, "stir_fried_bok_choy", Recipe("stir_fried_bok_choy").UnitsPerSlot);
        sim.Tick();
        Assert.Empty(sim.Tasks.Tasks.OfType<CookTask>());
    }

    [Fact]
    public void No_cook_task_without_a_stove_or_without_ingredients()
    {
        var sim = DeliStore();
        var table = SteamTable(sim);
        Put(sim, Backroom(sim), "bok_choy", 12);
        sim.Run(new SetMenuItem(table, 0, "stir_fried_bok_choy", 6));
        Assert.Empty(sim.Tasks.Tasks.OfType<CookTask>());

        Stove(sim);
        sim.Run(new SetMenuItem(table, 1, "braised_pork", 6));
        Assert.Equal("stir_fried_bok_choy", Assert.Single(sim.Tasks.Tasks.OfType<CookTask>()).DishId);
    }

    /// <summary>The player cooks the dish at the stove and carries it to the steam table.</summary>
    private static void CookAndServe(GameSim sim, int stove, int table)
    {
        sim.WalkPlayerTo(StoveCell);
        sim.Run(new PlayerInteract(stove));
        Assert.Equal(WorkStep.Cooking, sim.Player.Step);
        TickUntil(sim, () => sim.Player.Step == WorkStep.Carrying);
        sim.WalkPlayerTo(SteamTableWorkCell);
        sim.Run(new PlayerInteract(table));
        TickUntil(sim, () => sim.Player.Step == WorkStep.Idle);
    }

    [Fact]
    public void Cooking_uses_up_the_ingredients_and_puts_the_portions_on_the_station_at_their_cost()
    {
        var sim = DeliStore();
        var stove = Stove(sim);
        var table = SteamTable(sim);
        var backroom = Backroom(sim);
        Put(sim, backroom, "ground_pork", 2);
        Put(sim, backroom, "napa_cabbage", 1);
        sim.Run(new SetMenuItem(table, 0, "dumplings", 10));
        var ingredientCost = sim.Inventory.Get(backroom).Lots.Where(l => l.ProductId != "napa_cabbage").Take(1).Sum(l => l.UnitCostCents)
                             + sim.Inventory.Get(backroom).Lots.Single(l => l.ProductId == "napa_cabbage").UnitCostCents;

        CookAndServe(sim, stove, table);

        var portions = sim.Inventory.Get(new StockLocation(table, 0)).Lots;
        Assert.Equal(Recipe("dumplings").Portions, portions.Sum(l => l.Quantity));
        Assert.Equal(ingredientCost, portions.Sum(l => l.Quantity * l.UnitCostCents));
        Assert.Equal(1, sim.Inventory.Count(backroom, "ground_pork"));
        Assert.Equal(0, sim.Inventory.Count(backroom, "napa_cabbage"));
        Assert.Equal(10, sim.CookedToday(new StockLocation(table, 0)));
        Assert.All(sim.Inventory.Locations.SelectMany(p => p.Lots), l => Assert.True(l.Quantity > 0));
    }

    [Fact]
    public void Ingredients_taken_from_a_shelf_leave_it_shorter()
    {
        var sim = DeliStore();
        var stove = Stove(sim);
        var table = SteamTable(sim);
        var stand = Place(sim, "produce_stand", 6, 3, "bok_choy");
        Put(sim, new StockLocation(stand, 0), "bok_choy", 10);
        sim.Run(new SetMenuItem(table, 0, "stir_fried_bok_choy", 6));

        CookAndServe(sim, stove, table);

        Assert.Equal(7, sim.Inventory.Count(new StockLocation(stand, 0), "bok_choy"));
    }

    [Fact]
    public void Near_expiry_ingredients_cooked_are_reported_as_rescued()
    {
        var sim = DeliStore();
        var stove = Stove(sim);
        var table = SteamTable(sim);
        var backroom = Backroom(sim);
        var ripe = Put(sim, backroom, "bok_choy", 2, ageDays: 2); // bok choy: 1 fresh, 1 normal, 1 ripe day
        Put(sim, backroom, "bok_choy", 5);
        sim.Run(new SetMenuItem(table, 0, "stir_fried_bok_choy", 6));

        CookAndServe(sim, stove, table);

        Assert.Equal(2 * ripe.UnitCostCents, sim.Economy.Today.KitchenRescuedCents);
    }

    [Fact]
    public void Dishes_left_at_closing_are_thrown_away_as_deli_waste()
    {
        var sim = DeliStore();
        var table = SteamTable(sim);
        var slot = new StockLocation(table, 0);
        sim.Run(new SetMenuItem(table, 0, "braised_pork", 0));
        Put(sim, slot, "braised_pork", 5);

        sim.OpenAndRunToClose();

        Assert.Equal(0, sim.Inventory.Count(slot, "braised_pork"));
        Assert.Equal(5 * 100, sim.Reports[^1].DeliLossCents);
    }

    [Fact]
    public void Nothing_is_cooked_after_closing()
    {
        var sim = DeliStore();
        Stove(sim);
        var table = SteamTable(sim);
        sim.OpenAndRunToClose();
        Put(sim, Backroom(sim), "bok_choy", 12);
        sim.Enqueue(new SetMenuItem(table, 0, "stir_fried_bok_choy", 6));

        sim.Tick();

        Assert.Empty(sim.Tasks.Tasks.OfType<CookTask>());
    }

    [Fact]
    public void The_count_of_portions_cooked_starts_again_each_day()
    {
        var sim = DeliStore();
        var stove = Stove(sim);
        var table = SteamTable(sim);
        Put(sim, Backroom(sim), "bok_choy", 3);
        sim.Run(new SetMenuItem(table, 0, "stir_fried_bok_choy", 6));
        CookAndServe(sim, stove, table);
        Assert.Equal(6, sim.CookedToday(new StockLocation(table, 0)));

        sim.RunFullDay();

        Assert.Equal(0, sim.CookedToday(new StockLocation(table, 0)));
        Assert.Equal(new MenuItem("stir_fried_bok_choy", 6), sim.Menu[new StockLocation(table, 0)]);
    }

    [Fact]
    public void A_skilled_chef_cooks_faster_and_their_dishes_sell_for_more()
    {
        var sim = DeliStore();
        Worker Cook(int skill) => new(50 + skill, isPlayer: false, new GridPoint(0, 0), 3, 1000, skill);
        var recipe = Recipe("roast_duck");

        Assert.True(sim.CookTicks(Cook(5), recipe) < sim.CookTicks(Cook(1), recipe));
        var plain = new StockLot("roast_duck", 1, sim.Clock.Day, 100);
        var fancy = plain with { PriceBonusPermille = sim.PriceBonusOf(Cook(5)) };
        Assert.True(sim.EffectivePrice(fancy) > sim.EffectivePrice(plain));
    }

    [Fact]
    public void The_chef_cooks_on_their_own()
    {
        var sim = DeliStore();
        Stove(sim);
        var table = SteamTable(sim);
        Put(sim, Backroom(sim), "bok_choy", 3);
        sim.Run(new HireEmployee("chef"));
        sim.Run(new SetMenuItem(table, 0, "stir_fried_bok_choy", 6));

        TickUntil(sim, () => sim.Inventory.Count(new StockLocation(table, 0), "stir_fried_bok_choy") > 0);
    }

    [Fact]
    public void Kitchen_only_goods_cannot_be_put_out_for_sale()
    {
        var sim = DeliStore();
        var display = Place(sim, "meat_display", 3, 3);

        Assert.Equal(RejectReason.NotForSale, Send(sim, new AssignShelfSlot(display, 0, "whole_duck")));
    }
}
