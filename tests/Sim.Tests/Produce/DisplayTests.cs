using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Produce;
using ChineseGrocery.Sim.Save;
using ChineseGrocery.Sim.Tasks;
using static ChineseGrocery.Sim.Tests.Customers.CustomerScenario;

namespace ChineseGrocery.Sim.Tests.Produce;

/// <summary>Spec 11.3: display score parts, pairing between neighbouring stands, tidiness and the Tidy task.</summary>
public class DisplayTests
{
    private static readonly DisplaySettingsDef Settings = SimTestKit.Data.Economy.Display!;

    /// <summary>Produce stands available from stage 1 in a quiet store (no customers unless asked for).</summary>
    private static GameSim Store(int perHour = 0)
    {
        var data = ScenarioData(perHour: perHour, wants: Department.Produce, patienceMinutes: 60);
        data = data with
        {
            Fixtures = data.Fixtures.Select(f => f.Id == "produce_stand" ? f with { UnlockStage = 1 } : f).ToList(),
            CustomerTypes = data.CustomerTypes.Select(t => t with { ProduceCategories = null }).ToList(),
        };
        return SimTestKit.NewSim(data: data);
    }

    private static int Stand(GameSim sim, int x, int y, params string?[] products)
    {
        sim.Run(new PlaceFixture("produce_stand", new GridPoint(x, y)));
        var id = sim.Layout.Fixtures[^1].InstanceId;
        for (var slot = 0; slot < products.Length; slot++)
        {
            if (products[slot] is { } p) sim.Run(new AssignShelfSlot(id, slot, p));
        }

        return id;
    }

    private static void Fill(GameSim sim, int stand, int slot, string productId, int units, int ageDays = 0)
    {
        var product = sim.Data.Products.Single(p => p.Id == productId);
        sim.Inventory.GetOrCreate(new StockLocation(stand, slot)).Add(new StockLot(productId, units, sim.Clock.Day + product.ShelfLifeDays - 1 - ageDays, product.CostCents));
    }

    private static int Full(string productId) => SimTestKit.Data.Products.Single(p => p.Id == productId).UnitsPerShelfSlot;

    [Fact]
    public void The_four_parts_and_the_weighted_total()
    {
        var sim = Store();
        var stand = Stand(sim, 7, 3, "napa_cabbage");
        Fill(sim, stand, 0, "napa_cabbage", Full("napa_cabbage"));

        var score = sim.DisplayScoreOf(stand);

        // One of two slots full = 50; all fresh = 100; untouched = 100; no neighbour = the neutral pairing score.
        Assert.Equal((50, 100, 100, Settings.NoNeighborPairing), (score.Fullness, score.Freshness, score.Tidiness, score.Pairing));
        var w = Settings.Weights;
        Assert.Equal((w.Fullness * 50 + w.Freshness * 100 + w.Tidiness * 100 + w.Pairing * Settings.NoNeighborPairing) / (w.Fullness + w.Freshness + w.Tidiness + w.Pairing), score.Total);
    }

    [Fact]
    public void Older_batches_pull_freshness_down()
    {
        var sim = Store();
        var stand = Stand(sim, 7, 3, "napa_cabbage");
        Fill(sim, stand, 0, "napa_cabbage", 4);
        Fill(sim, stand, 0, "napa_cabbage", 4, ageDays: 4);

        var scores = SimTestKit.Data.Economy.Freshness!.Scores;
        Assert.Equal((scores.Fresh + scores.Ripe) / 2, sim.DisplayScoreOf(stand).Freshness);
    }

    [Fact]
    public void Pairing_counts_only_touching_produce_stands()
    {
        var sim = Store();
        var cabbage = Stand(sim, 7, 3, "napa_cabbage");
        var orange = Stand(sim, 8, 3, "orange");
        var faraway = Stand(sim, 11, 6, "bok_choy");
        Stand(sim, 6, 3, "bok_choy");
        sim.Run(new SellFixture(sim.Layout.Fixtures[^1].InstanceId));
        sim.Run(new PlaceFixture("shelf", new GridPoint(5, 3), Rotation.None));

        Assert.Equal(100, sim.DisplayScoreOf(cabbage).Pairing);
        Assert.Equal(100, sim.DisplayScoreOf(orange).Pairing);
        Assert.Equal(Settings.NoNeighborPairing, sim.DisplayScoreOf(faraway).Pairing);
    }

    [Fact]
    public void Same_colour_neighbours_score_zero()
    {
        var sim = Store();
        var a = Stand(sim, 7, 3, "napa_cabbage");
        Stand(sim, 8, 3, "bok_choy");

        Assert.Equal(0, sim.DisplayScoreOf(a).Pairing);
    }

    [Theory]
    [InlineData(0, 800)]
    [InlineData(25, 900)]
    [InlineData(50, 1000)]
    [InlineData(100, 1250)]
    public void Display_score_turns_into_a_buy_chance_factor(int score, int permille)
    {
        Assert.Equal(permille, Display.BuyFactorPermille(Settings, score));
    }

    [Fact]
    public void Shoppers_mess_up_a_stand_and_a_tidy_task_appears_below_the_threshold()
    {
        var sim = Store(perHour: 360);
        var stand = Stand(sim, 7, 3, "orange");
        Fill(sim, stand, 0, "orange", Full("orange"));
        Open(sim);

        TickUntil(sim, () => sim.TidinessOf(stand) < Settings.TidyBelow);
        sim.Tick();

        var task = Assert.Single(sim.Tasks.Tasks.OfType<TidyTask>());
        Assert.Equal(stand, task.StandInstanceId);
    }

    [Fact]
    public void Player_tidies_the_stand_with_the_shared_task_logic_and_only_one_worker_holds_it()
    {
        var sim = Store(perHour: 360);
        var stand = Stand(sim, 7, 3, "orange");
        Fill(sim, stand, 0, "orange", Full("orange"));
        Open(sim);
        TickUntil(sim, () => sim.Tasks.Tasks.OfType<TidyTask>().Any());
        var task = sim.Tasks.Tasks.OfType<TidyTask>().Single();

        sim.WalkPlayerTo(new GridPoint(7, 4));
        sim.Run(new PlayerInteract(stand));
        Assert.Equal(sim.Player.Id, sim.Tasks.ClaimedBy(task.Id));
        Assert.False(sim.Tasks.TryClaim(task.Id, 99));

        TickUntil(sim, () => sim.Player.Step == WorkStep.Idle);
        Assert.Equal(100, sim.TidinessOf(stand));
        Assert.DoesNotContain(sim.Tasks.Tasks, t => t.Id == task.Id);
    }

    [Fact]
    public void Tidiness_survives_save_and_load()
    {
        var sim = Store(perHour: 360);
        var stand = Stand(sim, 7, 3, "orange");
        Fill(sim, stand, 0, "orange", Full("orange"));
        Open(sim);
        TickUntil(sim, () => sim.TidinessOf(stand) < 100);

        var loaded = SaveGame.Read(sim.Data, SaveGame.Write(sim));

        Assert.Equal(sim.TidinessOf(stand), loaded.TidinessOf(stand));
        Assert.Equal(SaveGame.Write(sim), SaveGame.Write(loaded));
    }

    [Fact]
    public void A_better_display_sells_more_produce()
    {
        int Taken(int score)
        {
            var sim = Store(perHour: 360);
            sim.DisplayScoreOverride = score;
            var stand = Stand(sim, 7, 3, "orange", "lychee");
            Fill(sim, stand, 0, "orange", 999);
            Fill(sim, stand, 1, "lychee", 999);
            Open(sim);
            for (var i = 0; i < 2000; i++) sim.Tick();
            return 2 * 999 - sim.Inventory.Count(new StockLocation(stand, 0), "orange") - sim.Inventory.Count(new StockLocation(stand, 1), "lychee");
        }

        var (good, bad) = (Taken(100), Taken(0));
        Assert.True(good > bad * 11 / 10, $"score 100: {good}, score 0: {bad}");
    }
}
