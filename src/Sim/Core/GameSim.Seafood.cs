using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Save;
using ChineseGrocery.Sim.Seafood;
using ChineseGrocery.Sim.Tasks;

namespace ChineseGrocery.Sim.Core;

/// <summary>
/// Fish tanks: water that fouls while the store is open (faster with more animals and with dead ones left in), deaths
/// at closing, cleaning, and taking the dead out, which turns fish into chilled goods and throws the rest away.
/// Live seafood still in storage after its first business day dies too.
/// </summary>
public sealed partial class GameSim
{
    private const long MicroPerPoint = 1_000_000;
    private const long CleanWater = 100 * MicroPerPoint;

    /// <summary>Water of tanks that are not perfectly clean, in millionths of a quality point.</summary>
    private readonly SortedDictionary<int, long> _water = new();

    /// <summary>Dead animals still in each tank.</summary>
    private readonly SortedDictionary<int, List<StockLot>> _dead = new();

    /// <summary>Tools and tests only: never put up CleanTank tasks (to compare against keeping tanks clean).</summary>
    public bool TankCleaningDisabled { get; set; }

    /// <summary>Water quality 0-100.</summary>
    public int WaterQualityOf(int tankInstanceId) => (int)(WaterMicro(tankInstanceId) / MicroPerPoint);

    public IReadOnlyList<StockLot> DeadIn(int tankInstanceId) => _dead.TryGetValue(tankInstanceId, out var dead) ? dead : [];

    private long WaterMicro(int tankInstanceId) => _water.GetValueOrDefault(tankInstanceId, CleanWater);

    private SeafoodSettingsDef SeafoodSettings =>
        Data.Economy.Seafood ?? throw new InvalidOperationException("Fish tanks need economy.seafood settings.");

    private IEnumerable<int> Tanks() =>
        Layout.Fixtures.Where(f => _fixtures[f.FixtureId].Kind == FixtureKind.FishTank).Select(f => f.InstanceId);

    private IEnumerable<Stockpile> TankSlots(int tankInstanceId) =>
        Inventory.Locations.Where(p => p.Location.FixtureInstanceId == tankInstanceId);

    internal void SetWaterQuality(int tankInstanceId, int quality)
    {
        if (quality >= 100) _water.Remove(tankInstanceId);
        else _water[tankInstanceId] = quality * MicroPerPoint;
    }

    internal void AddDead(int tankInstanceId, StockLot lot)
    {
        if (!_dead.TryGetValue(tankInstanceId, out var dead)) _dead[tankInstanceId] = dead = [];

        var same = dead.FindIndex(l => l.ProductId == lot.ProductId && l.UnitCostCents == lot.UnitCostCents);
        if (same >= 0) dead[same] = dead[same] with { Quantity = dead[same].Quantity + lot.Quantity };
        else dead.Add(lot with { BestBeforeDay = 0 });
    }

    /// <summary>One open tick of fouling: a base rate plus a share for every live and every dead animal in the tank.</summary>
    private void FoulTankWater()
    {
        if (Data.Economy.Seafood is not { } s) return;

        foreach (var tank in Tanks())
        {
            var live = TankSlots(tank).Sum(p => p.Lots.Sum(l => l.Quantity));
            var dead = DeadIn(tank).Sum(l => l.Quantity);
            var perHourMilli = s.WaterDropBasePerHourMilli + (long)s.WaterDropPerUnitPerHourMilli * live + (long)s.WaterDropPerDeadPerHourMilli * dead;
            var drop = perHourMilli * (MicroPerPoint / 1000) * Data.Economy.SecondsPerTick / 3600;
            _water[tank] = Math.Max(0, WaterMicro(tank) - drop);
        }
    }

    /// <summary>At closing, every live animal in a tank may die, more likely the dirtier the water. The dead stay in.</summary>
    private void KillTankAnimals()
    {
        foreach (var tank in Tanks().ToList())
        {
            var quality = WaterQualityOf(tank);
            foreach (var pile in TankSlots(tank).ToList())
            {
                foreach (var lot in pile.Lots.Where(l => _products[l.ProductId].Live is not null).ToList())
                {
                    var chance = Mortality.ChancePermille(SeafoodSettings, _products[lot.ProductId], quality);
                    var deaths = 0;
                    for (var i = 0; i < lot.Quantity; i++)
                    {
                        if (Rng.NextInt(0, 1000) < chance) deaths++;
                    }

                    if (deaths == 0) continue;

                    AddDead(tank, pile.TakeFrom(lot, deaths));
                    Economy.Today.CountSeafoodDeaths(deaths);
                    _events.Add(new SeafoodDied(pile.Location, lot.ProductId, deaths, 0));
                }
            }
        }
    }

    /// <summary>Live seafood still in storage at the close of any day after the one it arrived on dies and is thrown away.</summary>
    private void KillStrandedLiveSeafood()
    {
        foreach (var pile in Inventory.Locations.Where(p => IsStorageArea(p.Location.FixtureInstanceId)).ToList())
        {
            foreach (var lot in pile.Lots.Where(IsStrandedAlive).ToList())
            {
                pile.TakeFrom(lot, lot.Quantity);
                var loss = checked(lot.Quantity * lot.UnitCostCents);
                Economy.RecordLoss(loss, Department.Seafood);
                Economy.Today.CountSeafoodDeaths(lot.Quantity);
                _events.Add(new SeafoodDied(pile.Location, lot.ProductId, lot.Quantity, loss));
            }
        }
    }

    private bool IsStrandedAlive(StockLot lot)
    {
        var product = _products[lot.ProductId];
        var arrivedDay = lot.BestBeforeDay - product.ShelfLifeDays + 1;
        return product.Live is not null && arrivedDay < Clock.Day;
    }

    /// <summary>A CleanTank task per tank below the threshold, and a RemoveDead task per tank with dead animals in it.</summary>
    private void RefreshTankTasks()
    {
        Tasks.RemoveOpen(t => t is CleanTankTask c && !NeedsCleaning(c.TankInstanceId));
        Tasks.RemoveOpen(t => t is RemoveDeadTask r && DeadIn(r.TankInstanceId).Count == 0);

        foreach (var tank in Tanks())
        {
            if (NeedsCleaning(tank) && !Tasks.Tasks.Any(t => t is CleanTankTask c && c.TankInstanceId == tank))
            {
                Tasks.Add(id => new CleanTankTask(id, Data.Economy.Tasks.PriorityOf(TaskType.CleanTank), tank));
            }

            if (DeadIn(tank).Count > 0 && !Tasks.Tasks.Any(t => t is RemoveDeadTask r && r.TankInstanceId == tank))
            {
                Tasks.Add(id => new RemoveDeadTask(id, Data.Economy.Tasks.PriorityOf(TaskType.RemoveDead), tank));
            }
        }
    }

    private bool NeedsCleaning(int tankInstanceId) =>
        !TankCleaningDisabled && Layout.Find(tankInstanceId) is not null && WaterMicro(tankInstanceId) < SeafoodSettings.CleanBelow * MicroPerPoint;

    private void CleanTank(Worker worker, int tankInstanceId)
    {
        _water.Remove(tankInstanceId);
        _events.Add(new TankCleaned(tankInstanceId, worker.Id));
    }

    /// <summary>
    /// Dead animals that have a chilled form become fresh chilled goods (same cost), going onto a display slot for them
    /// with room, else into storage; anything else, or with nowhere to go, is thrown away.
    /// </summary>
    private void TakeOutDead(Worker worker, int tankInstanceId)
    {
        if (!_dead.Remove(tankInstanceId, out var dead)) return;

        var kept = new List<StockLot>();
        long lossCents = 0;
        foreach (var body in dead)
        {
            var (lot, loss) = KeepAsChilled(body);
            lossCents += loss;
            if (lot is not null) kept.Add(lot);
        }

        _events.Add(new DeadRemoved(tankInstanceId, worker.Id, kept, lossCents));
    }

    /// <summary>
    /// A dead or killed animal goes on as its chilled form, fresh today at the same cost (display slot first, then
    /// storage); without a chilled form or anywhere to put it, it is written off. Returns what was kept and the loss.
    /// </summary>
    private (StockLot? Kept, long LossCents) KeepAsChilled(StockLot body)
    {
        long lossCents;
        StockLot? kept = null;
        if (_products[body.ProductId].Live?.DiesAs is not { } chilledId)
        {
            lossCents = checked(body.Quantity * body.UnitCostCents);
        }
        else
        {
            var chilled = _products[chilledId];
            var lot = new StockLot(chilledId, body.Quantity, Clock.Day + chilled.ShelfLifeDays - 1, body.UnitCostCents);
            var left = PutOnDisplay(lot);
            if (left > 0 && StorageRouting.FindDestination(Data, Layout.Fixtures, chilled) is { } storage)
            {
                Inventory.GetOrCreate(StockLocation.Storage(storage.InstanceId)).Add(lot with { Quantity = left });
                left = 0;
            }

            lossCents = checked(left * body.UnitCostCents);
            if (body.Quantity > left) kept = lot with { Quantity = body.Quantity - left };
        }

        Economy.RecordLoss(lossCents, Department.Seafood);
        return (kept, lossCents);
    }

    /// <summary>Fills display slots assigned to the lot's product as far as they have room; returns the units left over.</summary>
    private int PutOnDisplay(StockLot lot)
    {
        var left = lot.Quantity;
        var full = _products[lot.ProductId].UnitsPerShelfSlot;
        foreach (var (slot, _) in Shelves.Assigned.Where(a => a.Value == lot.ProductId && IsDisplay(a.Key.FixtureInstanceId)).OrderBy(a => a.Key))
        {
            var room = full - Inventory.Count(slot, lot.ProductId);
            if (room <= 0) continue;

            var n = Math.Min(room, left);
            Inventory.GetOrCreate(slot).Add(lot with { Quantity = n });
            left -= n;
            if (left == 0) break;
        }

        return left;
    }

    private List<TankSave> TankSaves() =>
        _water.Keys.Union(_dead.Keys).Order()
            .Select(id => new TankSave(id, WaterMicro(id), DeadIn(id).ToList()))
            .ToList();

    private void RestoreTanks(IEnumerable<TankSave> tanks)
    {
        foreach (var t in tanks)
        {
            if (t.WaterMicro < CleanWater) _water[t.TankInstanceId] = t.WaterMicro;
            if (t.Dead.Count > 0) _dead[t.TankInstanceId] = t.Dead.ToList();
        }
    }
}
