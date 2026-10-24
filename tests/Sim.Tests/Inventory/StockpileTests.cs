using ChineseGrocery.Sim.Inventory;

namespace ChineseGrocery.Sim.Tests.Inventory;

public class StockpileTests
{
    private static Stockpile WithLots(params StockLot[] lots)
    {
        var pile = new Stockpile(1);
        foreach (var lot in lots) pile.Add(lot);
        return pile;
    }

    [Fact]
    public void Count_sums_lots_of_a_product()
    {
        var pile = WithLots(new StockLot("soy_milk", 5, 3, 90), new StockLot("soy_milk", 2, 4, 90), new StockLot("jasmine_tea", 7, 3, 70));

        Assert.Equal(7, pile.Count("soy_milk"));
        Assert.Equal(0, pile.Count("rice_jasmine"));
    }

    [Fact]
    public void Lots_with_same_product_expiry_and_cost_merge()
    {
        var pile = WithLots(new StockLot("soy_milk", 5, 3, 90), new StockLot("soy_milk", 2, 3, 90));

        Assert.Equal(new StockLot("soy_milk", 7, 3, 90), Assert.Single(pile.Lots));
    }

    [Fact]
    public void Take_uses_earliest_expiry_first()
    {
        var pile = WithLots(new StockLot("soy_milk", 5, 9, 90), new StockLot("soy_milk", 3, 4, 90));

        var taken = pile.Take("soy_milk", 4);

        Assert.Equal([new StockLot("soy_milk", 3, 4, 90), new StockLot("soy_milk", 1, 9, 90)], taken);
        Assert.Equal(new StockLot("soy_milk", 4, 9, 90), Assert.Single(pile.Lots));
    }

    [Fact]
    public void Taking_more_than_stocked_fails_and_changes_nothing()
    {
        var pile = WithLots(new StockLot("soy_milk", 3, 4, 90));

        Assert.Throws<InvalidOperationException>(() => pile.Take("soy_milk", 4));
        Assert.Equal(3, pile.Count("soy_milk"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_quantities_are_rejected(int quantity)
    {
        var pile = WithLots(new StockLot("soy_milk", 3, 4, 90));

        Assert.Throws<ArgumentOutOfRangeException>(() => pile.Take("soy_milk", quantity));
        Assert.Throws<ArgumentOutOfRangeException>(() => pile.Add(new StockLot("soy_milk", quantity, 4, 90)));
        Assert.Equal(3, pile.Count("soy_milk"));
    }

    [Fact]
    public void Remove_expired_drops_lots_whose_last_day_has_passed()
    {
        var pile = WithLots(new StockLot("soy_milk", 3, 4, 90), new StockLot("soy_milk", 2, 5, 90), new StockLot("jasmine_tea", 1, 3, 70));

        var removed = pile.RemoveExpired(closingDay: 4);

        Assert.Equal([new StockLot("jasmine_tea", 1, 3, 70), new StockLot("soy_milk", 3, 4, 90)], removed);
        Assert.Equal(new StockLot("soy_milk", 2, 5, 90), Assert.Single(pile.Lots));
    }
}
