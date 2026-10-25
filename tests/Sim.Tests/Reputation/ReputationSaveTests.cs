using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Reputation;
using ChineseGrocery.Sim.Save;
using ChineseGrocery.Sim.Time;
using static ChineseGrocery.Sim.Tests.Reputation.ReputationScenario;

namespace ChineseGrocery.Sim.Tests.Reputation;

/// <summary>Spec 16.7.3: reputation, trends, group orders and reviews survive a save and play on the same.</summary>
public class ReputationSaveTests
{
    private static GameSim Reload(GameSim sim) => SaveGame.Read(sim.Data, SaveGame.Write(sim));

    /// <summary>Restaurant buyers and teens; a group order accepted and part set aside; labels bought; a day of reviews.</summary>
    private static GameSim BusyStore()
    {
        var data = Only("restaurant_buyer", perHour: 8, wants: Department.Sauces, reputation: r => r with
        {
            GroupOrders = r.GroupOrders with
            {
                OfferPermille = 1000,
                Lines = new IntRange(1, 1),
                DeadlineDays = new IntRange(3, 3),
                Candidates = [new GroupOrderCandidateDef(Rice, new IntRange(10, 10))],
            },
            Trends = r.Trends with { SeekPermille = 1000, HeartPermille = 1000 },
        });
        data = data with { CustomerTypes = data.CustomerTypes.Select(t => t.Id == "abc_teen" ? t with { ArrivalWeight = 1, MinStars = 1 } : t).ToList() };
        var sim = SimTestKit.NewSim(seed: 6, data: data);
        Shelve(sim, "soy_sauce_light", 300, slot: 1);
        Shelve(sim, sim.TrendingProducts[0], 300, slot: 2);
        sim.Run(new BuyBilingualLabels());
        PlayDay(sim);
        var offer = sim.GroupOrders.Single();
        sim.Run(new AcceptGroupOrder(offer.Id));
        sim.Run(new PlaceOrder(Rice, 6));
        sim.Run(new StartNextDay());
        return sim;
    }

    [Fact]
    public void Round_trip_keeps_reputation_trends_orders_and_reviews()
    {
        var sim = BusyStore();
        sim.WalkPlayerTo(CashierCell);
        sim.Run(new OpenStore());
        for (var i = 0; i < 2000; i++) sim.Tick();

        var json = SaveGame.Write(sim);
        var loaded = SaveGame.Read(sim.Data, json);

        Assert.Equal(json, SaveGame.Write(loaded));
        Assert.Equal(sim.Reputation, loaded.Reputation);
        Assert.Equal(sim.TrendingProducts, loaded.TrendingProducts);
        Assert.Equal(sim.Reviews, loaded.Reviews);
        Assert.True(loaded.HasBilingualLabels);
        Assert.Equal(6, loaded.GroupOrders.Single().ReservedOf(Rice));
        Assert.NotEmpty(loaded.Reviews);
    }

    [Fact]
    public void Loading_mid_day_plays_on_exactly_as_without_stopping()
    {
        var sim = BusyStore();
        sim.WalkPlayerTo(CashierCell);
        sim.Run(new OpenStore());
        for (var i = 0; i < 1500; i++) sim.Tick();
        var loaded = Reload(sim);

        foreach (var s in new[] { sim, loaded })
        {
            while (s.Clock.Phase != DayPhase.Closed) s.Tick();
            s.Run(new StartNextDay());
            s.OpenAndRunToClose();
        }

        Assert.Equal(SimSnapshot.Hash(sim), SimSnapshot.Hash(loaded));
        Assert.Equal(sim.Reviews, loaded.Reviews);
        Assert.Equal(sim.ReputationHistory, loaded.ReputationHistory);
    }

    [Fact]
    public void Old_saves_without_reputation_start_from_the_data()
    {
        var sim = SimTestKit.NewSim(seed: 3);
        var json = SaveGame.Write(sim);
        var start = json.IndexOf(",\"reputation\":", StringComparison.Ordinal);
        Assert.True(start > 0);
        var old = json[..start] + "}";

        var loaded = SaveGame.Read(sim.Data, old);

        Assert.Equal(Rules.StartReputation, loaded.Reputation);
        Assert.NotEmpty(loaded.TrendingProducts);
    }
}
