using ChineseGrocery.Sim.Calendar;
using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;
using static ChineseGrocery.Sim.Tests.Calendar.CalendarScenario;

namespace ChineseGrocery.Sim.Tests.Calendar;

/// <summary>Spec 15.2: festival decorations are bought once, hung on the wall, stored and reused; the bonus only counts in prep and on the day.</summary>
public class DecorationTests
{
    private static readonly FestivalsDef Festivals = SimTestKit.RealData.Festivals!;

    private static RejectReason? Send(GameSim sim, SimCommand command)
    {
        sim.DrainEvents();
        sim.Run(command);
        return sim.DrainEvents().OfType<CommandRejected>().SingleOrDefault()?.Reason;
    }

    [Fact]
    public void Buying_costs_the_price_once_and_keeps_it_stored()
    {
        var sim = Store(Quiet());
        var cash = sim.Economy.CashCents;
        var lantern = Festivals.Decorations.Single(d => d.Id == "rabbit_lantern");

        Assert.Null(Send(sim, new BuyDecoration("rabbit_lantern")));
        Assert.Equal(RejectReason.AlreadyOwned, Send(sim, new BuyDecoration("rabbit_lantern")));
        Assert.Equal(RejectReason.UnknownDecoration, Send(sim, new BuyDecoration("disco_ball")));

        Assert.Equal(cash - lantern.CostCents, sim.Economy.CashCents);
        Assert.Equal(lantern.CostCents, sim.Economy.Today.FixtureSpendCents);
        Assert.Equal([new OwnedDecoration("rabbit_lantern", null)], sim.Decorations);
    }

    [Fact]
    public void Hanging_takes_a_free_wall_cell_and_never_one_with_a_lantern_on_it()
    {
        var sim = Store(Quiet());
        var stageLanterns = sim.Data.Stages[0].Decorations!.Select(d => d.Cell.X).ToHashSet();
        sim.Run(new BuyDecoration("couplet_left"), new BuyDecoration("couplet_right"));

        Assert.Equal(RejectReason.NotOwned, Send(sim, new HangDecoration("rabbit_lantern")));
        Assert.Equal(RejectReason.WallTaken, Send(sim, new HangDecoration("couplet_left", stageLanterns.First())));
        Assert.Equal(RejectReason.OutOfBounds, Send(sim, new HangDecoration("couplet_left", sim.Layout.Size.W)));
        Assert.Null(Send(sim, new HangDecoration("couplet_left", 4)));
        Assert.Equal(RejectReason.WallTaken, Send(sim, new HangDecoration("couplet_right", 4)));
        Assert.Null(Send(sim, new HangDecoration("couplet_right")));

        var right = sim.Decorations.Single(d => d.DecorationId == "couplet_right").WallX!.Value;
        Assert.DoesNotContain(right, stageLanterns);
        Assert.NotEqual(4, right);
    }

    [Fact]
    public void The_bonus_counts_only_in_the_prep_days_and_on_the_day_while_hung()
    {
        var sim = Store(Quiet());
        int Mooncakes(int day)
        {
            sim.Clock.Day = day;
            return sim.DemandOf(Product(sim, "mooncake")).Permille;
        }

        var bare = new[] { 40, MidAutumn - 3, MidAutumn, MidAutumn + 1 }.Select(Mooncakes).ToList();
        sim.Run(new BuyDecoration("rabbit_lantern"), new HangDecoration("rabbit_lantern"));
        var hung = new[] { 40, MidAutumn - 3, MidAutumn, MidAutumn + 1 }.Select(Mooncakes).ToList();

        Assert.Equal(bare[0], hung[0]);
        Assert.Equal(bare[1] * Festivals.DecorationBonusPermille / 1000, hung[1]);
        Assert.Equal(bare[2] * Festivals.DecorationBonusPermille / 1000, hung[2]);
        Assert.Equal(bare[3], hung[3]);

        // A decoration for another festival does nothing for mooncakes.
        sim.Run(new StoreDecoration("rabbit_lantern"), new BuyDecoration("couplet_left"), new HangDecoration("couplet_left"));
        Assert.Equal(bare[2], Mooncakes(MidAutumn));
    }

    [Fact]
    public void A_stored_decoration_is_hung_again_next_year_without_buying_it_again()
    {
        var sim = Store(Quiet());
        sim.Run(new BuyDecoration("rabbit_lantern"), new HangDecoration("rabbit_lantern"));
        sim.Clock.Day = MidAutumn + 3;
        sim.Run(new StoreDecoration("rabbit_lantern"));
        var cash = sim.Economy.CashCents;

        sim.Clock.Day = MidAutumn + 112 - 2;
        Assert.Null(Send(sim, new HangDecoration("rabbit_lantern")));

        Assert.Equal(cash, sim.Economy.CashCents);
        Assert.True(sim.IsDecorated(Festivals.Festivals.Single(f => f.Id == "mid_autumn")));
    }
}
