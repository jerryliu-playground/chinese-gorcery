using ChineseGrocery.Sim.Data;

namespace ChineseGrocery.Sim.Tests.Data;

/// <summary>Spec 12: seafood rows and settings are checked when the data loads.</summary>
public class SeafoodDataTests
{
    private static readonly GameData Valid = GameDataLoader.LoadFromDirectory(RepoPaths.Data);

    private static IReadOnlyList<string> ErrorsFor(GameData data) => DataValidator.Validate(data);

    private static GameData WithProduct(string id, Func<ProductDef, ProductDef> change) =>
        Valid with { Products = Valid.Products.Select(p => p.Id == id ? change(p) : p).ToList() };

    [Fact]
    public void Shipped_seafood_data_is_valid()
    {
        Assert.Empty(ErrorsFor(Valid));
        Assert.Contains(Valid.Products, p => p.Live is { KillToOrder: true, DiesAs: "chilled_fish" });
    }

    [Fact]
    public void Live_seafood_needs_live_storage_and_the_other_way_round()
    {
        Assert.Contains(ErrorsFor(WithProduct("live_fish", p => p with { Storage = StorageType.Chilled })), e => e.Contains("'live_fish'") && e.Contains("storage 'live'"));
        Assert.Contains(ErrorsFor(WithProduct("clams", p => p with { Storage = StorageType.Live })), e => e.Contains("'clams'") && e.Contains("storage 'live'"));
    }

    [Fact]
    public void A_dead_animal_must_become_a_product_that_is_not_alive()
    {
        var data = WithProduct("live_fish", p => p with { Live = p.Live! with { DiesAs = "crab" } });

        Assert.Contains(ErrorsFor(data), e => e.Contains("live.diesAs 'crab'"));
    }

    [Fact]
    public void Seafood_freshness_days_must_add_up_like_produce()
    {
        var data = WithProduct("clams", p => p with { ShelfLifeDays = 5 });

        Assert.Contains(ErrorsFor(data), e => e.Contains("'clams'") && e.Contains("freshnessDays must add up"));
    }

    [Fact]
    public void Death_curve_must_cover_all_water_qualities()
    {
        var seafood = Valid.Economy.Seafood!;
        var data = Valid with { Economy = Valid.Economy with { Seafood = seafood with { DeathMultiplierPoints = [new QualityPoint(0, 5000), new QualityPoint(90, 1000)] } } };

        Assert.Contains(ErrorsFor(data), e => e.Contains("deathMultiplierPoints"));
    }

    [Fact]
    public void Product_weights_must_name_real_products()
    {
        var data = Valid with { CustomerTypes = Valid.CustomerTypes.Select((t, i) => i == 0 ? t with { ProductWeights = [new ProductWeight("whale", 5)] } : t).ToList() };

        Assert.Contains(ErrorsFor(data), e => e.Contains("productWeights 'whale'"));
    }
}
