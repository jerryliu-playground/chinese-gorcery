using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Reputation;

namespace ChineseGrocery.Sim.Core;

/// <summary>
/// Restaurant group orders (spec 16.5): a paying restaurant buyer may propose one; the player accepts or declines.
/// Accepted orders take matching goods out of storage as they come in (so they are never shelved or sold); at a
/// closing with everything set aside the order is delivered and paid at the wholesale price (reputation up); at the
/// closing of the due day with something missing it is cancelled and the goods go back (reputation down).
/// </summary>
public sealed partial class GameSim
{
    private readonly List<GroupOrder> _groupOrders = [];
    private int _nextGroupOrderId = 1;

    /// <summary>Offered and accepted group orders.</summary>
    public IReadOnlyList<GroupOrder> GroupOrders => _groupOrders;

    private GroupOrderSettingsDef GroupOrderSettings => Data.Reputation!.GroupOrders;

    /// <summary>After a buyer pays: an offer, if none is waiting, there is room for another order and the roll comes up.</summary>
    private void MaybeOfferGroupOrder(CustomerTypeDef buyer)
    {
        var settings = GroupOrderSettings;
        if (_groupOrders.Any(o => o.Status == GroupOrderStatus.Offered)) return;
        if (_groupOrders.Count(o => o.Status == GroupOrderStatus.Active) >= settings.MaxActive) return;
        if (_reputationRng.NextInt(0, 1000) >= settings.OfferPermille) return;

        var pool = settings.Candidates.Where(c => StorageRouting.FindDestination(Data, Layout.Fixtures, _products[c.ProductId]) is not null).ToList();
        if (pool.Count == 0) return;

        var lines = new List<GroupOrderLine>();
        for (var n = Math.Min(pool.Count, _reputationRng.NextInt(settings.Lines.Min, settings.Lines.Max + 1)); n > 0; n--)
        {
            var pick = pool[_reputationRng.NextInt(0, pool.Count)];
            pool.Remove(pick);
            var units = _reputationRng.NextInt(pick.Units.Min, pick.Units.Max + 1);
            lines.Add(new GroupOrderLine(pick.ProductId, units, _products[pick.ProductId].SuggestedPriceCents * settings.WholesalePermille / 1000));
        }

        var days = _reputationRng.NextInt(settings.DeadlineDays.Min, settings.DeadlineDays.Max + 1);
        var order = new GroupOrder(_nextGroupOrderId++, buyer.Id, lines, Clock.Day, days, GroupOrderStatus.Offered, null, []);
        _groupOrders.Add(order);
        _events.Add(new GroupOrderOffered(order.Id));
    }

    private Rejection? HandleAcceptGroupOrder(AcceptGroupOrder accept)
    {
        var i = _groupOrders.FindIndex(o => o.Id == accept.OrderId && o.Status == GroupOrderStatus.Offered);
        if (i < 0) return Reject(RejectReason.UnknownGroupOrder);
        if (_groupOrders.Count(o => o.Status == GroupOrderStatus.Active) >= GroupOrderSettings.MaxActive) return Reject(RejectReason.GroupOrderLimit);

        var due = Clock.Day + _groupOrders[i].DeadlineDays;
        _groupOrders[i] = _groupOrders[i] with { Status = GroupOrderStatus.Active, DueDay = due };
        _events.Add(new GroupOrderAccepted(accept.OrderId, due));
        ReserveForGroupOrders();
        return null;
    }

    private Rejection? HandleDeclineGroupOrder(DeclineGroupOrder decline)
    {
        if (_groupOrders.RemoveAll(o => o.Id == decline.OrderId && o.Status == GroupOrderStatus.Offered) == 0) return Reject(RejectReason.UnknownGroupOrder);

        _events.Add(new GroupOrderClosed(decline.OrderId, GroupOrderOutcome.Declined, 0));
        return null;
    }

    /// <summary>Offers not answered by the next morning lapse.</summary>
    private void ExpireGroupOrderOffers()
    {
        foreach (var order in _groupOrders.Where(o => o.Status == GroupOrderStatus.Offered && o.OfferedDay < Clock.Day).ToList())
        {
            _groupOrders.Remove(order);
            _events.Add(new GroupOrderClosed(order.Id, GroupOrderOutcome.Expired, 0));
        }
    }

    /// <summary>Accepted orders take what they still miss out of storage (backroom and cold storage), oldest stock first.</summary>
    private void ReserveForGroupOrders()
    {
        for (var i = 0; i < _groupOrders.Count; i++)
        {
            var order = _groupOrders[i];
            if (order.Status != GroupOrderStatus.Active || order.IsComplete) continue;

            var reserved = order.Reserved.ToList();
            foreach (var line in order.Lines)
            {
                var missing = line.Units - reserved.Where(l => l.ProductId == line.ProductId).Sum(l => l.Quantity);
                foreach (var pile in Inventory.Locations.Where(p => IsStorageAreaInstance(p.Location.FixtureInstanceId) && p.Count(line.ProductId) > 0).ToList())
                {
                    if (missing <= 0) break;

                    var n = Math.Min(missing, pile.Count(line.ProductId));
                    reserved.AddRange(pile.Take(line.ProductId, n));
                    missing -= n;
                }
            }

            _groupOrders[i] = order with { Reserved = reserved };
        }
    }

    private bool IsStorageAreaInstance(int instanceId) =>
        Layout.Find(instanceId) is { } f && DataValidator.IsStorageArea(_fixtures[f.FixtureId].Kind);

    /// <summary>
    /// At closing: complete orders are delivered and paid; orders due today and still short are cancelled; set-aside
    /// goods at the end of their life are thrown out (and must be set aside again).
    /// </summary>
    private void CloseGroupOrders()
    {
        ReserveForGroupOrders();
        var settings = GroupOrderSettings;
        foreach (var order in _groupOrders.Where(o => o.Status == GroupOrderStatus.Active).ToList())
        {
            if (order.IsComplete)
            {
                _groupOrders.Remove(order);
                foreach (var line in order.Lines)
                {
                    var cost = order.Reserved.Where(l => l.ProductId == line.ProductId).Sum(l => l.Quantity * l.UnitCostCents);
                    Economy.Earn(line.ProductId, line.Units, checked(line.Units * line.UnitPriceCents), cost);
                }

                _events.Add(new GroupOrderClosed(order.Id, GroupOrderOutcome.Delivered, order.PaymentCents));
                NoteFact(ReviewFact.GroupOrderDelivered, null, order.CustomerTypeId);
                NudgeReputation(settings.DeliveredReputation, "reputation.reason.group_order_delivered");
            }
            else if (Clock.Day >= order.DueDay)
            {
                _groupOrders.Remove(order);
                foreach (var lot in order.Reserved) PutBackInStorage(lot);
                _events.Add(new GroupOrderClosed(order.Id, GroupOrderOutcome.Failed, 0));
                NoteFact(ReviewFact.GroupOrderFailed, null, order.CustomerTypeId);
                NudgeReputation(settings.FailedReputation, "reputation.reason.group_order_failed");
            }
        }

        for (var i = 0; i < _groupOrders.Count; i++)
        {
            var order = _groupOrders[i];
            var expired = order.Reserved.Where(l => l.BestBeforeDay <= Clock.Day).ToList();
            if (expired.Count == 0) continue;

            foreach (var lot in expired)
            {
                var loss = checked(lot.Quantity * lot.UnitCostCents);
                Economy.RecordLoss(loss, _products[lot.ProductId].Department);
                _events.Add(new ProductExpired(Clock.Day, lot.ProductId, lot.Quantity, null, loss));
            }

            _groupOrders[i] = order with { Reserved = order.Reserved.Where(l => l.BestBeforeDay > Clock.Day).ToList() };
        }
    }

    /// <summary>Goods from a cancelled order go back to storage (or are lost if there is none for them).</summary>
    private void PutBackInStorage(StockLot lot)
    {
        var product = _products[lot.ProductId];
        if (StorageRouting.FindDestination(Data, Layout.Fixtures, product) is { } storage)
        {
            Inventory.GetOrCreate(StockLocation.Storage(storage.InstanceId)).Add(lot);
            return;
        }

        var loss = checked(lot.Quantity * lot.UnitCostCents);
        Economy.RecordLoss(loss, product.Department);
        _events.Add(new ProductExpired(Clock.Day, lot.ProductId, lot.Quantity, null, loss));
    }
}
