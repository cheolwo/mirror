using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.BatchMonitor.Windows
{

internal sealed record ObservationEvent(string Id, string WorkId, string Kind, string Stage,
    TimeSpan At, string Actor, string SourceRevision, string Message);

internal sealed record FreightMapSnapshot(string Id, string Phase, string Label,
    DeliveryPoint Position, DeliveryPoint Pickup, DeliveryPoint Dropoff, DeliveryPoint[] Route,
    string? PickupDistrict = null, string? DropoffDistrict = null);

internal sealed class FreightReplay
{
    internal const string SourceRevision = "SIM-FREIGHT-TRANSPORT-1/workflow-rules.v1";

    private const int CargoKilograms = 300;
    private const int CapacityKilograms = 400;
    private const int EventLimit = 32;
    private static readonly TimeSpan Retention = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan NextWorkDelay = TimeSpan.FromSeconds(3);

    private static readonly TimeSpan TransitStartsAt = TimeSpan.FromSeconds(10);

    private readonly ReplayRoute[] routes;
    private readonly List<ObservationEvent> events = new();
    private TimeSpan elapsed;
    private TimeSpan cycleStartsAt;
    private int nextStage;
    private long cycle = 1;

    private FreightReplay(ReplayRoute[] routes) => this.routes = routes;

    private static ReplayRoute BuildRoute(DeliveryPath path, string? pickupDistrict = null, string? dropoffDistrict = null)
    {
        var transitEndsAt = TransitStartsAt + TimeSpan.FromSeconds(Math.Clamp(path.Kilometers * 10, 5,
            pickupDistrict is null ? 14 : 60));
        var routeLabel = pickupDistrict is null ? null : $"{pickupDistrict} → {dropoffDistrict}";

        // Observed Simulation test stages. The timings and the map positions are synthetic.
        ReplayStage[] stages =
        new ReplayStage[] {
            new(TimeSpan.Zero, 화물운송상태코드.배차대기, "가상 화주", $"감자 {CargoKilograms}kg 운송 의뢰{(routeLabel is null ? "" : $" · {routeLabel}")}"),
            new(TimeSpan.FromSeconds(2), 화물운송상태코드.매칭중, "가상 배차", $"후보 차량 용량 {CapacityKilograms}kg 확인"),
            new(TimeSpan.FromSeconds(4), 화물운송상태코드.배차확정, "가상 배차", "가상 화물차 배차 확정"),
            new(TimeSpan.FromSeconds(6), 화물운송상태코드.상차지도착, "가상 화물기사", "상차지 도착"),
            new(TimeSpan.FromSeconds(8), 화물운송상태코드.상차완료, "가상 화물기사", "상차 완료"),
            new(TransitStartsAt, 화물운송상태코드.운송중, "가상 화물기사", "하차지로 이동"),
            new(transitEndsAt, 화물운송상태코드.하차지도착, "가상 화물기사", "하차지 도착 · 인수 확인 대기"),
            new(transitEndsAt + TimeSpan.FromSeconds(3), 화물운송상태코드.인수완료,
                "가상 수령인", "가상 수령인 확인 · 인수 완료"),
        };
        return new(path, pickupDistrict, dropoffDistrict, routeLabel, transitEndsAt, stages);
    }

    internal int CompletedCount { get; private set; }
    internal IReadOnlyList<ObservationEvent> Events => events;
    internal string? CurrentStatus => VisibleStage is { } stage ? $"{WorkId} · {stage.Code}" : null;
    internal string? CurrentRoute => VisibleStage is null ? null : Route.Label;

    internal static FreightReplay? TryCreate(DeliveryRoadNetwork network, IReadOnlyList<DistrictStop>? districtStops = null)
    {
        if (districtStops is { Count: > 1 })
        {
            var regionalRoutes = new ReplayRoute[districtStops.Count];
            for (var index = 0; index < districtStops.Count; index++)
            {
                var pickup = districtStops[index];
                var dropoff = districtStops[(index + 1) % districtStops.Count];
                if (network.FindPath(pickup.RoadNode, dropoff.RoadNode) is not { Kilometers: > 0 } path)
                {
                    return null;
                }

                regionalRoutes[index] = BuildRoute(path, pickup.Name, dropoff.Name);
            }

            return new(regionalRoutes);
        }

        var coverage = network.Coverage;
        var southwest = coverage[0];
        var northeast = coverage[1];
        var stops = new[]
        {
            southwest,
            northeast,
            new DeliveryPoint(southwest.Latitude, northeast.Longitude),
            new DeliveryPoint(northeast.Latitude, southwest.Longitude),
            new DeliveryPoint((southwest.Latitude + northeast.Latitude) / 2,
                (southwest.Longitude + northeast.Longitude) / 2),
        }.Select(network.Closest).Distinct().ToArray();

        foreach (var pickup in stops)
        {
            foreach (var dropoff in stops.Where(stop => stop != pickup).Concat(network.NearbyDestinations(pickup)))
            {
                if (network.FindPath(pickup, dropoff) is { Kilometers: > 0 } path)
                {
                    return new(new[] { BuildRoute(path) });
                }
            }
        }

        return null;
    }

    internal void Tick(TimeSpan currentElapsed)
    {
        if (currentElapsed < elapsed)
        {
            return;
        }

        elapsed = currentElapsed;
        while (true)
        {
            var stages = Route.Stages;
            if (nextStage == stages.Length)
            {
                var nextCycleStartsAt = cycleStartsAt + stages[^1].Offset + Retention + NextWorkDelay;
                if (elapsed < nextCycleStartsAt)
                {
                    return;
                }

                cycleStartsAt = nextCycleStartsAt;
                cycle++;
                nextStage = 0;
                continue;
            }

            var stage = stages[nextStage];
            var eventTime = cycleStartsAt + stage.Offset;
            if (elapsed < eventTime)
            {
                return;
            }

            nextStage++;
            if (stage.Code == 화물운송상태코드.인수완료)
            {
                CompletedCount++;
            }

            var workId = WorkId;
            events.Insert(0, new($"freight:{workId}:{stage.Code}", workId, "freight", stage.Code, eventTime,
                stage.Actor, SourceRevision, $"{workId} · {stage.Message}"));
            if (events.Count > EventLimit)
            {
                events.RemoveAt(EventLimit);
            }
        }
    }

    internal FreightMapSnapshot? MapSnapshot()
    {
        if (VisibleStage is not { } stage)
        {
            return null;
        }

        var route = Route;
        var path = route.Path;
        var localElapsed = elapsed - cycleStartsAt;
        var fraction = (localElapsed - TransitStartsAt) / (route.TransitEndsAt - TransitStartsAt);
        var workId = WorkId;
        var label = $"가상 화물 {workId} · 감자 {CargoKilograms}kg";
        if (route.Label is { } routeLabel)
        {
            label += $" · {routeLabel}";
        }
        return new(workId, stage.Code, label, path.Position(fraction), path.Points[0], path.Points[^1], path.Points,
            route.PickupDistrict, route.DropoffDistrict);
    }

    private string WorkId => FormattableString.Invariant($"F{cycle:0000}");

    private ReplayRoute Route => routes[(int)((cycle - 1) % routes.Length)];

    private ReplayStage? VisibleStage
    {
        get
        {
            var stages = Route.Stages;
            return nextStage == 0 ||
                (nextStage == stages.Length && elapsed >= cycleStartsAt + stages[^1].Offset + Retention)
                ? null
                : stages[nextStage - 1];
        }
    }

    private sealed record ReplayRoute(DeliveryPath Path, string? PickupDistrict, string? DropoffDistrict,
        string? Label, TimeSpan TransitEndsAt, ReplayStage[] Stages);

    private sealed record ReplayStage(TimeSpan Offset, string Code, string Actor, string Message);
}

}
