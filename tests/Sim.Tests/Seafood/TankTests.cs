using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Produce;
using ChineseGrocery.Sim.Save;
using ChineseGrocery.Sim.Seafood;
using ChineseGrocery.Sim.Staff;
using ChineseGrocery.Sim.Tasks;
using ChineseGrocery.Sim.Time;
using static ChineseGrocery.Sim.Tests.Customers.CustomerScenario;
using static ChineseGrocery.Sim.Tests.Seafood.SeafoodScenario;

namespace ChineseGrocery.Sim.Tests.Seafood;

/// <summary>Spec 12.3 and 12.5: water quality, deaths, cleaning, taking the dead out and where they go.</summary>
public class TankTests
{
    private static readonly SeafoodSettingsDef Settings = SimTestKit.Data.Economy.Seafood!;
    private const int TicksPerHour = 360;

    [Fact]
    public void Water_gets_dirty_faster_the_fuller_the_tank()
    {
        var sim = Store();
        var few = Tank(sim, 7, Fish);
        var many = Tank(sim, 8, Fish);
        Stock(sim, few, 0, Fish, 1);
        Stock(sim, many, 0, Fish, 6);
        Open(sim);

        TickFor(sim, 2 * TicksPerHour);

        Assert.True(sim.WaterQualityOf(few) < 100);
        Assert.True(sim.WaterQualityOf(many) < sim.WaterQualityOf(few), $"few {sim.WaterQualityOf(few)}, many {sim.WaterQualityOf(many)}");
    }

    [Fact]
    public void Dead_animals_left_in_the_tank_foul_the_water_faster()
    {
        var sim = Store();
        var clean = Tank(sim, 7, Fish);
        var withDead = Tank(sim, 8, Fish);
        Stock(sim, clean, 0, Fish, 3);
        Stock(sim, withDead, 0, Fish, 3);
        sim.AddDead(withDead, DeadLot(sim, Fish, 2));
        Open(sim);

        TickFor(sim, 2 * TicksPerHour);

        Assert.True(sim.WaterQualityOf(withDead) < sim.WaterQualityOf(clean));
    }

    [Fact]
    public void Water_only_changes_while_the_store_is_open()
    {
        var sim = Store();
        var tank = Tank(sim, 7, Fish);
        Stock(sim, tank, 0, Fish, 6);

        TickFor(sim, 2 * TicksPerHour);

        Assert.Equal(100, sim.WaterQualityOf(tank));
    }

    [Theory]
    [InlineData(100, 30)]
    [InlineData(60, 60)]
    [InlineData(30, 180)]
    [InlineData(15, 315)]
    [InlineData(0, 450)]
    public void Death_chance_rises_as_the_water_gets_worse(int quality, int permille)
    {
        // live_fish dies at 30 permille a night in clean water (placeholder); the curve multiplies that.
        Assert.Equal(permille, Mortality.ChancePermille(Settings, SimTestKit.Data.Products.Single(p => p.Id == Fish), quality));
    }

    [Fact]
    public void Death_chance_never_goes_above_certain()
    {
        var hardy = SimTestKit.Data.Products.Single(p => p.Id == Fish);
        var fragile = hardy with { Live = hardy.Live! with { DeathBasePermille = 500 } };

        Assert.Equal(1000, Mortality.ChancePermille(Settings, fragile, 0));
    }

    private static (int Dead, string Json) DieOvernight(int quality, ulong seed)
    {
        var sim = Store(seed: seed);
        var tank = Tank(sim, 7, Fish, Shrimp);
        Stock(sim, tank, 0, Fish, 6);
        Stock(sim, tank, 1, Shrimp, 12);
        sim.TankCleaningDisabled = true;
        sim.SetWaterQuality(tank, quality);
        sim.OpenAndRunToClose();
        return (sim.DeadIn(tank).Sum(l => l.Quantity), SaveGame.Write(sim));
    }

    [Fact]
    public void Dirty_water_kills_more_and_the_same_seed_kills_the_same()
    {
        var clean = Enumerable.Range(1, 5).Sum(seed => DieOvernight(100, (ulong)seed).Dead);
        var dirty = Enumerable.Range(1, 5).Sum(seed => DieOvernight(10, (ulong)seed).Dead);

        Assert.True(dirty > clean * 3, $"dirty {dirty}, clean {clean}");
        Assert.Equal(DieOvernight(10, 7), DieOvernight(10, 7));
    }

    [Fact]
    public void Deaths_are_counted_in_the_report()
    {
        var sim = Store();
        var tank = Tank(sim, 7, Fish);
        Stock(sim, tank, 0, Fish, 6);
        sim.TankCleaningDisabled = true;
        sim.SetWaterQuality(tank, 0);
        sim.OpenAndRunToClose();

        var dead = sim.DeadIn(tank).Sum(l => l.Quantity);
        Assert.True(dead > 0);
        Assert.Equal(dead, sim.Reports[^1].SeafoodDeaths);
        Assert.Equal(6 - dead, LiveUnits(sim, tank));
    }

    [Fact]
    public void Taking_the_dead_out_turns_fish_and_lobster_into_chilled_goods_and_throws_shrimp_and_crab_away()
    {
        var sim = Store();
        var tank = Tank(sim, 7, Fish);
        var ice = Place(sim, "ice_table", 6, 3, ChilledFish);
        var cold = Place(sim, "cold_storage", 9, 4);
        sim.AddDead(tank, DeadLot(sim, Fish, 2));
        sim.AddDead(tank, DeadLot(sim, Lobster, 1));
        sim.AddDead(tank, DeadLot(sim, Shrimp, 3));
        sim.AddDead(tank, DeadLot(sim, Crab, 1));
        sim.Tick();

        sim.WalkPlayerTo(new GridPoint(7, 4));
        sim.Run(new PlayerInteract(tank));
        TickUntil(sim, () => sim.Player.Step == WorkStep.Idle);

        Assert.Empty(sim.DeadIn(tank));
        var chilled = Assert.Single(sim.Inventory.Get(new StockLocation(ice, 0)).Lots);
        Assert.Equal((ChilledFish, 2, Def(sim, Fish).CostCents), (chilled.ProductId, chilled.Quantity, chilled.UnitCostCents));
        Assert.Equal(FreshnessTier.Fresh, Freshness.TierOf(Def(sim, ChilledFish), chilled, sim.Clock.Day));
        Assert.Equal(FreshnessTier.Normal, Freshness.TierOf(Def(sim, ChilledFish), chilled, sim.Clock.Day + 1));
        Assert.Equal(1, sim.Inventory.Count(StockLocation.Storage(cold), ChilledLobster));
        var thrownAway = 3 * Def(sim, Shrimp).CostCents + Def(sim, Crab).CostCents;
        Assert.Equal(thrownAway, sim.Economy.Today.SeafoodLossCents);
        Assert.Equal(thrownAway, sim.Economy.Today.LossCents);
    }

    [Fact]
    public void Live_seafood_left_in_storage_for_more_than_a_business_day_all_dies()
    {
        var sim = Store();
        Tank(sim, 7, Fish);
        sim.Run(new PlaceOrder(Fish, 6));
        sim.RunFullDay();
        var backroom = sim.Layout.Fixtures.Single(f => f.FixtureId == "backroom").InstanceId;
        Assert.Equal(6, sim.Inventory.Count(StockLocation.Storage(backroom), Fish));

        // The day it arrives it can still be put in a tank.
        sim.OpenAndRunToClose();
        Assert.Equal(6, sim.Inventory.Count(StockLocation.Storage(backroom), Fish));

        sim.Run(new StartNextDay());
        sim.OpenAndRunToClose();
        Assert.Equal(0, sim.Inventory.Count(StockLocation.Storage(backroom), Fish));
        Assert.Equal(6, sim.Reports[^1].SeafoodDeaths);
        Assert.Equal(6 * Def(sim, Fish).CostCents, sim.Reports[^1].SeafoodLossCents);
    }

    [Fact]
    public void A_clean_task_appears_below_the_threshold_and_cleaning_restores_the_water()
    {
        var sim = Store();
        var tank = Tank(sim, 7, Fish);
        sim.SetWaterQuality(tank, Settings.CleanBelow);
        sim.Tick();
        Assert.Empty(sim.Tasks.Tasks.OfType<CleanTankTask>());

        sim.SetWaterQuality(tank, Settings.CleanBelow - 1);
        sim.Tick();
        var task = Assert.Single(sim.Tasks.Tasks.OfType<CleanTankTask>());
        Assert.Equal(tank, task.TankInstanceId);

        sim.WalkPlayerTo(new GridPoint(7, 4));
        sim.Run(new PlayerInteract(tank));
        Assert.Equal(sim.Player.Id, sim.Tasks.ClaimedBy(task.Id));
        Assert.False(sim.Tasks.TryClaim(task.Id, 99));
        TickUntil(sim, () => sim.Player.Step == WorkStep.Idle);

        Assert.Equal(100, sim.WaterQualityOf(tank));
        Assert.Empty(sim.Tasks.Tasks.OfType<CleanTankTask>());
    }

    [Fact]
    public void A_remove_dead_task_appears_when_something_died_and_only_one_worker_holds_it()
    {
        var sim = Store();
        var tank = Tank(sim, 7, Fish);
        sim.Tick();
        Assert.Empty(sim.Tasks.Tasks.OfType<RemoveDeadTask>());

        sim.AddDead(tank, DeadLot(sim, Fish, 1));
        sim.Tick();
        var task = Assert.Single(sim.Tasks.Tasks.OfType<RemoveDeadTask>());

        sim.WalkPlayerTo(new GridPoint(7, 4));
        sim.Run(new PlayerInteract(tank));
        Assert.Equal(sim.Player.Id, sim.Tasks.ClaimedBy(task.Id));
        Assert.False(sim.Tasks.TryClaim(task.Id, 99));
    }

    [Fact]
    public void The_fishmonger_cleans_tanks_on_their_own()
    {
        var sim = Store();
        var tank = Tank(sim, 7, Fish);
        sim.Run(new HireEmployee("fishmonger"));
        sim.SetWaterQuality(tank, 10);

        TickUntil(sim, () => sim.WaterQualityOf(tank) == 100);

        Assert.Contains(sim.Workers, w => w.RoleId == "fishmonger");
    }

    [Fact]
    public void A_tank_with_dead_animals_in_it_cannot_be_sold()
    {
        var sim = Store();
        var tank = Tank(sim, 7, Fish);
        sim.AddDead(tank, DeadLot(sim, Fish, 1));
        sim.DrainEvents();

        sim.Run(new SellFixture(tank));

        Assert.Equal(RejectReason.FixtureNotEmpty, sim.DrainEvents().OfType<CommandRejected>().Single().Reason);
    }
}
