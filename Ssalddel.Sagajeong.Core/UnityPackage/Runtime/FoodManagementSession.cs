using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.WorkflowRules;

namespace Ssalddel.BatchMonitor.Windows
{

internal enum FoodManagementStage { Waiting, Assigned, Accepted, ToPickup, AtPickup, ToDropoff, Delivered, Received, Settled }

internal sealed record FoodManagementEvent(DateTime At, string Text, string Code = "");

internal sealed record FoodManagementCandidate(string DriverId, string Label, bool Eligible,
    double PickupKilometers, DateTime? ArrivalAt, TimeSpan? ReadyDifference, string Reason);

internal sealed record FoodManagementPreview(long Revision, string? RecommendedDriverId,
    IReadOnlyList<FoodManagementCandidate> Candidates);

internal sealed record FoodManagementAssignment(bool Accepted, string Message);

internal sealed record FoodManagementSettlement(TimeSpan DeliveryTime, int PlatformRevenue, int DriverPay,
    int PlatformBalance, string DriverId, DateTime PickupAt, DateTime DeliveredAt);

internal sealed class FoodManagementSession
{
    private const double SpeedKilometersPerHour = 18;
    private const decimal PickupRadiusKilometers = 5;
    private const int RevenueAmount = 4_800;
    private const int DriverPayAmount = 3_600;
    private static readonly DateTime Epoch = new(2026, 1, 1, 11, 30, 0);
    private static readonly TimeSpan AcceptanceDelay = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan DepartureDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PickupDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ReceiptDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan SettlementDelay = TimeSpan.FromSeconds(30);

    // Bundled SeoulEast synthetic stores. The graph provides the pickup, approach and delivery geometry.
    private static readonly DeliveryStore StoreA = new("jungnang-a", "중랑구 A", 37.5808495, 127.0884925, "중랑구");
    private static readonly DeliveryStore StoreB = new("jungnang-b", "중랑구 B", 37.5823163, 127.0867325, "중랑구");

    private readonly DeliveryRoadNetwork roads;
    private readonly int orderNumber;
    private readonly DeliveryStore pickupStore;
    private readonly DeliveryStore otherStore;
    private readonly int otherNode;
    private readonly int pickupNode;
    private readonly int dropoffNode;
    private readonly DeliveryPath deliveryRoute;
    private readonly Driver[] drivers;
    private readonly Func<string, bool>? isDriverAvailable;
    private readonly Func<string, bool>? hasApproachRoute;
    private readonly List<FoodManagementEvent> events = new();
    private readonly IReadOnlyList<FoodManagementEvent> eventView;
    private TimeSpan elapsed;
    private AcceptedCommand? acceptedCommand;
    private Driver? assignedDriver;
    private DeliveryPath? approachRoute;
    private TimeSpan departureAt;
    private TimeSpan arrivalAt;
    private TimeSpan pickupAt;
    private TimeSpan deliveredAt;
    private IReadOnlyList<FoodManagementCandidate> assignmentCandidates = Array.Empty<FoodManagementCandidate>();
    private Step[] steps = Array.Empty<Step>();
    private int nextStep;
    private bool manualPickup;
    private bool pickupConfirmed;
    private PickupCommand? acceptedPickupCommand;

    private FoodManagementSession(DeliveryRoadNetwork roads, OrderIdentity order, StoreStop pickup, StoreStop other,
        int dropoffNode, DeliveryPath deliveryRoute, Driver[] drivers, Func<string, bool>? isDriverAvailable,
        Func<string, bool>? hasApproachRoute)
    {
        this.roads = roads;
        orderNumber = order.Number;
        OrderId = order.Id;
        TerritoryId = order.TerritoryId;
        TerritoryName = order.TerritoryName;
        Menu = order.Menu;
        pickupStore = pickup.Store;
        otherStore = other.Store;
        pickupNode = pickup.Node;
        otherNode = other.Node;
        this.dropoffNode = dropoffNode;
        this.deliveryRoute = deliveryRoute;
        this.isDriverAvailable = isDriverAvailable;
        this.hasApproachRoute = hasApproachRoute;
        this.drivers = drivers;
        eventView = events.AsReadOnly();
        Record(TimeSpan.Zero, "order_received", $"가상 주문 {OrderId} 접수 · {pickupStore.Name} {Menu}");
    }

    internal string TerritoryId { get; }
    internal string TerritoryName { get; }
    internal string OrderId { get; }
    internal string PickupStoreId => pickupStore.Id;
    internal string PickupStoreName => pickupStore.Name;
    internal string? District => pickupStore.District;
    internal int DropoffNode => dropoffNode;
    internal string Menu { get; }
    internal DateTime Now => Epoch + elapsed;
    internal DateTime ReadyAt => Epoch.AddMinutes(2);
    internal long Revision { get; private set; } = 1;
    internal string? AssignedDriverId => assignedDriver?.Id;
    internal string? AssignmentCommandId => acceptedCommand?.Id;
    internal string? PickupCommandId => acceptedPickupCommand?.Id;
    internal bool CanConfirmPickup => manualPickup && !pickupConfirmed && Stage == FoodManagementStage.AtPickup && elapsed >= pickupAt;
    internal void EnableManualPickup() { manualPickup = true; }
    internal DateTime? AssignedAt => acceptedCommand?.At;
    internal FoodManagementStage Stage { get; private set; } = FoodManagementStage.Waiting;
    internal IReadOnlyList<FoodManagementEvent> Events => eventView;
    internal FoodManagementSettlement? Settlement { get; private set; }
    internal int DriverPay => Stage is FoodManagementStage.ToDropoff or FoodManagementStage.Delivered
        or FoodManagementStage.Received or FoodManagementStage.Settled
        ? DriverPayAmount : 0;
    internal int AccruedDriverPay { get; private set; }

    internal static FoodManagementSession? Create(DeliveryRoadNetwork roads,
        Func<string, bool>? isDriverAvailable = null, Func<string, bool>? hasApproachRoute = null) =>
        Create(roads, 1, isDriverAvailable, hasApproachRoute);

    internal static FoodManagementSession? CreateSecond(DeliveryRoadNetwork roads, Func<string, bool> isDriverAvailable) =>
        Create(roads, 2, isDriverAvailable, null);

    private static FoodManagementSession? Create(DeliveryRoadNetwork roads, int orderNumber,
        Func<string, bool>? isDriverAvailable, Func<string, bool>? hasApproachRoute)
    {
        if (roads is null) throw new ArgumentNullException(nameof(roads));
        var storeA = roads.Closest(new(StoreA.Latitude, StoreA.Longitude));
        var storeB = roads.Closest(new(StoreB.Latitude, StoreB.Longitude));
        if (storeA == storeB)
        {
            return null;
        }

        var firstDropoff = roads.NearbyDestinations(storeA).FirstOrDefault(node => node != storeB, storeB);
        var pickup = orderNumber == 2 ? storeB : storeA;
        var dropoff = orderNumber == 2
            ? roads.NearbyDestinations(storeB).FirstOrDefault(node => node != storeA && node != firstDropoff, storeA)
            : firstDropoff;
        if (roads.FindPath(pickup, dropoff) is not { Kilometers: > 0 } route)
        {
            return null;
        }

        var secondOrder = orderNumber == 2;
        var order = new OrderIdentity(orderNumber, secondOrder ? "M-002" : "M-001",
            secondOrder ? "제육덮밥" : "김치찌개", "sim-territory:sagajeong", "중랑구 사가정");
        var a = new StoreStop(StoreA, storeA);
        var b = new StoreStop(StoreB, storeB);
        Driver[] drivers =
        new Driver[] {
            new("management-driver-a", "중랑구 기사 A", storeA),
            new("management-driver-b", "중랑구 기사 B", storeB),
        };
        return new(roads, order, secondOrder ? b : a, secondOrder ? a : b, dropoff, route, drivers,
            isDriverAvailable, hasApproachRoute);
    }

    internal static FoodManagementSession? CreateCampaign(DeliveryRoadNetwork roads, FoodCampaignStage stage,
        FoodCampaignDistrict district, int orderNumber, (DeliveryStore Store, int Node) a,
        (DeliveryStore Store, int Node) b, bool useStoreB, string menu, ISet<int> unavailableDestinations,
        Func<string, bool> isDriverAvailable)
    {
        var pickup = useStoreB ? b : a;
        var other = useStoreB ? a : b;
        var dropoff = roads.NearbyDestinations(pickup.Node)
            .FirstOrDefault(node => !unavailableDestinations.Contains(node), -1);
        if (dropoff < 0 || roads.FindPath(pickup.Node, dropoff) is not { Kilometers: > 0 } route)
        {
            return null;
        }

        var order = new OrderIdentity(orderNumber, $"{stage.Id}:{pickup.Store.Id}", menu,
            $"sim-territory:{district.TerritoryKey}", $"{district.District} {district.TerritoryName}");
        Driver[] drivers =
        new Driver[] {
            new($"management-driver-{district.Key}-a", $"{district.District} 기사 A", a.Node),
            new($"management-driver-{district.Key}-b", $"{district.District} 기사 B", b.Node),
        };
        return new(roads, order, new(pickup.Store, pickup.Node), new(other.Store, other.Node), dropoff, route, drivers,
            isDriverAvailable, null);
    }

    internal FoodManagementPreview Preview()
    {
        if (Stage != FoodManagementStage.Waiting)
        {
            return new(Revision, null, assignmentCandidates);
        }

        var approaches = drivers.Select(driver =>
        {
            var route = hasApproachRoute?.Invoke(driver.Id) == false ? null : roads.FindPath(driver.StartNode, pickupNode);
            var travel = route is null ? TimeSpan.Zero : DepartureDelay + TravelTime(route);
            return new Approach(driver, route, travel, isDriverAvailable?.Invoke(driver.Id) != false);
        }).ToArray();
        var evaluated = 음식배달후보선정Policy.판정(approaches.Select(approach => new 음식배달기사후보(
            approach.Driver.Id, 0, (decimal)(approach.Route?.Kilometers ?? 0), (decimal)approach.Travel.TotalMinutes,
            0, PickupRadiusKilometers, available: approach.Available, route: approach.Route is not null)),
            Now, ReadyAt, PickupRadiusKilometers);

        var candidates = evaluated.평가목록.Select(evaluation =>
        {
            var approach = approaches.Single(item => item.Driver.Id == evaluation.후보.기사Id);
            var arrival = approach.Route is null ? (DateTime?)null : Now + approach.Travel;
            return new FoodManagementCandidate(approach.Driver.Id, approach.Driver.Label, evaluation.적격,
                approach.Route?.Kilometers ?? 0, arrival, arrival - ReadyAt,
                evaluation.평가?.설명 ?? ExclusionMessage(evaluation.제외사유));
        }).ToArray();
        return new(Revision, evaluated.선택기사Id, Array.AsReadOnly(candidates));
    }

    internal FoodManagementAssignment Assign(string commandId, string orderId, string driverId, long expectedRevision)
    {
        if (string.IsNullOrWhiteSpace(commandId))
        {
            return new(false, "배정 요청 번호가 없습니다.");
        }

        if (acceptedCommand is { } previous && previous.Id == commandId)
        {
            return previous.OrderId == orderId && previous.DriverId == driverId && previous.Revision == expectedRevision
                ? previous.Result : new(false, "같은 요청 번호에 다른 배정 내용이 들어왔습니다.");
        }

        if (orderId != OrderId)
        {
            return new(false, "현재 가상 주문과 배정 대상이 다릅니다.");
        }

        if (expectedRevision != Revision)
        {
            return new(false, "주문이 바뀌었습니다. 후보를 다시 확인해 주세요.");
        }

        if (Stage != FoodManagementStage.Waiting)
        {
            return new(false, "이미 기사가 배정된 주문입니다.");
        }

        var preview = Preview();
        var candidate = preview.Candidates.SingleOrDefault(item => item.DriverId == driverId);
        var driver = drivers.SingleOrDefault(item => item.Id == driverId);
        if (candidate is not { Eligible: true } || driver is null ||
            roads.FindPath(driver.StartNode, pickupNode) is not { } approach)
        {
            return new(false, candidate?.Reason ?? "배정할 수 있는 기사가 아닙니다.");
        }

        assignedDriver = driver;
        approachRoute = approach;
        assignmentCandidates = preview.Candidates;
        departureAt = elapsed + DepartureDelay;
        arrivalAt = departureAt + TravelTime(approach);
        pickupAt = arrivalAt + PickupDelay;
        if (pickupAt < ReadyAt - Epoch)
        {
            pickupAt = ReadyAt - Epoch;
        }

        deliveredAt = pickupAt + TravelTime(deliveryRoute);
        var receiptAt = deliveredAt + ReceiptDelay;
        steps =
        new Step[] {
            new(elapsed + AcceptanceDelay, FoodManagementStage.Accepted, "driver_accepted", $"{driver.Label} 가상 배정 수락"),
            new(departureAt, FoodManagementStage.ToPickup, "driver_departed", $"{driver.Label} 출발 · {pickupStore.Name}으로 이동"),
            new(arrivalAt, FoodManagementStage.AtPickup, "pickup_arrived", $"{driver.Label} 매장 도착 · 음식 준비 확인"),
            new(pickupAt, FoodManagementStage.ToDropoff, "food_picked_up", $"{driver.Label} 음식 픽업 · 가상 배송지로 이동"),
            new(deliveredAt, FoodManagementStage.Delivered, "food_delivered", $"{driver.Label} 전달 완료"),
            new(receiptAt, FoodManagementStage.Received, "customer_received", "가상 주문자 수령 확인"),
            new(receiptAt + SettlementDelay, FoodManagementStage.Settled, "virtual_settlement_recorded", "가상 정산 기록"),
        };
        Stage = FoodManagementStage.Assigned;
        Revision++;
        Record(elapsed, "assignment_designated", $"운영자 가상 배정 지정 · {driver.Label}");
        var result = new FoodManagementAssignment(true, $"{driver.Label}에게 배정했습니다.");
        acceptedCommand = new(commandId, orderId, driverId, expectedRevision, Now, result);
        return result;
    }

    internal void Tick(TimeSpan newElapsed)
    {
        if (newElapsed < elapsed)
        {
            return;
        }

        elapsed = newElapsed;
        while (nextStep < steps.Length && steps[nextStep].At <= elapsed)
        {
            if (manualPickup && !pickupConfirmed && steps[nextStep].Stage == FoodManagementStage.ToDropoff)
                break;
            var step = steps[nextStep++];
            Stage = step.Stage;
            Revision++;
            Record(step.At, step.Code, step.Text);
            if (Stage == FoodManagementStage.Delivered)
            {
                AccruedDriverPay = DriverPayAmount;
                Record(step.At, "driver_pay_accrued", FormattableString.Invariant($"가상 기사 보수 발생 · {DriverPayAmount:N0}원"));
            }

            if (Stage == FoodManagementStage.Settled)
            {
                Settlement = new(deliveredAt, RevenueAmount, AccruedDriverPay, RevenueAmount - AccruedDriverPay,
                    assignedDriver!.Id, Epoch + pickupAt, Epoch + deliveredAt);
            }
        }
    }

    internal FoodManagementAssignment ConfirmPickup(string commandId, string orderId, string driverId, long expectedRevision)
    {
        if (acceptedPickupCommand is { } previous && previous.Id == commandId)
            return previous.OrderId == orderId && previous.DriverId == driverId && previous.Revision == expectedRevision
                ? previous.Result : new(false, "같은 요청 번호에 다른 픽업 내용이 들어왔습니다.");
        if (!manualPickup || string.IsNullOrWhiteSpace(commandId) || commandId == AssignmentCommandId)
            return new(false, "현재 회차의 픽업 확인 요청을 확인하세요.");
        if (orderId != OrderId || driverId != AssignedDriverId)
            return new(false, "현재 주문의 담당 기사만 픽업을 확인할 수 있습니다.");
        if (expectedRevision != Revision)
            return new(false, "주문이 바뀌었습니다. 현재 상태를 다시 확인해 주세요.");
        if (!CanConfirmPickup)
            return new(false, Stage == FoodManagementStage.AtPickup ? "음식 준비를 기다린 뒤 확인해 주세요." : "매장 도착 후 픽업을 확인해 주세요.");

        var delay = elapsed - pickupAt;
        pickupAt = elapsed;
        deliveredAt += delay;
        for (var index = nextStep; index < steps.Length; index++)
            steps[index] = steps[index] with { At = steps[index].At + delay };
        pickupConfirmed = true;
        Record(elapsed, "app_pickup_confirmed", "업무 화면에서 가상 픽업 확인");
        var result = new FoodManagementAssignment(true, "픽업을 확인했습니다. 가상 배송지로 출발합니다.");
        acceptedPickupCommand = new(commandId, orderId, driverId, expectedRevision, result);
        Tick(elapsed);
        return result;
    }

    internal DeliveryMapSnapshot MapSnapshot()
    {
        var pickup = roads[pickupNode];
        var other = roads[otherNode];
        var dropoff = roads[dropoffNode];
        var orderPhase = Stage switch
        {
            FoodManagementStage.Waiting => DeliveryPhase.Waiting,
            FoodManagementStage.Assigned or FoodManagementStage.Accepted or FoodManagementStage.ToPickup => DeliveryPhase.ToPickup,
            FoodManagementStage.AtPickup => DeliveryPhase.AtPickup,
            FoodManagementStage.ToDropoff => DeliveryPhase.ToDropoff,
            FoodManagementStage.Delivered => DeliveryPhase.Delivered,
            _ => DeliveryPhase.Completed,
        };
        var couriers = drivers.Select(driver =>
        {
            var position = roads[driver.StartNode];
            var phase = "Idle";
            DeliveryPoint[] route = Array.Empty<DeliveryPoint>();
            if (driver == assignedDriver)
            {
                switch (Stage)
                {
                    case FoodManagementStage.ToPickup:
                        var approach = approachRoute!;
                        route = approach.Points;
                        position = approach.Position((elapsed - departureAt) / (arrivalAt - departureAt));
                        phase = nameof(DeliveryPhase.ToPickup);
                        break;
                    case FoodManagementStage.AtPickup:
                        position = pickup;
                        phase = nameof(DeliveryPhase.AtPickup);
                        break;
                    case FoodManagementStage.ToDropoff:
                        route = deliveryRoute.Points;
                        position = deliveryRoute.Position((elapsed - pickupAt) / (deliveredAt - pickupAt));
                        phase = nameof(DeliveryPhase.ToDropoff);
                        break;
                    case FoodManagementStage.Delivered:
                    case FoodManagementStage.Received:
                    case FoodManagementStage.Settled:
                        position = dropoff;
                        break;
                }
            }

            return new DeliveryMapCourier(driver.Id, driver.Label, position.Latitude, position.Longitude, phase, route,
                pickupStore.District, driver == assignedDriver ? pickupStore.Id : null);
        }).ToArray();

        return new(new DeliveryMapOrder[] { new(orderNumber, orderPhase.ToString(), dropoff.Latitude, dropoff.Longitude,
                $"{OrderId} 가상 배송지", pickupStore.District, pickupStore.Id, AssignedDriverId) },
            new DeliveryStore[] { pickupStore with { Latitude = pickup.Latitude, Longitude = pickup.Longitude },
             otherStore with { Latitude = other.Latitude, Longitude = other.Longitude } },
            couriers, new[] { pickup, other, dropoff });
    }

    private static TimeSpan TravelTime(DeliveryPath route) =>
        TimeSpan.FromSeconds(Math.Max(30, route.Kilometers / SpeedKilometersPerHour * 3600));

    private void Record(TimeSpan at, string code, string text) => events.Add(new(Epoch + at, text, code));

    private static string ExclusionMessage(string reason) => reason switch
    {
        "DriverUnavailable" => "기사가 배정 가능 상태가 아닙니다.",
        "NoTraversableRoute" => "매장으로 이어지는 경로를 찾을 수 없습니다.",
        "ApproachRadiusExceeded" => "매장 접근 거리 한도를 넘었습니다.",
        _ => "현재 조건에서 배정할 수 없습니다.",
    };

    private sealed record Driver(string Id, string Label, int StartNode);
    private sealed record OrderIdentity(int Number, string Id, string Menu, string TerritoryId, string TerritoryName);
    private sealed record StoreStop(DeliveryStore Store, int Node);
    private sealed record Approach(Driver Driver, DeliveryPath? Route, TimeSpan Travel, bool Available);
    private sealed record AcceptedCommand(string Id, string OrderId, string DriverId, long Revision,
        DateTime At, FoodManagementAssignment Result);
    private sealed record PickupCommand(string Id, string OrderId, string DriverId, long Revision, FoodManagementAssignment Result);
    private sealed record Step(TimeSpan At, FoodManagementStage Stage, string Code, string Text);
}

}
