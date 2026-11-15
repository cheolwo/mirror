using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using Ssalddel.WorkflowRules;

namespace Ssalddel.BatchMonitor.Windows
{

internal enum DeliveryPhase { Waiting, ToPickup, AtPickup, ToDropoff, Delivered, Completed }

internal sealed record DeliveryStore(string Id, string Name, double Latitude, double Longitude,
    string? District = null, DeliveryPoint? FreightStop = null);
internal sealed record DeliveryOrderSample(string StoreId, string Menu, string? ObservedTime);
internal sealed record DeliverySample(DeliveryStore[] Stores, DeliveryOrderSample[] Orders);
internal sealed record DeliveryMapOrder(long Number, string Phase, double Latitude, double Longitude, string Title,
    string? District = null, string? StoreId = null, string? CourierId = null);
internal sealed record DeliveryMapCourier(string Id, string Label, double Latitude, double Longitude, string Phase,
    DeliveryPoint[] Route, string? District = null, string? PickupStoreId = null);
internal sealed record DistrictStop(string Name, int RoadNode);
internal sealed record FoodManagementExpansionPin(string Id, string Label, double Latitude, double Longitude);
internal sealed record FoodManagementMapInput(string[] PendingStoreIds, string? SelectedDriverId, bool AssignmentPending,
    string[]? AvailableDriverIds = null, FoodManagementExpansionPin[]? ExpansionOptions = null);
internal sealed record DeliveryMapSnapshot(DeliveryMapOrder[] Orders, DeliveryStore[] Restaurants, DeliveryMapCourier[] Couriers,
    DeliveryPoint[] Coverage, FreightMapSnapshot? Freight = null, string? ViewScope = null,
    FoodManagementMapInput? Management = null, string? MapFitKey = null);

internal sealed class DeliveryOrder
{
    private readonly DeliveryOrderSample sample;
    public DeliveryOrder(long number, DeliveryStore store, DeliveryOrderSample sample, int pickup, int dropoff, TimeSpan readyAt)
    { Number = number; Store = store; this.sample = sample; Pickup = pickup; Dropoff = dropoff; ReadyAt = readyAt; }
    public long Number { get; }
    public DeliveryStore Store { get; }
    public string Menu => sample.Menu;
    public string? ObservedTime => sample.ObservedTime;
    public int Pickup { get; }
    public int Dropoff { get; }
    public TimeSpan ReadyAt { get; }
    public DeliveryPhase Phase { get; set; }
    public TimeSpan DeliveredAt { get; set; }
    public TimeSpan CompletedAt { get; set; }
    public string? CourierLabel { get; set; }
    public string? CourierId { get; set; }
}

internal sealed class DeliveryCourier
{
    public DeliveryCourier(string id, string label, int node, string? district)
    { Id = id; Label = label; Node = node; District = district; }
    public string Id { get; }
    public string Label { get; }
    public string? District { get; }
    public string? LastPickupStoreId { get; set; }
    public int Node { get; set; }
    public DeliveryOrder? Order { get; set; }
    public DeliveryPath? Path { get; set; }
    public TimeSpan DepartedAt { get; set; }
    public TimeSpan NextAt { get; set; }
}

internal sealed class DeliverySimulation
{
    private const int ClockScale = 12;
    private const double SpeedKmPerHour = 18;
    private static readonly TimeSpan OrderInterval = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan Retention = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan ReceiptDelay = TimeSpan.FromSeconds(2);
    internal const string SourceRevision = "SIM-FOOD-DELIVERY-1:batch-monitor.v1";
    private static readonly DateTime Epoch = new(2026, 1, 1);
    
    private readonly DeliveryRoadNetwork network;
    private readonly DeliveryStore[] stores;
    private readonly (DeliveryStore Store, DeliveryOrderSample Sample, int Pickup, int[] Destinations)[] templates;
    private readonly DeliveryPoint[] coverage;
    private readonly List<DeliveryOrder> orders = new();
    private readonly DeliveryCourier[] couriers;
    private readonly List<ObservationEvent> events = new();
    private long nextEvent = 1;
    private TimeSpan nextArrival;
    private long nextNumber = 1;

    private DeliverySimulation(DeliverySample sample, DeliveryRoadNetwork roads)
    {
        if (sample.Stores is not { Length: >= 3 } || sample.Orders is not { Length: > 0 } ||
            sample.Stores.Any(store => store is null || string.IsNullOrWhiteSpace(store.Id) ||
                string.IsNullOrWhiteSpace(store.Name) || !new DeliveryPoint(store.Latitude, store.Longitude).IsValid) ||
            sample.Orders.Any(order => order is null || string.IsNullOrWhiteSpace(order.StoreId) || string.IsNullOrWhiteSpace(order.Menu)))
        {
            throw new ArgumentException("Invalid delivery sample.");
        }

        network = roads;
        Sample = sample;
        coverage = roads.Coverage;
        stores = sample.Stores;
        var pickups = stores.ToDictionary(store => store.Id, store =>
        {
            var pickup = roads.Closest(new(store.Latitude, store.Longitude));
            return (Store: store, Pickup: pickup, Destinations: roads.NearbyDestinations(pickup));
        });
        // In the district sample, a handoff point must not also be the next shop.
        var pickupNodes = pickups.Values.Where(stop => stop.Store.District is not null)
            .Select(stop => stop.Pickup).ToHashSet();
        templates = sample.Orders.Select(order =>
        {
            if (!pickups.TryGetValue(order.StoreId, out var stop))
            {
                throw new ArgumentException("Order store is missing.");
            }

            var destinations = stop.Destinations.Where(node => !pickupNodes.Contains(node)).ToArray();
            if (destinations.Length == 0)
            {
                throw new ArgumentException("Order has no connected delivery destination.");
            }

            return (stop.Store, order, stop.Pickup, destinations);
        }).ToArray();
        if (stores.All(store => store.District is null))
        {
            var starts = pickups.Values.Select(stop => stop.Pickup).Distinct().Take(3).ToArray();
            if (starts.Length < 3 || starts.Skip(1).Any(node => roads.FindPath(starts[0], node) is null))
            {
                throw new ArgumentException("The three rider starts must share a connected road network.");
            }

            couriers = starts.Select((node, index) =>
                new DeliveryCourier($"r{index + 1}", $"{index + 1}번 기사", node, null)).ToArray();
            return;
        }

        var districts = stores.GroupBy(store => store.District, StringComparer.Ordinal).ToArray();
        if (districts.Length < 2 || stores.Any(store => string.IsNullOrWhiteSpace(store.District) ||
                (store.FreightStop is not null && !store.FreightStop.IsValid)) ||
            districts.Any(district => district.Select(store => pickups[store.Id].Pickup).Distinct().Count() < 2 ||
                district.Count(store => store.FreightStop is not null) != 1) ||
            stores.Any(store => !sample.Orders.Any(order => order.StoreId == store.Id)))
        {
            throw new ArgumentException("Each district needs two distinct pickup locations with orders and one freight stop.");
        }

        DistrictStops = districts.Select(district => new DistrictStop(district.Key!,
            roads.Closest(district.Single(store => store.FreightStop is not null).FreightStop!))).ToArray();
        if (DistrictStops.Select(stop => stop.RoadNode).Distinct().Count() != DistrictStops.Count ||
            DistrictStops.Any(stop => roads.FindPath(DistrictStops[0].RoadNode, stop.RoadNode) is null))
        {
            throw new ArgumentException("District freight stops must be distinct and connected.");
        }

        couriers = districts.Select((district, index) => new DeliveryCourier($"r{index + 1}", $"{district.Key} 기사",
            pickups[district.First().Id].Pickup, district.Key)).ToArray();
    }

    public TimeSpan Elapsed { get; private set; }
    public IReadOnlyList<DeliveryOrder> Orders => orders;
    public IReadOnlyList<DeliveryCourier> Couriers => couriers;
    public IReadOnlyList<ObservationEvent> Events => events;
    public DeliveryRoadNetwork RoadNetwork => network;
    internal DeliverySample Sample { get; }
    public IReadOnlyList<DistrictStop> DistrictStops { get; } = Array.Empty<DistrictStop>();
    public int CompletedCount { get; private set; }
    public int SampleCount => templates.Length;
    public int StoreCount => stores.Length;

    public static DeliverySimulation? Load(string directory)
    {
        // A loose pair keeps the original sample/replay usable. A plain Release uses the bundled district sample.
        if (!File.Exists(Path.Combine(directory, "delivery-sample.json")) && !File.Exists(Path.Combine(directory, "road-graph.json")))
        {
            directory = Path.Combine(directory, "Samples", "SeoulEast");
        }

        try
        {
            var deliveries = JsonConvert.DeserializeObject<DeliverySample>(File.ReadAllText(Path.Combine(directory, "delivery-sample.json")));
            var roads = JsonConvert.DeserializeObject<DeliveryRoadSample>(File.ReadAllText(Path.Combine(directory, "road-graph.json")));
            return deliveries is null || roads is null ? null : new(deliveries, new(roads));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            return null;
        }
    }

    public void Tick(TimeSpan elapsed)
    {
        Elapsed = elapsed;
        foreach (var order in orders.Where(order => order.Phase == DeliveryPhase.Delivered && elapsed >= order.DeliveredAt + ReceiptDelay))
        {
            order.Phase = DeliveryPhase.Completed;
            order.CompletedAt = elapsed;
            CompletedCount++;
            Log(order, "수령확인", "가상 주문자", "가상 주문자 수령 확인");
        }

        foreach (var order in orders.Where(order => order.Phase == DeliveryPhase.Completed && elapsed >= order.CompletedAt + Retention).ToArray())
        {
            orders.Remove(order);
            Log(order, "표시정리", "관찰 화면", "완료 표시 정리");
        }

        foreach (var courier in couriers)
        {
            Advance(courier);
        }

        if (elapsed >= nextArrival)
        {
            nextArrival = elapsed + OrderInterval;
            if (orders.Count(order => order.Phase == DeliveryPhase.Waiting) < 12)
            {
                ReceiveOrder();
            }
        }

        DispatchWaiting();
    }

    private void ReceiveOrder()
    {
        var (store, sample, pickup, destinations) = templates[(int)((nextNumber - 1) % templates.Length)];
        // Only public road nodes are eligible. Keep active order numbers at distinct points.
        var used = orders.Select(order => order.Dropoff).ToHashSet();
        var offset = (int)((nextNumber * 17 - 6) % destinations.Length);
        for (var index = 0; index < destinations.Length; index++)
        {
            var dropoff = destinations[(offset + index) % destinations.Length];
            if (used.Contains(dropoff))
            {
                continue;
            }

            var readyAt = Elapsed + TimeSpan.FromSeconds(10 + nextNumber % 3 * 3);
            var order = new DeliveryOrder(nextNumber++, store, sample, pickup, dropoff, readyAt);
            orders.Add(order);
            Log(order, "주문접수", "가상 주문자", $"접수 · {store.Name} · {sample.Menu}");
            return;
        }
    }

    private void DispatchWaiting()
    {
        foreach (var order in orders.Where(order => order.Phase == DeliveryPhase.Waiting))
        {
            var candidates = couriers.Where(courier => courier.Order is null && courier.District == order.Store.District &&
                    (courier.District is null || courier.LastPickupStoreId != order.Store.Id))
                .Select(courier => (Courier: courier, Path: network.FindPath(courier.Node, order.Pickup)))
                .Where(candidate => candidate.Path is not null).ToArray();
            if (candidates.Length == 0)
            {
                // A busy district must not block the next waiting order in another district.
                continue;
            }

            var result = 음식배달후보선정Policy.판정(candidates.Select(candidate =>
            {
                var path = candidate.Path!;
                var minutes = TravelTime(path).TotalSeconds * ClockScale / 60;
                return new 음식배달기사후보(candidate.Courier.Id, 0, (decimal)path.Kilometers,
                    (decimal)minutes, 0, 5);
            }), ToSimulationClock(Elapsed), ToSimulationClock(order.ReadyAt), 5);
            if (result.선택기사Id is not { } selected)
            {
                continue;
            }

            var (courier, route) = candidates.Single(candidate => candidate.Courier.Id == selected);
            var path = route!;
            var evaluation = result.평가목록.Single(item => item.후보.기사Id == selected).평가!;
            order.CourierLabel = courier.Label;
            order.CourierId = courier.Id;
            courier.Order = order;
            Move(courier, path, DeliveryPhase.ToPickup);
            Log(order, "기사배정", "가상 배차", $"배차 · {courier.Label} · 픽업까지 {path.Kilometers * 1000:0}m · {evaluation.설명}");
        }
    }

    private void Advance(DeliveryCourier courier)
    {
        if (courier.Order is not { } order || Elapsed < courier.NextAt)
        {
            return;
        }

        switch (order.Phase)
        {
            case DeliveryPhase.ToPickup:
                courier.Node = order.Pickup;
                courier.Path = null;
                courier.NextAt = order.ReadyAt > Elapsed + TimeSpan.FromSeconds(2)
                    ? order.ReadyAt : Elapsed + TimeSpan.FromSeconds(2);
                order.Phase = DeliveryPhase.AtPickup;
                Log(order, "매장도착", courier.Label, $"{courier.Label} 매장 도착 · 음식 준비 확인");
                break;
            case DeliveryPhase.AtPickup:
                Move(courier, network.FindPath(order.Pickup, order.Dropoff)!, DeliveryPhase.ToDropoff);
                var pickup = courier.District is null ? "음식 픽업" : $"{order.Store.Name} 음식 픽업";
                Log(order, "픽업완료", courier.Label, $"{courier.Label} {pickup} · 가상 배송지로 이동");
                break;
            case DeliveryPhase.ToDropoff:
                courier.Node = order.Dropoff;
                courier.LastPickupStoreId = order.Store.Id;
                courier.Path = null;
                courier.Order = null;
                order.Phase = DeliveryPhase.Delivered;
                order.DeliveredAt = Elapsed;
                Log(order, "전달완료", courier.Label, $"{courier.Label} 전달 완료");
                break;
        }
    }

    public DeliveryMapSnapshot MapSnapshot() => new(
        orders.Select(order =>
        {
            var position = network[order.Dropoff];
            return new DeliveryMapOrder(order.Number, order.Phase.ToString(), position.Latitude, position.Longitude,
                $"가상 배송지 #{order.Number:00}", order.Store.District, order.Store.Id, order.CourierId);
        }).ToArray(),
        stores,
        couriers.Select(courier =>
        {
            var position = courier.Path?.Position((Elapsed - courier.DepartedAt) / (courier.NextAt - courier.DepartedAt))
                ?? network[courier.Node];
            return new DeliveryMapCourier(courier.Id, courier.Label, position.Latitude, position.Longitude,
                courier.Order?.Phase.ToString() ?? "Idle", courier.Path?.Points ?? Array.Empty<DeliveryPoint>(), courier.District,
                courier.Order?.Store.Id ?? courier.LastPickupStoreId);
        }).ToArray(),
        coverage);

    private void Move(DeliveryCourier courier, DeliveryPath path, DeliveryPhase phase)
    {
        courier.Path = path;
        courier.DepartedAt = Elapsed;
        courier.NextAt = Elapsed + TravelTime(path);
        courier.Order!.Phase = phase;
    }

    private void Log(DeliveryOrder order, string stage, string actor, string message)
    {
        var workId = $"D-{order.Number:00}";
        events.Insert(0, new($"food-{nextEvent++}", workId, "food", stage, Elapsed, actor, SourceRevision, $"{workId} · {message}"));
        if (events.Count > 12)
        {
            events.RemoveAt(events.Count - 1);
        }
    }

    private static TimeSpan TravelTime(DeliveryPath path) =>
        TimeSpan.FromSeconds(Math.Max(2.5, path.Kilometers / SpeedKmPerHour * 3600 / ClockScale));

    private static DateTime ToSimulationClock(TimeSpan elapsed) => Epoch + elapsed * ClockScale;
}

}
