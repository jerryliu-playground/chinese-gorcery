using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Grid;

namespace ChineseGrocery.Sim.Tests.Data;

public class DataValidatorTests
{
    private static readonly GameData Valid = GameDataLoader.LoadFromDirectory(RepoPaths.Data);

    private static IReadOnlyList<string> ErrorsFor(GameData data) => DataValidator.Validate(data);

    private static List<T> Replace<T>(IReadOnlyList<T> list, int index, Func<T, T> change)
    {
        var copy = list.ToList();
        copy[index] = change(copy[index]);
        return copy;
    }

    [Fact]
    public void Duplicate_product_id_is_reported()
    {
        var data = Valid with { Products = [.. Valid.Products, Valid.Products[0]] };

        Assert.Contains(ErrorsFor(data), e => e.Contains($"duplicate id '{Valid.Products[0].Id}'"));
    }

    [Fact]
    public void Negative_price_is_reported()
    {
        var data = Valid with { Products = Replace(Valid.Products, 0, p => p with { SuggestedPriceCents = -1 }) };

        Assert.Contains(ErrorsFor(data), e => e.Contains("suggestedPriceCents must be > 0"));
    }

    [Fact]
    public void Negative_cost_is_reported()
    {
        var data = Valid with { Products = Replace(Valid.Products, 0, p => p with { CostCents = -5 }) };

        Assert.Contains(ErrorsFor(data), e => e.Contains("costCents must be >= 0"));
    }

    [Fact]
    public void Name_key_must_follow_category_id_field_format()
    {
        var data = Valid with { Products = Replace(Valid.Products, 0, p => p with { NameKey = "soy sauce" }) };

        Assert.Contains(ErrorsFor(data), e => e.Contains("nameKey must be"));
    }

    [Fact]
    public void Id_must_be_snake_case()
    {
        var data = Valid with { Fixtures = Replace(Valid.Fixtures, 0, f => f with { Id = "Big-Shelf", NameKey = "fixture.Big-Shelf.name" }) };

        Assert.Contains(ErrorsFor(data), e => e.Contains("lower_snake_case"));
    }

    [Fact]
    public void Orderable_product_needs_a_positive_cost()
    {
        var index = Valid.Products.ToList().FindIndex(p => p.Orderable);
        var data = Valid with { Products = Replace(Valid.Products, index, p => p with { CostCents = 0 }) };

        Assert.Contains(ErrorsFor(data), e => e.Contains("orderable products need costCents > 0"));
    }

    [Fact]
    public void Orderable_chilled_product_needs_cold_storage()
    {
        var data = Valid with { Fixtures = Valid.Fixtures.Where(f => f.Kind != FixtureKind.ColdStorage).ToList() };

        Assert.Contains(ErrorsFor(data), e => e.Contains("no backroom/cold storage fixture stores 'Chilled'"));
    }

    [Fact]
    public void Fixture_referencing_unknown_stage_is_reported()
    {
        var data = Valid with { Fixtures = Replace(Valid.Fixtures, 0, f => f with { UnlockStage = 99 }) };

        Assert.Contains(ErrorsFor(data), e => e.Contains("unlockStage 99 does not exist"));
    }

    [Fact]
    public void Interaction_cell_inside_footprint_is_reported()
    {
        var data = Valid with { Fixtures = Replace(Valid.Fixtures, 0, f => f with { InteractionCells = [new GridPoint(0, 0)] }) };

        Assert.Contains(ErrorsFor(data), e => e.Contains("interaction cell (0,0)"));
    }

    [Fact]
    public void Missing_required_stage_one_fixture_kind_is_reported()
    {
        var data = Valid with { Fixtures = Valid.Fixtures.Where(f => f.Kind != FixtureKind.Checkout).ToList() };

        Assert.Contains(ErrorsFor(data), e => e.Contains("'Checkout'"));
    }

    [Fact]
    public void Meat_cut_referencing_unknown_product_is_reported()
    {
        var data = Valid with
        {
            MeatCuts = Replace(Valid.MeatCuts, 0, c => c with { Cuts = [.. c.Cuts, new CutYield("pork_cheek", 10)] }),
        };

        Assert.Contains(ErrorsFor(data), e => e.Contains("'pork_cheek' does not exist"));
    }

    [Fact]
    public void Meat_cut_yields_over_whole_are_reported()
    {
        var data = Valid with { MeatCuts = Replace(Valid.MeatCuts, 0, c => c with { GroundPermille = 1000 }) };

        Assert.Contains(ErrorsFor(data), e => e.Contains("exceeds 1000"));
    }

    [Fact]
    public void Meat_cut_must_reference_meat_products()
    {
        var nonMeat = Valid.Products.First(p => p.Department != Department.Meat).Id;
        var data = Valid with { MeatCuts = Replace(Valid.MeatCuts, 0, c => c with { GroundProductId = nonMeat }) };

        Assert.Contains(ErrorsFor(data), e => e.Contains("not in the meat department"));
    }

    [Fact]
    public void Role_with_inverted_range_is_reported()
    {
        var roles = Replace(Valid.Employees.Roles, 0, r => r with { KnifeSkill = new IntRange(5, 1) });
        var data = Valid with { Employees = Valid.Employees with { Roles = roles } };

        Assert.Contains(ErrorsFor(data), e => e.Contains("knifeSkill.max must be >= knifeSkill.min"));
    }

    [Fact]
    public void Customer_price_sensitivity_out_of_range_is_reported()
    {
        var data = Valid with { CustomerTypes = Replace(Valid.CustomerTypes, 0, t => t with { PriceSensitivityPermille = 1500 }) };

        Assert.Contains(ErrorsFor(data), e => e.Contains("priceSensitivityPermille"));
    }

    [Fact]
    public void Stage_ids_must_be_sequential()
    {
        var data = Valid with { Stages = Replace(Valid.Stages, 0, s => s with { Id = 7, NameKey = "stage.7.name" }) };

        Assert.Contains(ErrorsFor(data), e => e.Contains("stage ids must be 1, 2, 3"));
    }

    [Fact]
    public void Opening_hours_must_be_ordered()
    {
        var data = Valid with { Economy = Valid.Economy with { OpenMinuteOfDay = 1300 } };

        Assert.Contains(ErrorsFor(data), e => e.Contains("openMinuteOfDay < closeMinuteOfDay"));
    }

    [Fact]
    public void Speed_multipliers_must_ascend()
    {
        var data = Valid with { Economy = Valid.Economy with { SpeedMultipliers = [1, 4, 2] } };

        Assert.Contains(ErrorsFor(data), e => e.Contains("strictly ascending"));
    }

    [Fact]
    public void Negative_starting_cash_is_reported()
    {
        var data = Valid with { Economy = Valid.Economy with { StartingCashCents = -1 } };

        Assert.Contains(ErrorsFor(data), e => e.Contains("startingCashCents"));
    }

    private static GameData WithStartingLayout(params LayoutEntry[] layout) =>
        Valid with { Economy = Valid.Economy with { StartingLayout = layout } };

    [Fact]
    public void Starting_layout_needs_an_entrance()
    {
        var data = WithStartingLayout(new LayoutEntry("shelf", new GridPoint(1, 1)));

        Assert.Contains(ErrorsFor(data), e => e.Contains("MissingEntrance"));
    }

    [Fact]
    public void Starting_layout_must_obey_placement_rules()
    {
        var data = WithStartingLayout(
            new LayoutEntry("entrance", new GridPoint(5, 7)),
            new LayoutEntry("shelf", new GridPoint(1, 1)),
            new LayoutEntry("shelf", new GridPoint(2, 1)));

        Assert.Contains(ErrorsFor(data), e => e.Contains("Overlap") && e.Contains("[2] 'shelf'"));
    }

    [Fact]
    public void Starting_layout_respects_rotation()
    {
        // Facing up from the bottom row is fine; facing down there would put its interaction cells outside.
        var up = WithStartingLayout(new LayoutEntry("entrance", new GridPoint(5, 7)), new LayoutEntry("shelf", new GridPoint(7, 7), Rotation.Cw180));
        var down = WithStartingLayout(new LayoutEntry("entrance", new GridPoint(5, 7)), new LayoutEntry("shelf", new GridPoint(7, 7)));

        Assert.Empty(ErrorsFor(up));
        Assert.Contains(ErrorsFor(down), e => e.Contains("PathBlocked"));
    }

    [Fact]
    public void Starting_layout_only_uses_stage_one_fixtures_that_exist()
    {
        var data = WithStartingLayout(
            new LayoutEntry("entrance", new GridPoint(5, 7)),
            new LayoutEntry("meat_display", new GridPoint(1, 1)),
            new LayoutEntry("fish_tank", new GridPoint(4, 1)));

        var errors = ErrorsFor(data);
        Assert.Contains(errors, e => e.Contains("'meat_display' is not available at stage 1"));
        Assert.Contains(errors, e => e.Contains("'fish_tank' does not exist"));
    }

    [Fact]
    public void Throw_if_invalid_lists_all_errors()
    {
        var data = Valid with { Economy = Valid.Economy with { StartingCashCents = -1, MinutesPerTick = 0 } };

        var ex = Assert.Throws<DataValidationException>(() => DataValidator.ThrowIfInvalid(data));
        Assert.Equal(2, ex.Errors.Count);
    }
}
