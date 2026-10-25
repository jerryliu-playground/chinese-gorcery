using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Customers;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Reputation;
using ChineseGrocery.Sim.Time;
using static ChineseGrocery.Sim.Tests.Reputation.ReputationScenario;

namespace ChineseGrocery.Sim.Tests.Reputation;

/// <summary>Spec 16.5: restaurant group orders — offers, accepting, setting goods aside, delivery and cancellation.</summary>
public class GroupOrderTests
{
    private const string Buyer = "restaurant_buyer";
    private const string Sauce = "soy_sauce_light";
    private const int Units = 10;
    private const int Days = 2;

    /// <summary>
    /// Restaurant buyers only, shopping for sauce, who always offer an order for 10 rice due in 2 days once they have
    /// paid. The first shelf's slot 0 is assigned to rice (empty) and slot 1 holds sauce.
    /// </summary>
    private static GameSim SauceBuyers()
    {
        var sim = SimTestKit.NewSim(data: Only(Buyer, perHour: 6, wants: Department.Sauces, reputation: r => r with
        {
            GroupOrders = r.GroupOrders with
            {
                OfferPermille = 1000,
                Lines = new IntRange(1, 1),
                DeadlineDays = new IntRange(Days, Days),
                Candidates = [new GroupOrderCandidateDef(Rice, new IntRange(Units, Units))],
            },
        }));
        Shelve(sim, Sauce, 200, slot: 1);
        sim.Run(new AssignShelfSlot(sim.InstanceOf("shelf"), 0, Rice));
        return sim;
    }

    /// <summary>Plays a day so a buyer offers an order; the store is left closed.</summary>
    private static GroupOrder OfferedOn(GameSim sim)
    {
        PlayDay(sim);
        return Assert.Single(sim.GroupOrders);
    }

    [Fact]
    public void A_paying_restaurant_buyer_offers_an_order_at_the_wholesale_price()
    {
        var sim = SauceBuyers();

        var offer = OfferedOn(sim);

        Assert.Equal(GroupOrderStatus.Offered, offer.Status);
        var line = Assert.Single(offer.Lines);
        Assert.Equal((Rice, Units), (line.ProductId, line.Units));
        var suggested = sim.Data.Products.Single(p => p.Id == Rice).SuggestedPriceCents;
        Assert.Equal(suggested * Rules.GroupOrders.WholesalePermille / 1000, line.UnitPriceCents);
        Assert.True(line.UnitPriceCents < suggested);
        Assert.Equal(Days, offer.DeadlineDays);
    }

    [Fact]
    public void Only_one_offer_waits_at_a_time()
    {
        var sim = SauceBuyers();

        var events = PlayDay(sim);

        Assert.True(events.OfType<CheckoutCompleted>().Count() > 1);
        Assert.Single(events.OfType<GroupOrderOffered>());
    }

    [Fact]
    public void An_unanswered_offer_lapses_when_the_next_day_starts()
    {
        var sim = SauceBuyers();
        var offer = OfferedOn(sim);

        sim.Run(new StartNextDay());

        Assert.Empty(sim.GroupOrders);
        Assert.Contains(sim.DrainEvents().OfType<GroupOrderClosed>(), c => c.OrderId == offer.Id && c.Outcome == GroupOrderOutcome.Expired);
    }

    [Fact]
    public void Declining_drops_the_offer_without_touching_reputation()
    {
        var sim = SauceBuyers();
        var offer = OfferedOn(sim);
        var reputation = sim.Reputation;

        sim.Run(new DeclineGroupOrder(offer.Id));

        Assert.Empty(sim.GroupOrders);
        Assert.Equal(reputation, sim.Reputation);
    }

    [Fact]
    public void Accepted_orders_set_goods_aside_so_they_are_not_shelved()
    {
        var sim = SauceBuyers();
        var offer = OfferedOn(sim);
        sim.Run(new AcceptGroupOrder(offer.Id));
        sim.Run(new PlaceOrder(Rice, Units + 4));
        sim.Run(new StartNextDay());

        var active = Assert.Single(sim.GroupOrders);
        Assert.Equal(GroupOrderStatus.Active, active.Status);
        Assert.Equal(Units, active.ReservedOf(Rice));
        Assert.Equal(4, sim.Inventory.Total(Rice));

        sim.Run(new RestockAll());
        Assert.Equal(4, sim.Inventory.Count(new StockLocation(sim.InstanceOf("shelf"), 0), Rice));
        Assert.Equal(Units, sim.GroupOrders.Single().ReservedOf(Rice));
    }

    [Fact]
    public void Delivered_on_time_the_order_is_paid_and_reputation_rises()
    {
        var sim = SauceBuyers();
        var offer = OfferedOn(sim);
        sim.Run(new AcceptGroupOrder(offer.Id));
        sim.Run(new PlaceOrder(Rice, Units));
        sim.Run(new StartNextDay());

        var events = PlayDay(sim);

        Assert.Empty(sim.GroupOrders);
        Assert.Contains(events.OfType<GroupOrderClosed>(), c => c.OrderId == offer.Id && c.Outcome == GroupOrderOutcome.Delivered && c.PaymentCents == offer.PaymentCents);
        var nudge = events.OfType<ReputationChanged>().Single(c => c.ReasonKey == "reputation.reason.group_order_delivered");
        Assert.Equal(Rules.GroupOrders.DeliveredReputation, nudge.After - nudge.Before);
        Assert.Equal(offer.PaymentCents, sim.Reports[^1].Sales.Single(s => s.ProductId == Rice).RevenueCents);
        Assert.Equal(Units, sim.Reports[^1].Sales.Single(s => s.ProductId == Rice).Units);
    }

    [Fact]
    public void Short_at_the_deadline_the_order_is_cancelled_the_goods_go_back_and_reputation_falls()
    {
        var sim = SauceBuyers();
        var offer = OfferedOn(sim);
        sim.Run(new AcceptGroupOrder(offer.Id));
        var due = sim.GroupOrders.Single().DueDay!.Value;
        sim.Run(new PlaceOrder(Rice, Units - 3));
        sim.Run(new StartNextDay());

        var closed = new List<SimEvent>();
        while (sim.GroupOrders.Count > 0)
        {
            Assert.True(sim.Clock.Day <= due);
            closed.AddRange(PlayDay(sim));
            if (sim.GroupOrders.Count > 0) sim.Run(new StartNextDay());
        }

        Assert.Equal(due, sim.Clock.Day);
        Assert.Contains(closed.OfType<GroupOrderClosed>(), c => c.OrderId == offer.Id && c.Outcome == GroupOrderOutcome.Failed);
        var nudge = closed.OfType<ReputationChanged>().Single(c => c.ReasonKey == "reputation.reason.group_order_failed");
        Assert.Equal(Rules.GroupOrders.FailedReputation, nudge.After - nudge.Before);
        Assert.Equal(Units - 3, sim.Inventory.Count(Backroom(sim), Rice));
    }

    [Fact]
    public void No_new_offers_while_an_order_runs()
    {
        var sim = SauceBuyers();
        var offer = OfferedOn(sim);
        sim.Run(new AcceptGroupOrder(offer.Id));
        sim.Run(new StartNextDay());

        var events = PlayDay(sim);

        Assert.Empty(events.OfType<GroupOrderOffered>());
        Assert.Single(sim.GroupOrders);
    }

    [Fact]
    public void Accepting_needs_a_real_offer()
    {
        var sim = SimTestKit.NewSim();

        sim.Run(new AcceptGroupOrder(99));

        Assert.Equal(RejectReason.UnknownGroupOrder, sim.DrainEvents().Single<CommandRejected>().Reason);
    }
}
