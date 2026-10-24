using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Economy;

namespace ChineseGrocery.SimRunner;

/// <summary>Sanity checks after each day: negative numbers, unbalanced cash, negative stock. Fixture spend is net of refunds, so it may be negative.</summary>
public static class AnomalyCheck
{
    public static IEnumerable<string> Check(GameSim sim, DailyReport r)
    {
        var day = $"day {r.Day}";
        if (r.CashEndCents < 0) yield return $"{day}: cash is negative ({r.CashEndCents})";

        foreach (var (name, value) in new[]
                 {
                     ("revenue", r.RevenueCents), ("purchases", r.PurchaseCostCents), ("wages", r.WagesCents),
                     ("loss", r.LossCents), ("customers", r.Customers), ("angry", r.AngryLeaves),
                     ("notFound", r.ItemsNotFound), ("tooExpensive", r.ItemsTooExpensive),
                 })
        {
            if (value < 0) yield return $"{day}: {name} is negative ({value})";
        }

        var expectedChange = r.RevenueCents - r.PurchaseCostCents - r.WagesCents - r.FixtureSpendCents;
        if (r.CashEndCents - r.CashStartCents != expectedChange)
        {
            yield return $"{day}: cash changed by {r.CashEndCents - r.CashStartCents} but ledger says {expectedChange}";
        }

        foreach (var lot in sim.Inventory.Locations.SelectMany(l => l.Lots).Where(l => l.Quantity <= 0))
        {
            yield return $"{day}: non-positive stock lot {lot}";
        }

        foreach (var (slot, productId) in sim.Shelves.Assigned)
        {
            var max = sim.Data.Products.Single(p => p.Id == productId).UnitsPerShelfSlot;
            if (sim.Inventory.Count(slot, productId) > max) yield return $"{day}: slot {slot} holds more than {max} {productId}";
        }

        if (r.Sales.Sum(s => s.RevenueCents) != r.RevenueCents) yield return $"{day}: sales lines do not add up to revenue";
        if (sim.Customers.Count > 0) yield return $"{day}: {sim.Customers.Count} customers still inside after closing";
    }
}
