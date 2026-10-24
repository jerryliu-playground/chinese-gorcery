using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Core;

/// <summary>Placing, moving and selling fixtures.</summary>
public sealed partial class GameSim
{
    private Rejection? HandlePlaceFixture(PlaceFixture place)
    {
        if (Clock.Phase != DayPhase.PreOpen) return Reject(RejectReason.WrongPhase);
        if (!_fixtures.TryGetValue(place.FixtureId, out var def)) return Reject(RejectReason.UnknownFixture);
        if (def.UnlockStage > Economy.Stage) return Reject(RejectReason.FixtureLocked);

        var candidate = new PlacedFixture(Layout.NextInstanceId, def.Id, place.Origin, def.PriceCents, place.Rotation);
        if (CheckLayout([.. Layout.Fixtures, candidate], candidate.InstanceId) is { } problem) return problem;
        if (!Economy.CanAfford(def.PriceCents)) return Reject(RejectReason.InsufficientCash);

        Economy.Spend(ExpenseCategory.Fixtures, def.PriceCents);
        var placed = Layout.Add(def.Id, place.Origin, place.Rotation, def.PriceCents);
        _events.Add(new FixturePlaced(placed.InstanceId, def.Id, placed.Origin, placed.Rotation, def.PriceCents));
        return null;
    }

    private Rejection? HandleMoveFixture(MoveFixture move)
    {
        if (Clock.Phase != DayPhase.PreOpen) return Reject(RejectReason.WrongPhase);
        if (Layout.Find(move.InstanceId) is not { } current) return Reject(RejectReason.UnknownFixtureInstance);

        var moved = Layout.Fixtures.Select(f => f.InstanceId == move.InstanceId ? f with { Origin = move.Origin, Rotation = move.Rotation } : f).ToList();
        if (CheckLayout(moved, move.InstanceId) is { } problem) return problem;

        Layout.Move(move.InstanceId, move.Origin, move.Rotation);
        _events.Add(new FixtureMoved(move.InstanceId, current.Origin, move.Origin, move.Rotation));
        return null;
    }

    private Rejection? HandleSellFixture(SellFixture sell)
    {
        if (Clock.Phase != DayPhase.PreOpen) return Reject(RejectReason.WrongPhase);
        if (Layout.Find(sell.InstanceId) is not { } target) return Reject(RejectReason.UnknownFixtureInstance);

        var remaining = Layout.Fixtures.Where(f => f.InstanceId != sell.InstanceId).ToList();
        if (_fixtures[target.FixtureId].Kind == FixtureKind.Entrance && !remaining.Any(f => _fixtures[f.FixtureId].Kind == FixtureKind.Entrance))
        {
            return Reject(RejectReason.LastEntrance);
        }

        if (Inventory.Holds(sell.InstanceId)) return Reject(RejectReason.FixtureNotEmpty);
        if (_orders.Pending.Any(o => StorageRouting.FindDestination(Data, remaining, _products[o.ProductId]) is null))
        {
            return Reject(RejectReason.StorageNeededForOrders);
        }

        if (CheckLayout(remaining, focusId: 0) is { } problem) return problem;

        var refund = target.PaidCents * Data.Economy.FixtureSellRefundPermille / 1000;
        Layout.Remove(sell.InstanceId);
        Inventory.Forget(sell.InstanceId);
        Shelves.Forget(sell.InstanceId);
        Economy.Refund(ExpenseCategory.Fixtures, refund);
        _events.Add(new FixtureSold(sell.InstanceId, target.FixtureId, refund));
        return null;
    }

    private Rejection? CheckLayout(IReadOnlyList<PlacedFixture> fixtures, int focusId) =>
        LayoutRules.Check(Layout.Size, fixtures, id => _fixtures[id], focusId);
}
