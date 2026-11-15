using System;
using System.Collections.Generic;
using System.Linq;
namespace Ssalddel.BatchMonitor.Windows
{

internal sealed class FoodManagementRound
{
    internal const int FastDispatchBonusPerOrder = 600;
    internal static readonly TimeSpan FastDispatchWindow = TimeSpan.FromMinutes(6);

    private readonly DateTime startedAt;
    private readonly DateTime dispatchDeadline;
    private readonly List<FoodCampaignCommand> campaignCommands = new();

    private FoodManagementRound(IReadOnlyList<FoodManagementSession> orders, FoodCampaignStage? campaignStage = null)
    {
        Orders = orders;
        CampaignStage = campaignStage;
        startedAt = Now;
        dispatchDeadline = startedAt + (campaignStage?.DispatchLimit ?? FastDispatchWindow);
    }

    internal IReadOnlyList<FoodManagementSession> Orders { get; }
    internal FoodCampaignStage? CampaignStage { get; }
    internal int BatchCount => Orders.Count;
    internal bool IsSettled => Orders.All(order => order.Settlement is not null);
    internal DateTime Now => Orders[0].Now;
    internal TimeSpan DispatchRemaining => Now < dispatchDeadline ? dispatchDeadline - Now : TimeSpan.Zero;
    internal TimeSpan BonusRemaining => DispatchRemaining;
    internal int OnTimeAssignmentCount => Orders.Count(AssignedOnTime);
    internal IReadOnlyList<FoodManagementSession> LateOrders => Array.AsReadOnly(Orders.Where(order =>
        order.AssignedAt is { } assignedAt && assignedAt > dispatchDeadline).ToArray());
    internal bool CampaignCleared => CampaignStage is not null && IsSettled && OnTimeAssignmentCount == BatchCount;
    internal int FastDispatchBonus => BatchCount == 1 ? 0 : FastDispatchBonusPerOrder * Orders.Count(order =>
        order.Settlement is not null && AssignedOnTime(order));

    internal FoodCampaignResult? CampaignResult
    {
        get
        {
            if (CampaignStage is not { } stage || !IsSettled)
            {
                return null;
            }

            var results = Orders.Select(order => new FoodCampaignOrderResult(order.OrderId, order.PickupStoreId,
                order.TerritoryId, AssignedOnTime(order), order.AssignedAt!.Value, order.Settlement!)).ToArray();
            var bonus = FastDispatchBonus;
            return new(stage.Id, CampaignCleared, BatchCount, OnTimeAssignmentCount,
                results.Sum(order => order.Settlement.PlatformRevenue), results.Sum(order => order.Settlement.DriverPay),
                results.Sum(order => order.Settlement.PlatformBalance) + bonus, bonus, Array.AsReadOnly(results));
        }
    }

    internal FoodCampaignReplay? CampaignReplay() => CampaignStage is { } stage
        ? new(stage.Id, Now - startedAt, Array.AsReadOnly(campaignCommands.ToArray()),
            Array.AsReadOnly(Orders.Select(order => new FoodCampaignOrderReplay(order.OrderId, order.Stage,
                Array.AsReadOnly(order.Events.ToArray()))).ToArray()), CampaignResult)
        : null;

    private bool AssignedOnTime(FoodManagementSession order) =>
        order.AssignedAt is { } assignedAt && assignedAt <= dispatchDeadline;

    internal static FoodManagementRound? Create(DeliveryRoadNetwork roads, int batchCount)
    {
        if (batchCount < 1 || batchCount > 2) throw new ArgumentOutOfRangeException(nameof(batchCount));

        var orders = new List<FoodManagementSession>(batchCount);
        // A rider keeps the assigned route and final position throughout this round.
        bool IsDriverAvailable(string driverId) => orders.All(order => order.AssignedDriverId != driverId);

        var first = FoodManagementSession.Create(roads, IsDriverAvailable);
        if (first is null)
        {
            return null;
        }

        orders.Add(first);
        if (batchCount == 2)
        {
            var second = FoodManagementSession.CreateSecond(roads, IsDriverAvailable);
            if (second is null)
            {
                return null;
            }

            orders.Add(second);
        }

        return new(orders.AsReadOnly());
    }

    internal static FoodManagementRound? CreateCampaign(DeliveryRoadNetwork roads, DeliverySample sample, FoodCampaignStage stage)
    {
        var stores = sample.Stores.ToLookup(store => store.Id, StringComparer.Ordinal);
        var menus = sample.Orders.ToLookup(order => order.StoreId, order => order.Menu, StringComparer.Ordinal);
        var districtStores = new List<(FoodCampaignDistrict District, (DeliveryStore Store, int Node) A,
            (DeliveryStore Store, int Node) B)>(stage.Districts.Count);
        var unavailableDestinations = new HashSet<int>();
        foreach (var district in stage.Districts)
        {
            var storesA = stores[district.Key + "-a"].ToArray();
            var storesB = stores[district.Key + "-b"].ToArray();
            if (storesA.Length != 1 || storesB.Length != 1 ||
                storesA[0].District != district.District || storesB[0].District != district.District)
            {
                return null;
            }

            var storeA = storesA[0];
            var storeB = storesB[0];
            var a = roads.Closest(new(storeA.Latitude, storeA.Longitude));
            var b = roads.Closest(new(storeB.Latitude, storeB.Longitude));
            if (!unavailableDestinations.Add(a) || !unavailableDestinations.Add(b) || roads.FindPath(a, b) is null)
            {
                return null;
            }

            districtStores.Add((district, (storeA, a), (storeB, b)));
        }

        var orders = new List<FoodManagementSession>(stage.OrderCount);
        bool IsDriverAvailable(string driverId) => orders.All(order => order.AssignedDriverId != driverId);

        for (var i = 0; i < districtStores.Count; i++)
        {
            var (district, a, b) = districtStores[i];
            var storeCount = i == districtStores.Count - 1 ? 2 : 1;
            for (var storeIndex = 0; storeIndex < storeCount; storeIndex++)
            {
                var useStoreB = storeIndex == 1;
                var store = useStoreB ? b.Store : a.Store;
                var menu = menus[store.Id].FirstOrDefault(menu => !string.IsNullOrWhiteSpace(menu));
                if (menu is null || FoodManagementSession.CreateCampaign(roads, stage, district, orders.Count + 1,
                        a, b, useStoreB, menu, unavailableDestinations, IsDriverAvailable) is not { } order)
                {
                    return null;
                }

                orders.Add(order);
                unavailableDestinations.Add(order.DropoffNode);
            }
        }

        return new(orders.AsReadOnly(), stage);
    }

    internal void Tick(TimeSpan elapsed)
    {
        foreach (var order in Orders)
        {
            order.Tick(elapsed);
        }
    }

    internal void EnableManualPickup()
    {
        foreach (var order in Orders) order.EnableManualPickup();
    }

    internal FoodManagementAssignment ConfirmPickup(string commandId, string orderId, string driverId, long expectedRevision)
    {
        var order = Orders.FirstOrDefault(candidate => candidate.OrderId == orderId);
        if (order is null) return new(false, "현재 회차에 이 주문이 없습니다.");
        if (Orders.Any(candidate => candidate != order &&
            (candidate.AssignmentCommandId == commandId || candidate.PickupCommandId == commandId)))
            return new(false, "같은 요청 번호가 다른 주문에 사용되었습니다.");
        return order.ConfirmPickup(commandId, orderId, driverId, expectedRevision);
    }

    internal FoodManagementAssignment Assign(string commandId, string orderId, string driverId, long expectedRevision)
    {
        var order = Orders.FirstOrDefault(candidate => candidate.OrderId == orderId);
        if (order is null)
        {
            return new(false, "현재 배치에서 가상 주문을 찾을 수 없습니다.");
        }

        if (Orders.Any(candidate => candidate != order &&
            (candidate.AssignmentCommandId == commandId || candidate.PickupCommandId == commandId)))
        {
            return new(false, "같은 요청 번호가 다른 주문에 사용되었습니다.");
        }

        var alreadyAssigned = order.AssignedDriverId is not null;
        var result = order.Assign(commandId, orderId, driverId, expectedRevision);
        if (CampaignStage is not null && !alreadyAssigned && result.Accepted)
        {
            campaignCommands.Add(new(commandId, orderId, driverId, expectedRevision, Now - startedAt));
        }

        return result;
    }

    internal DeliveryMapSnapshot MapSnapshot()
    {
        if (BatchCount == 1)
        {
            return Orders[0].MapSnapshot();
        }

        var snapshots = Orders.Select(order => order.MapSnapshot()).ToArray();
        var assignedCouriers = new Dictionary<string, DeliveryMapCourier>(StringComparer.Ordinal);
        for (var i = 0; i < Orders.Count; i++)
        {
            if (Orders[i].AssignedDriverId is { } id)
            {
                assignedCouriers.Add(id, snapshots[i].Couriers.Single(courier => courier.Id == id));
            }
        }

        return snapshots[0] with
        {
            Orders = snapshots.SelectMany(snapshot => snapshot.Orders).ToArray(),
            Restaurants = snapshots.SelectMany(snapshot => snapshot.Restaurants).GroupBy(store => store.Id).Select(group => group.First()).ToArray(),
            Couriers = snapshots.SelectMany(snapshot => snapshot.Couriers).GroupBy(courier => courier.Id).Select(group => group.First())
                .Select(courier => assignedCouriers.GetValueOrDefault(courier.Id, courier)).ToArray(),
            Coverage = snapshots.SelectMany(snapshot => snapshot.Coverage).Distinct().ToArray(),
        };
    }
}

}
