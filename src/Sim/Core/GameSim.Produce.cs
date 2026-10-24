using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Produce;
using ChineseGrocery.Sim.Tasks;

namespace ChineseGrocery.Sim.Core;

/// <summary>
/// Produce stands: tidiness (shoppers mess it up, a Tidy task puts it right) and the display score that scales how
/// likely shoppers are to buy from the stand.
/// </summary>
public sealed partial class GameSim
{
    private const int PerfectlyTidy = 100;

    /// <summary>Tidiness of stands that have been touched; a stand missing here is perfectly tidy.</summary>
    private readonly SortedDictionary<int, int> _tidiness = new();

    /// <summary>Tools and tests only: use this display score on every stand instead of the real one.</summary>
    public int? DisplayScoreOverride { get; set; }

    public int TidinessOf(int standInstanceId) => _tidiness.GetValueOrDefault(standInstanceId, PerfectlyTidy);

    private bool IsProduceStand(int instanceId) =>
        Layout.Find(instanceId) is { } f && _fixtures[f.FixtureId].Kind == FixtureKind.ProduceStand;

    private DisplaySettingsDef DisplaySettings =>
        Data.Economy.Display ?? throw new InvalidOperationException("Produce stands need economy.display settings.");

    public DisplayScore DisplayScoreOf(int standInstanceId)
    {
        var stand = Layout.Find(standInstanceId) ?? throw new ArgumentOutOfRangeException(nameof(standInstanceId));
        var def = _fixtures[stand.FixtureId];
        if (def.Kind != FixtureKind.ProduceStand) throw new ArgumentOutOfRangeException(nameof(standInstanceId), "Not a produce stand.");

        var fullness = Fullness(standInstanceId, def.Capacity);
        var freshness = FreshnessScore(standInstanceId, def.Capacity);
        var tidiness = TidinessOf(standInstanceId);
        var pairing = Pairing(stand);
        return new DisplayScore(fullness, freshness, tidiness, pairing, Display.Total(DisplaySettings, fullness, freshness, tidiness, pairing));
    }

    /// <summary>Average fill of the stand's slots; an unassigned slot counts as empty.</summary>
    private int Fullness(int standInstanceId, int capacity)
    {
        var total = 0;
        for (var slot = 0; slot < capacity; slot++)
        {
            var location = new StockLocation(standInstanceId, slot);
            if (Shelves.ProductAt(location) is not { } productId) continue;

            var full = _products[productId].UnitsPerShelfSlot;
            total += Math.Min(Inventory.Count(location, productId), full) * 100 / full;
        }

        return total / capacity;
    }

    /// <summary>Unit-weighted average of the freshness tier scores of everything on the stand; 0 when it is empty.</summary>
    private int FreshnessScore(int standInstanceId, int capacity)
    {
        var scores = Data.Economy.Freshness!.Scores;
        long units = 0, points = 0;
        foreach (var lot in Lots(standInstanceId, capacity))
        {
            var tier = Freshness.TierOf(_products[lot.ProductId], lot, Clock.Day);
            units += lot.Quantity;
            points += (long)lot.Quantity * (tier == FreshnessTier.Spoiled ? 0 : scores.Of(tier));
        }

        return units == 0 ? 0 : (int)(points / units);
    }

    private IEnumerable<StockLot> Lots(int standInstanceId, int capacity) =>
        Enumerable.Range(0, capacity)
            .Select(slot => Inventory.TryGet(new StockLocation(standInstanceId, slot)))
            .Where(pile => pile is not null)
            .SelectMany(pile => pile!.Lots);

    /// <summary>
    /// Share of the touching produce stands whose main colour differs from this one's, as 0..100. With no touching
    /// stand (or no colour to compare) the neutral score from the data applies.
    /// </summary>
    private int Pairing(PlacedFixture stand)
    {
        if (MainColour(stand) is not { } colour) return DisplaySettings.NoNeighborPairing;

        var cells = LayoutRules.Footprint(_fixtures[stand.FixtureId], stand.Origin, stand.Rotation).ToList();
        var neighbourColours = Layout.Fixtures
            .Where(f => f.InstanceId != stand.InstanceId && _fixtures[f.FixtureId].Kind == FixtureKind.ProduceStand)
            .Where(f => LayoutRules.Footprint(_fixtures[f.FixtureId], f.Origin, f.Rotation).Any(c => cells.Any(own => Touches(own, c))))
            .Select(MainColour)
            .OfType<ProduceColor>()
            .ToList();
        if (neighbourColours.Count == 0) return DisplaySettings.NoNeighborPairing;

        return neighbourColours.Count(c => c != colour) * 100 / neighbourColours.Count;
    }

    private static bool Touches(GridPoint a, GridPoint b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) == 1;

    /// <summary>The colour with the most units on the stand; ties and empty stands go by the assigned slots in order.</summary>
    private ProduceColor? MainColour(PlacedFixture stand)
    {
        var capacity = _fixtures[stand.FixtureId].Capacity;
        var assigned = Enumerable.Range(0, capacity)
            .Select(slot => Shelves.ProductAt(new StockLocation(stand.InstanceId, slot)))
            .OfType<string>()
            .Select(id => _products[id].Color)
            .OfType<ProduceColor>()
            .Distinct()
            .ToList();
        if (assigned.Count == 0) return null;

        var units = Lots(stand.InstanceId, capacity)
            .Where(l => _products[l.ProductId].Color is not null)
            .GroupBy(l => _products[l.ProductId].Color!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
        return assigned.OrderByDescending(c => units.GetValueOrDefault(c)).First();
    }

    /// <summary>Scales a buy chance by the display score of the stand the item is on (other fixtures are unaffected).</summary>
    private int ApplyDisplay(int chancePermille, StockLocation slot)
    {
        if (!IsProduceStand(slot.FixtureInstanceId)) return chancePermille;

        var score = DisplayScoreOverride ?? DisplayScoreOf(slot.FixtureInstanceId).Total;
        return (int)Math.Min(1000L, (long)chancePermille * Display.BuyFactorPermille(DisplaySettings, score) / 1000);
    }

    /// <summary>A shopper rummaged through the stand.</summary>
    private void MessUp(StockLocation slot)
    {
        if (!IsProduceStand(slot.FixtureInstanceId)) return;

        _tidiness[slot.FixtureInstanceId] = Math.Max(0, TidinessOf(slot.FixtureInstanceId) - DisplaySettings.MessPerItemTaken);
    }

    /// <summary>One Tidy task per stand below the threshold, gone once it has been tidied.</summary>
    private void RefreshTidyTasks()
    {
        Tasks.RemoveOpen(t => t is TidyTask tidy && !NeedsTidying(tidy.StandInstanceId));

        foreach (var stand in Layout.Fixtures.Where(f => _fixtures[f.FixtureId].Kind == FixtureKind.ProduceStand))
        {
            if (!NeedsTidying(stand.InstanceId)) continue;
            if (Tasks.Tasks.Any(t => t is TidyTask tidy && tidy.StandInstanceId == stand.InstanceId)) continue;

            Tasks.Add(id => new TidyTask(id, Data.Economy.Tasks.PriorityOf(TaskType.Tidy), stand.InstanceId));
        }
    }

    private bool NeedsTidying(int standInstanceId) =>
        IsProduceStand(standInstanceId) && TidinessOf(standInstanceId) < DisplaySettings.TidyBelow;

    private void Tidy(Worker worker, TidyTask task)
    {
        _tidiness.Remove(task.StandInstanceId);
        _events.Add(new StandTidied(task.StandInstanceId, worker.Id));
    }
}
