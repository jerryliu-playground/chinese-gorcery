using ChineseGrocery.Sim.Calendar;
using ChineseGrocery.Sim.Data;
using Godot;

namespace ChineseGrocery.Game.Ui;

/// <summary>What a random event says and the answers it offers, shared by the notice popup and the calendar.</summary>
public static class NoticeText
{
    public static string Date(SimHost host, int day)
    {
        var date = host.Sim.DateOf(day)!.Value;
        return UiText.F("ui.date.short", ("season", UiText.T(host.Data.Calendar!.Seasons[date.SeasonIndex].NameKey)), ("day", date.DayOfSeason));
    }

    private static string Product(SimHost host, string id) => UiText.T(host.Data.Products.Single(p => p.Id == id).NameKey);

    public static string Describe(SimHost host, StoreEvent e)
    {
        var def = host.Sim.EventDefOf(e);
        var key = "event." + def.Id + ".notice";
        return def.Kind switch
        {
            EventKind.SupplierStockout => UiText.F(key, ("product", Product(host, e.ProductId!)), ("start", Date(host, e.StartDay)), ("end", Date(host, e.EndDay))),
            EventKind.CostRise => UiText.F(key, ("department", UiText.T("ui.department." + UiText.Snake(e.Department!.Value))),
                ("percent", (def.CostPermille - 1000) / 10), ("start", Date(host, e.StartDay)), ("end", Date(host, e.EndDay))),
            EventKind.Inspection => UiText.F(key, ("time", UiText.ClockTime(def.Inspection!.ArriveMinuteOfDay))),
            EventKind.PowerCut => UiText.F(key, ("start", UiText.ClockTime(def.PowerCut!.StartMinuteOfDay)), ("end", UiText.ClockTime(def.PowerCut.EndMinuteOfDay))),
            EventKind.ClearanceOffer => UiText.F(key, ("qty", e.Goods![0].Quantity), ("product", Product(host, e.ProductId!)),
                ("date", Date(host, e.BestBeforeDay)), ("cost", UiText.Money(e.Goods[0].Quantity * e.UnitCostCents))),
            EventKind.GroupOrder => UiText.F(key,
                ("goods", string.Join(", ", e.Goods!.Select(g => UiText.F("ui.notice.goods_item", ("qty", g.Quantity), ("product", Product(host, g.ProductId)))))),
                ("time", UiText.ClockTime(def.GroupOrder!.PickupMinuteOfDay)),
                ("reward", UiText.Money(e.Goods!.Sum(g => g.Quantity * host.Data.Products.Single(p => p.Id == g.ProductId).SuggestedPriceCents) * def.GroupOrder.RewardPermille / 1000))),
            _ => UiText.T(key),
        };
    }

    /// <summary>Buttons for the answers the event still takes; <paramref name="answered"/> runs after one is sent.</summary>
    public static IEnumerable<Button> Actions(SimHost host, StoreEvent e, Action answered)
    {
        Button Make(string text, Sim.Core.SimCommand command)
        {
            var b = new Button { Text = text, AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled };
            b.Pressed += () =>
            {
                host.Send(command);
                answered();
            };
            return b;
        }

        var def = host.Sim.EventDefOf(e);
        if (e.Status == StoreEventStatus.Offered)
        {
            yield return Make(UiText.T("ui.notice.accept"), new AnswerOffer(e.Id, true));
            yield return Make(UiText.T("ui.notice.decline"), new AnswerOffer(e.Id, false));
        }
        else if (def.PowerCut is { } cut && e.Status == StoreEventStatus.Announced && !e.GeneratorRented)
        {
            yield return Make(UiText.F("ui.notice.rent_generator", ("cost", UiText.Money(cut.GeneratorCostCents))), new RentGenerator(e.Id));
        }
    }
}
