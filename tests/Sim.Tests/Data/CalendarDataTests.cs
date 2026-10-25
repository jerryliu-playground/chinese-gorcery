using ChineseGrocery.Sim.Data;

namespace ChineseGrocery.Sim.Tests.Data;

/// <summary>Validation of calendar.json, festivals.json and events.json.</summary>
public class CalendarDataTests
{
    private static readonly GameData Valid = SimTestKit.RealData;

    private static IReadOnlyList<string> Errors(GameData data) => DataValidator.Validate(data);

    [Fact]
    public void Real_calendar_data_is_valid_and_marked_tbd()
    {
        Assert.Empty(Errors(Valid));
        Assert.True(Valid.Calendar!.Tbd && Valid.Festivals!.Tbd && Valid.Events!.Tbd);
    }

    [Fact]
    public void A_festival_on_a_day_its_season_does_not_have_is_rejected()
    {
        var festivals = Valid.Festivals! with { Festivals = Valid.Festivals.Festivals.Select((f, i) => i == 0 ? f with { Day = 29 } : f).ToList() };

        Assert.Contains(Errors(Valid with { Festivals = festivals }), e => e.Contains("day must be within its season"));
    }

    [Fact]
    public void Festival_goods_must_exist_and_be_orderable()
    {
        var festivals = Valid.Festivals! with { Festivals = Valid.Festivals.Festivals.Select((f, i) => i == 0 ? f with { Goods = ["pork_belly", "unicorn"] } : f).ToList() };

        var errors = Errors(Valid with { Festivals = festivals });

        Assert.Contains(errors, e => e.Contains("'pork_belly' must be orderable"));
        Assert.Contains(errors, e => e.Contains("unknown good 'unicorn'"));
    }

    [Fact]
    public void An_event_kind_needs_its_settings_block()
    {
        var events = Valid.Events! with { Events = Valid.Events.Events.Select(e => e.Kind == EventKind.PowerCut ? e with { PowerCut = null } : e).ToList() };

        Assert.Contains(Errors(Valid with { Events = events }), e => e.Contains("needs a 'powerCut' block"));
    }

    [Fact]
    public void A_demand_factor_names_exactly_one_kind_of_goods()
    {
        var calendar = Valid.Calendar! with { SeasonalDemand = [new SeasonalDemandDef("summer", 1200, ProductId: "soy_milk", Department: Department.Drinks), new SeasonalDemandDef("monsoon", 1200, ProductId: "soy_milk")] };

        var errors = Errors(Valid with { Calendar = calendar });

        Assert.Contains(errors, e => e.Contains("set exactly one of"));
        Assert.Contains(errors, e => e.Contains("unknown season 'monsoon'"));
    }
}
