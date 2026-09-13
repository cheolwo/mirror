using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Application
{
    public static class 음식배달여정ProjectionErrorCodes
    {
        public const string InputInvalid = "FoodDeliveryJourneyInputInvalid";
        public const string SyntheticFixtureRequired = "FoodDeliveryJourneySyntheticFixtureRequired";
        public const string GraphRevisionMismatch = "FoodDeliveryJourneyGraphRevisionMismatch";
        public const string GraphHashMismatch = "FoodDeliveryJourneyGraphHashMismatch";
        public const string RouteUnresolved = "FoodDeliveryJourneyRouteUnresolved";
        public const string RouteFingerprintMismatch = "FoodDeliveryJourneyRouteFingerprintMismatch";
        public const string SchemaUnsupported = "FoodDeliveryJourneySchemaUnsupported";
        public const string SourceKindInvalid = "FoodDeliveryJourneySourceKindInvalid";
        public const string DistributionForbidden = "FoodDeliveryJourneyDistributionForbidden";
        public const string SnapshotShapeInvalid = "FoodDeliveryJourneySnapshotShapeInvalid";
        public const string ObservedAtInvalid = "FoodDeliveryJourneyObservedAtInvalid";
        public const string StaleSnapshot = "FoodDeliveryJourneyStaleSnapshot";
        public const string StaleRevision = "FoodDeliveryJourneyStaleRevision";
        public const string RevisionConflict = "FoodDeliveryJourneyRevisionConflict";
        public const string IdentityMismatch = "FoodDeliveryJourneyIdentityMismatch";
        public const string ActorPositionMismatch = "FoodDeliveryJourneyActorPositionMismatch";
        public const string VehiclePositionMismatch = "FoodDeliveryJourneyVehiclePositionMismatch";
    }

    /// <summary>SyntheticFixture Simulation 상태를 읽는 투영 요청. 주문 명령이 아니다.</summary>
    public sealed class 합성음식배달여정ProjectionRequest
    {
        public string RegionStableId { get; set; } = string.Empty;
        public string AdministrativeAreaStableId { get; set; } = string.Empty;
        public string VehicleStableId { get; set; } = string.Empty;
        public string RouteStableId { get; set; } = string.Empty;
        public string ExpectedGraphRevision { get; set; } = string.Empty;
        public string ExpectedGraphHash { get; set; } = string.Empty;
        public long Revision { get; set; }
        public DateTimeOffset ObservedAt { get; set; }
        public bool Blocked { get; set; }
    }

    /// <summary>
    /// 기존 합성 주문·기사 사본과 검토된 동네 이동망을 새 읽기 전용 여정으로 투영합니다.
    /// 원본 주문, Actor, 공간 사본을 변경하지 않으며 경로 실패 시 직선 대체 경로를 만들지 않습니다.
    /// </summary>
    [SsalddelCodeMetadata(SsalddelCodeFeatureKeys.SimulationWorldDerivation, SsalddelCodeLayer.Application,
        "합성 음식 배달 상태와 판본화된 이동망을 Unity 읽기 전용 여정으로 투영한다.",
        StepKey = "application.synthetic-food-delivery-journey-projection",
        DependsOnStepKeys = new[] { "application.neighborhood-route", "application.neighborhood-movement-candidate" },
        FlowOrder = 130, ExecutionStage = SsalddelCodeExecutionStage.Projection,
        ReadsFrom = SsalddelCodeDataScope.DerivedWorld, Effects = SsalddelCodeEffect.None,
        Boundary = "SyntheticFixture 전용 읽기 사본이며 도착·보간으로 주문·픽업·전달 상태를 변경하지 않는다.")]
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E2,
        "경로 판본·hash·leg·진행 상태를 비식별 배달 여정 사본으로 생성한다.",
        Boundary = "운영 주문 원장·Unity Scene·GameObject를 변경하지 않는다.")]
    public sealed class 음식배달여정SnapshotFactory
    {
        private readonly 동네이동경로Engine routeEngine = new 동네이동경로Engine();

        public 음식배달여정Snapshot 합성상태에서생성(
            동네공간Snapshot graph,
            Simulation음식배달Snapshot order,
            가상배달기사Snapshot courier,
            합성음식배달여정ProjectionRequest request)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (order == null) throw new ArgumentNullException(nameof(order));
            if (courier == null) throw new ArgumentNullException(nameof(courier));
            if (request == null) throw new ArgumentNullException(nameof(request));
            요구(graph.Source.IsSynthetic, 음식배달여정ProjectionErrorCodes.SyntheticFixtureRequired);
            요구(order.DeliveryScopeStableId == "delivery-scope:synthetic"
                && 합성Id(order.FoodOrderStableId) && 합성Id(courier.ActorStableId)
                && 합성Id(request.VehicleStableId) && 합성Id(request.RouteStableId),
                음식배달여정ProjectionErrorCodes.SyntheticFixtureRequired);
            요구(courier.OrderStableId == order.FoodOrderStableId,
                음식배달여정ProjectionErrorCodes.IdentityMismatch);
            요구(지역Id(request.RegionStableId) && 행정동Id(request.AdministrativeAreaStableId)
                && request.Revision > 0 && request.ObservedAt != default
                && request.ObservedAt.Offset == TimeSpan.Zero,
                음식배달여정ProjectionErrorCodes.InputInvalid);
            요구(graph.Source.SourceVersion == request.ExpectedGraphRevision,
                음식배달여정ProjectionErrorCodes.GraphRevisionMismatch);
            요구(해시형식(request.ExpectedGraphHash)
                && string.Equals(graph.Revision, request.ExpectedGraphHash, StringComparison.OrdinalIgnoreCase),
                음식배달여정ProjectionErrorCodes.GraphHashMismatch);

            var destination = 목적지(order.DestinationFacilityStableId);
            var definitions = 여정정의(destination);
            var routes = definitions.Select(definition => new LegRoute(
                definition,
                routeEngine.탐색(graph, graph.Revision, definition.FromNodeStableId,
                    definition.ToNodeStableId, (동네이동수단)(int)definition.Mode))).ToArray();
            var stage = 현재Leg(courier.Stage, courier.Distance, routes);
            var unresolvedIndex = Array.FindIndex(routes, value => !value.Route.Found);
            if (unresolvedIndex >= 0)
                return 미해결생성(graph, order, courier, request, routes, stage);

            요구(유한(stage.ProgressMeters) && stage.ProgressMeters >= 0
                && stage.ProgressMeters <= routes[stage.Index].Route.LengthMeters,
                음식배달여정ProjectionErrorCodes.InputInvalid);
            var legs = 상태Leg생성(graph, routes, stage.Index, stage.ProgressMeters, request.Blocked);
            var current = legs[stage.Index];
            요구(같은위치(current.ActorPosition, courier.X, courier.Z),
                음식배달여정ProjectionErrorCodes.ActorPositionMismatch);
            요구(current.VehiclePosition != null
                && 같은위치(current.VehiclePosition, courier.VehicleX, courier.VehicleZ),
                음식배달여정ProjectionErrorCodes.VehiclePositionMismatch);
            return Snapshot(graph, order, courier, request, legs, stage.Index,
                음식배달여정ReadinessCodes.Ready, request.Blocked);
        }

        private static 음식배달여정Snapshot 미해결생성(
            동네공간Snapshot graph,
            Simulation음식배달Snapshot order,
            가상배달기사Snapshot courier,
            합성음식배달여정ProjectionRequest request,
            LegRoute[] routes,
            (int Index, double ProgressMeters) stage)
        {
            요구(유한(courier.X) && 유한(courier.Z)
                && 유한(courier.VehicleX) && 유한(courier.VehicleZ),
                음식배달여정ProjectionErrorCodes.InputInvalid);
            var progress = routes[stage.Index].Route.Found ? stage.ProgressMeters : 0;
            요구(유한(progress) && progress >= 0
                && progress <= routes[stage.Index].Route.LengthMeters,
                음식배달여정ProjectionErrorCodes.InputInvalid);
            var legs = 상태Leg생성(graph, routes, stage.Index, progress, true);
            var current = legs[stage.Index];
            if (routes[stage.Index].Route.Found)
            {
                요구(같은위치(current.ActorPosition, courier.X, courier.Z),
                    음식배달여정ProjectionErrorCodes.ActorPositionMismatch);
                요구(current.VehiclePosition != null
                    && 같은위치(current.VehiclePosition, courier.VehicleX, courier.VehicleZ),
                    음식배달여정ProjectionErrorCodes.VehiclePositionMismatch);
            }
            else
            {
                // 경로를 복원할 수 없으면 선분을 추정하지 않고 Simulation의 마지막 확인 위치에 멈춘다.
                current.ActorPosition = new 음식배달여정Point { X = courier.X, Z = courier.Z };
                current.VehiclePosition = new 음식배달여정Point
                { X = courier.VehicleX, Z = courier.VehicleZ };
            }
            return Snapshot(graph, order, courier, request, legs, stage.Index,
                음식배달여정ReadinessCodes.RouteUnresolved, true);
        }

        private static 음식배달여정Snapshot Snapshot(
            동네공간Snapshot graph,
            Simulation음식배달Snapshot order,
            가상배달기사Snapshot courier,
            합성음식배달여정ProjectionRequest request,
            음식배달여정Leg[] legs,
            int currentLegIndex,
            string readiness,
            bool blocked)
        {
            var snapshot = new 음식배달여정Snapshot
            {
                RegionStableId = request.RegionStableId.Trim(),
                AdministrativeAreaStableId = request.AdministrativeAreaStableId.Trim(),
                OrderStableId = order.FoodOrderStableId,
                ActorStableId = courier.ActorStableId,
                VehicleStableId = request.VehicleStableId.Trim(),
                RouteStableId = request.RouteStableId.Trim(),
                GraphRevision = graph.Source.SourceVersion,
                GraphHash = graph.Revision.ToUpperInvariant(),
                Revision = request.Revision,
                ObservedAt = request.ObservedAt,
                DistributionApproved = false,
                ReadinessCode = readiness,
                Blocked = blocked,
                CurrentLegIndex = currentLegIndex,
                Legs = legs,
                SourceKindCode = 음식배달여정Policy.SyntheticFixtureSourceKind
            };
            snapshot.RouteFingerprint = 음식배달여정CanonicalFingerprint.경로(snapshot);
            return snapshot;
        }

        private static 음식배달여정Leg[] 상태Leg생성(
            동네공간Snapshot graph, LegRoute[] routes, int currentIndex, double currentProgress, bool blocked)
        {
            var result = new 음식배달여정Leg[routes.Length];
            음식배달여정Point? vehiclePosition = null;
            for (var index = 0; index < routes.Length; index++)
            {
                var item = routes[index];
                var found = item.Route.Found;
                var distance = found ? item.Route.LengthMeters : 0;
                var progress = index < currentIndex ? distance : index == currentIndex ? currentProgress : 0;
                var state = index < currentIndex
                    ? 음식배달여정LegStateCodes.Completed
                    : index > currentIndex
                        ? 음식배달여정LegStateCodes.Pending
                        : blocked
                            ? 음식배달여정LegStateCodes.Blocked
                            : progress == distance
                                ? 음식배달여정LegStateCodes.Completed
                                : 음식배달여정LegStateCodes.Active;
                var points = found
                    ? item.Route.Points.Select(Point).ToArray()
                    : Array.Empty<음식배달여정Point>();
                var actorPosition = found
                    ? 위치(item.Route.Points, progress)
                    : 미해결정지위치(graph, item.Definition.FromNodeStableId);
                if (item.Definition.Mode != 음식배달여정이동수단.Pedestrian)
                    vehiclePosition = Copy(actorPosition);
                result[index] = new 음식배달여정Leg
                {
                    StableId = item.Definition.StableId,
                    Mode = ModeCode(item.Definition.Mode),
                    StateCode = state,
                    DistanceMeters = distance,
                    ProgressMeters = progress,
                    Points = points,
                    ActorPosition = actorPosition,
                    VehiclePosition = vehiclePosition == null ? null : Copy(vehiclePosition)
                };
            }
            return result;
        }

        private static 음식배달여정Point 미해결정지위치(동네공간Snapshot graph, string fromNodeStableId)
        {
            var node = graph.Nodes.FirstOrDefault(value => value.StableId == fromNodeStableId);
            if (node == null) throw new InvalidDataException(음식배달여정ProjectionErrorCodes.RouteUnresolved);
            return Point(node.Position);
        }

        private static (int Index, double ProgressMeters) 현재Leg(
            string stage, double currentDistance, LegRoute[] routes)
        {
            switch (stage)
            {
                case "DriveRestaurant": return (0, currentDistance);
                case "WalkRestaurant": return (1, currentDistance);
                case "WaitFood":
                case "Pickup": return (1, routes[1].Route.LengthMeters);
                case "WalkRestaurantOut": return (2, currentDistance);
                case "DriveHome": return (3, currentDistance);
                case "WalkHome": return (4, currentDistance);
                case "Deliver":
                case "Receive": return (4, routes[4].Route.LengthMeters);
                case "WalkHomeOut": return (5, currentDistance);
                case "Return": return (6, currentDistance);
                default: throw new InvalidDataException(음식배달여정ProjectionErrorCodes.InputInvalid);
            }
        }

        private static LegDefinition[] 여정정의(string destination)
        {
            var stop = destination + "-stop";
            var door = destination + "-door";
            return new[]
            {
                new LegDefinition("leg:synthetic:depot-restaurant-drive", 음식배달여정이동수단.Motorcycle, "depot", "restaurant-stop"),
                new LegDefinition("leg:synthetic:restaurant-enter", 음식배달여정이동수단.Pedestrian, "restaurant-stop", "restaurant-door"),
                new LegDefinition("leg:synthetic:restaurant-exit", 음식배달여정이동수단.Pedestrian, "restaurant-door", "restaurant-stop"),
                new LegDefinition("leg:synthetic:restaurant-destination-drive", 음식배달여정이동수단.Motorcycle, "restaurant-stop", stop),
                new LegDefinition("leg:synthetic:destination-enter", 음식배달여정이동수단.Pedestrian, stop, door),
                new LegDefinition("leg:synthetic:destination-exit", 음식배달여정이동수단.Pedestrian, door, stop),
                new LegDefinition("leg:synthetic:return-depot", 음식배달여정이동수단.Motorcycle, stop, "depot")
            };
        }

        private static string 목적지(string facilityStableId)
        {
            if (facilityStableId.EndsWith(":a", StringComparison.Ordinal)) return "residence-a";
            if (facilityStableId.EndsWith(":b", StringComparison.Ordinal)) return "residence-b";
            throw new InvalidDataException(음식배달여정ProjectionErrorCodes.RouteUnresolved);
        }

        private static 음식배달여정Point 위치(IReadOnlyList<동네평면좌표> points, double distance)
        {
            if (points.Count == 0) throw new InvalidDataException(음식배달여정ProjectionErrorCodes.RouteUnresolved);
            if (distance <= 0) return Point(points[0]);
            var remaining = distance;
            for (var index = 1; index < points.Count; index++)
            {
                var length = points[index - 1].거리(points[index]);
                if (remaining <= length)
                {
                    var ratio = length == 0 ? 1 : remaining / length;
                    return new 음식배달여정Point
                    {
                        X = points[index - 1].X + (points[index].X - points[index - 1].X) * ratio,
                        Z = points[index - 1].Z + (points[index].Z - points[index - 1].Z) * ratio
                    };
                }
                remaining -= length;
            }
            return Point(points[points.Count - 1]);
        }

        private static 음식배달여정Point Point(동네평면좌표 point)
            => new 음식배달여정Point { X = point.X, Z = point.Z };

        private static 음식배달여정Point Copy(음식배달여정Point point)
            => new 음식배달여정Point { X = point.X, Z = point.Z };

        private static bool 같은위치(음식배달여정Point point, double x, double z)
            => Math.Abs(point.X - x) <= 0.000001 && Math.Abs(point.Z - z) <= 0.000001;

        private static bool 합성Id(string value)
            => !string.IsNullOrWhiteSpace(value)
                && value.IndexOf(":synthetic", StringComparison.Ordinal) >= 0;

        private static string ModeCode(음식배달여정이동수단 mode)
        {
            switch (mode)
            {
                case 음식배달여정이동수단.Vehicle: return 음식배달여정ModeCodes.Vehicle;
                case 음식배달여정이동수단.Pedestrian: return 음식배달여정ModeCodes.Pedestrian;
                case 음식배달여정이동수단.Motorcycle: return 음식배달여정ModeCodes.Motorcycle;
                default: throw new InvalidDataException(음식배달여정ProjectionErrorCodes.InputInvalid);
            }
        }

        private static bool 지역Id(string value)
            => !string.IsNullOrWhiteSpace(value) && value.StartsWith("world-region:", StringComparison.Ordinal);

        private static bool 행정동Id(string value)
        {
            const string prefix = "region:kr:hjd:";
            return value != null && value.StartsWith(prefix, StringComparison.Ordinal)
                && value.Length == prefix.Length + 10
                && value.Skip(prefix.Length).All(character => character >= '0' && character <= '9');
        }

        private static bool 유한(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        internal static bool 해시형식(string value)
            => value != null && value.Length == 64 && value.All(character =>
                character >= '0' && character <= '9'
                || character >= 'a' && character <= 'f'
                || character >= 'A' && character <= 'F');

        private static void 요구(bool condition, string code)
        {
            if (!condition) throw new InvalidDataException(code);
        }

        private sealed class LegDefinition
        {
            public string StableId { get; }
            public 음식배달여정이동수단 Mode { get; }
            public string FromNodeStableId { get; }
            public string ToNodeStableId { get; }

            public LegDefinition(string id, 음식배달여정이동수단 mode, string from, string to)
            { StableId = id; Mode = mode; FromNodeStableId = from; ToNodeStableId = to; }
        }

        private sealed class LegRoute
        {
            public LegDefinition Definition { get; }
            public 동네이동경로후보 Route { get; }

            public LegRoute(LegDefinition definition, 동네이동경로후보 route)
            { Definition = definition; Route = route; }

        }
    }

    public sealed class 음식배달여정ValidationResult
    {
        public bool Accepted { get; internal set; }
        public string ErrorCode { get; internal set; } = string.Empty;
    }

    /// <summary>wire 사본을 예상 행정동·graph·시간에 결속하는 순수 검증기.</summary>
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E2,
        "가상 배달 여정 사본을 예상 지역·행정동·이동망 판본·시간·경로 지문에 결속한다.",
        Boundary = "검증 결과는 실제 통행 승인·주문 상태 변경·Unity 실행 증거가 아니다.")]
    public sealed class 음식배달여정SnapshotValidator
    {
        public 음식배달여정ValidationResult 검증(
            음식배달여정Snapshot snapshot,
            string expectedRegionStableId,
            string expectedAdministrativeAreaStableId,
            string expectedGraphRevision,
            string expectedGraphHash,
            DateTimeOffset now,
            TimeSpan maximumAge)
        {
            if (snapshot == null) return 거절(음식배달여정ProjectionErrorCodes.InputInvalid);
            if (snapshot.SchemaVersion != 음식배달여정Policy.SchemaVersion)
                return 거절(음식배달여정ProjectionErrorCodes.SchemaUnsupported);
            if (snapshot.SourceKindCode != 음식배달여정Policy.SyntheticFixtureSourceKind)
                return 거절(음식배달여정ProjectionErrorCodes.SourceKindInvalid);
            if (snapshot.DistributionApproved)
                return 거절(음식배달여정ProjectionErrorCodes.DistributionForbidden);
            if (snapshot.RegionStableId != expectedRegionStableId
                || !지역Id(snapshot.RegionStableId) || !행정동Id(snapshot.AdministrativeAreaStableId)
                || snapshot.AdministrativeAreaStableId != expectedAdministrativeAreaStableId
                || !합성Id(snapshot.OrderStableId) || !합성Id(snapshot.ActorStableId)
                || !합성Id(snapshot.VehicleStableId) || !합성Id(snapshot.RouteStableId)
                || snapshot.Revision <= 0)
                return 거절(음식배달여정ProjectionErrorCodes.SnapshotShapeInvalid);
            if (snapshot.GraphRevision != expectedGraphRevision)
                return 거절(음식배달여정ProjectionErrorCodes.GraphRevisionMismatch);
            if (!음식배달여정SnapshotFactory.해시형식(snapshot.GraphHash)
                || !음식배달여정SnapshotFactory.해시형식(expectedGraphHash)
                || !string.Equals(snapshot.GraphHash, expectedGraphHash, StringComparison.OrdinalIgnoreCase))
                return 거절(음식배달여정ProjectionErrorCodes.GraphHashMismatch);
            if (now.Offset != TimeSpan.Zero || maximumAge <= TimeSpan.Zero
                || snapshot.ObservedAt == default || snapshot.ObservedAt.Offset != TimeSpan.Zero
                || snapshot.ObservedAt > now)
                return 거절(음식배달여정ProjectionErrorCodes.ObservedAtInvalid);
            if (now - snapshot.ObservedAt > maximumAge)
                return 거절(음식배달여정ProjectionErrorCodes.StaleSnapshot);
            if (!형태(snapshot))
                return 거절(음식배달여정ProjectionErrorCodes.SnapshotShapeInvalid);
            if (!음식배달여정SnapshotFactory.해시형식(snapshot.RouteFingerprint)
                || snapshot.RouteFingerprint != 음식배달여정CanonicalFingerprint.경로(snapshot))
                return 거절(음식배달여정ProjectionErrorCodes.RouteFingerprintMismatch);
            return new 음식배달여정ValidationResult { Accepted = true };
        }

        private static bool 형태(음식배달여정Snapshot snapshot)
        {
            if (snapshot.Legs == null || snapshot.Legs.Length == 0
                || snapshot.CurrentLegIndex < 0 || snapshot.CurrentLegIndex >= snapshot.Legs.Length)
                return false;
            var unresolved = snapshot.ReadinessCode == 음식배달여정ReadinessCodes.RouteUnresolved;
            if (!unresolved && snapshot.ReadinessCode != 음식배달여정ReadinessCodes.Ready) return false;
            if (unresolved && !snapshot.Blocked) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < snapshot.Legs.Length; index++)
            {
                var leg = snapshot.Legs[index];
                if (leg == null || !합성Id(leg.StableId) || !ids.Add(leg.StableId)
                    || !음식배달여정ModeCodes.IsSupported(leg.Mode)
                    || !음식배달여정LegStateCodes.IsSupported(leg.StateCode)
                    || !유한(leg.DistanceMeters) || !유한(leg.ProgressMeters)
                    || leg.DistanceMeters < 0 || leg.ProgressMeters < 0 || leg.ProgressMeters > leg.DistanceMeters
                    || leg.Points == null || leg.ActorPosition == null || !유한(leg.ActorPosition)) return false;
                if (index < snapshot.CurrentLegIndex
                    && (leg.StateCode != 음식배달여정LegStateCodes.Completed
                        || leg.ProgressMeters != leg.DistanceMeters)) return false;
                if (index > snapshot.CurrentLegIndex
                    && (leg.StateCode != 음식배달여정LegStateCodes.Pending
                        || leg.ProgressMeters != 0)) return false;
                if (index == snapshot.CurrentLegIndex)
                {
                    if (snapshot.Blocked != (leg.StateCode == 음식배달여정LegStateCodes.Blocked)) return false;
                    if (!snapshot.Blocked && leg.StateCode != 음식배달여정LegStateCodes.Active
                        && leg.StateCode != 음식배달여정LegStateCodes.Completed) return false;
                }
                if (leg.Points.Length == 0)
                {
                    if (!unresolved || leg.DistanceMeters != 0 || leg.ProgressMeters != 0) return false;
                }
                else
                {
                    if (leg.Points.Any(point => point == null || !유한(point))) return false;
                    var length = 0d;
                    for (var point = 1; point < leg.Points.Length; point++)
                    {
                        var dx = leg.Points[point].X - leg.Points[point - 1].X;
                        var dz = leg.Points[point].Z - leg.Points[point - 1].Z;
                        var segment = Math.Sqrt(dx * dx + dz * dz);
                        if (!유한(segment) || segment <= 0) return false;
                        length += segment;
                    }
                    if (Math.Abs(length - leg.DistanceMeters) > 0.000001
                        || !같은위치(leg.ActorPosition, 위치(leg.Points, leg.ProgressMeters))) return false;
                }
                if (leg.VehiclePosition != null && !유한(leg.VehiclePosition)) return false;
                if (leg.Mode != 음식배달여정ModeCodes.Pedestrian
                    && (leg.VehiclePosition == null || !같은위치(leg.ActorPosition, leg.VehiclePosition))) return false;
            }
            return true;
        }

        private static 음식배달여정Point 위치(IReadOnlyList<음식배달여정Point> points, double distance)
        {
            if (distance <= 0) return points[0];
            var remaining = distance;
            for (var index = 1; index < points.Count; index++)
            {
                var from = points[index - 1];
                var to = points[index];
                var dx = to.X - from.X;
                var dz = to.Z - from.Z;
                var length = Math.Sqrt(dx * dx + dz * dz);
                if (remaining <= length)
                {
                    var ratio = remaining / length;
                    return new 음식배달여정Point { X = from.X + dx * ratio, Z = from.Z + dz * ratio };
                }
                remaining -= length;
            }
            return points[points.Count - 1];
        }

        private static bool 같은위치(음식배달여정Point first, 음식배달여정Point second)
            => Math.Abs(first.X - second.X) <= 0.000001 && Math.Abs(first.Z - second.Z) <= 0.000001;

        private static bool 유한(음식배달여정Point point) => 유한(point.X) && 유한(point.Z);
        private static bool 유한(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool 합성Id(string value) => !string.IsNullOrWhiteSpace(value)
            && value.IndexOf(":synthetic", StringComparison.Ordinal) >= 0;
        private static bool 지역Id(string value) => !string.IsNullOrWhiteSpace(value)
            && value.StartsWith("world-region:", StringComparison.Ordinal);
        private static bool 행정동Id(string value)
        {
            const string prefix = "region:kr:hjd:";
            return value != null && value.StartsWith(prefix, StringComparison.Ordinal)
                && value.Length == prefix.Length + 10
                && value.Skip(prefix.Length).All(character => character >= '0' && character <= '9');
        }
        private static 음식배달여정ValidationResult 거절(string code)
            => new 음식배달여정ValidationResult { ErrorCode = code };
    }

    public sealed class 음식배달여정ApplyResult
    {
        public bool Accepted { get; internal set; }
        public bool Changed { get; internal set; }
        public string ErrorCode { get; internal set; } = string.Empty;
        public 음식배달여정Snapshot? Snapshot { get; internal set; }
    }

    /// <summary>
    /// 검증된 최신 여정만 메모리에 보존하는 읽기 전용 상태 투영기.
    /// 이 투영기에는 주문 Command, Repository, 운영 DB 의존성이 없다.
    /// </summary>
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E2,
        "검증된 최신 가상 배달 여정을 revision과 내용 충돌 규칙에 따라 메모리 상태 사본으로 투영한다.",
        Boundary = "메모리 투영은 Save/Replay·운영 원장·Unity Scene·도착 권위를 변경하지 않는다.")]
    public sealed class 음식배달여정상태Projector
    {
        private readonly 음식배달여정SnapshotValidator validator = new 음식배달여정SnapshotValidator();
        private 음식배달여정Snapshot? current;

        public 음식배달여정Snapshot? Current => current == null ? null : Copy(current);

        public 음식배달여정ApplyResult 적용(
            음식배달여정Snapshot incoming,
            string expectedRegionStableId,
            string expectedAdministrativeAreaStableId,
            string expectedGraphRevision,
            string expectedGraphHash,
            DateTimeOffset now,
            TimeSpan maximumAge)
        {
            var validation = validator.검증(incoming, expectedRegionStableId, expectedAdministrativeAreaStableId,
                expectedGraphRevision, expectedGraphHash, now, maximumAge);
            if (!validation.Accepted) return 거절(validation.ErrorCode);
            if (current != null)
            {
                if (current.RegionStableId != incoming.RegionStableId
                    || current.AdministrativeAreaStableId != incoming.AdministrativeAreaStableId
                    || current.OrderStableId != incoming.OrderStableId
                    || current.ActorStableId != incoming.ActorStableId
                    || current.VehicleStableId != incoming.VehicleStableId
                    || current.RouteStableId != incoming.RouteStableId)
                    return 거절(음식배달여정ProjectionErrorCodes.IdentityMismatch);
                if (incoming.Revision < current.Revision)
                    return 거절(음식배달여정ProjectionErrorCodes.StaleRevision);
                if (incoming.Revision == current.Revision)
                {
                    if (음식배달여정CanonicalFingerprint.상태(incoming)
                        != 음식배달여정CanonicalFingerprint.상태(current))
                        return 거절(음식배달여정ProjectionErrorCodes.RevisionConflict);
                    return new 음식배달여정ApplyResult
                    { Accepted = true, Changed = false, Snapshot = Copy(current) };
                }
            }
            current = Copy(incoming);
            return new 음식배달여정ApplyResult
            { Accepted = true, Changed = true, Snapshot = Copy(current) };
        }

        public void Clear() => current = null;

        private 음식배달여정ApplyResult 거절(string code)
            => new 음식배달여정ApplyResult
            { ErrorCode = code, Snapshot = current == null ? null : Copy(current) };

        private static 음식배달여정Snapshot Copy(음식배달여정Snapshot value)
            => new 음식배달여정Snapshot
            {
                SchemaVersion = value.SchemaVersion,
                RegionStableId = value.RegionStableId,
                AdministrativeAreaStableId = value.AdministrativeAreaStableId,
                OrderStableId = value.OrderStableId,
                ActorStableId = value.ActorStableId,
                VehicleStableId = value.VehicleStableId,
                RouteStableId = value.RouteStableId,
                GraphRevision = value.GraphRevision,
                GraphHash = value.GraphHash,
                RouteFingerprint = value.RouteFingerprint,
                Revision = value.Revision,
                ObservedAt = value.ObservedAt,
                DistributionApproved = value.DistributionApproved,
                ReadinessCode = value.ReadinessCode,
                Blocked = value.Blocked,
                CurrentLegIndex = value.CurrentLegIndex,
                SourceKindCode = value.SourceKindCode,
                Legs = (value.Legs ?? Array.Empty<음식배달여정Leg>()).Select(leg => new 음식배달여정Leg
                {
                    StableId = leg.StableId,
                    Mode = leg.Mode,
                    StateCode = leg.StateCode,
                    DistanceMeters = leg.DistanceMeters,
                    ProgressMeters = leg.ProgressMeters,
                    Points = (leg.Points ?? Array.Empty<음식배달여정Point>()).Select(point => new 음식배달여정Point
                    { X = point.X, Z = point.Z }).ToArray(),
                    ActorPosition = new 음식배달여정Point { X = leg.ActorPosition.X, Z = leg.ActorPosition.Z },
                    VehiclePosition = leg.VehiclePosition == null ? null : new 음식배달여정Point
                    { X = leg.VehiclePosition.X, Z = leg.VehiclePosition.Z }
                }).ToArray()
            };
    }

    internal static class 음식배달여정CanonicalFingerprint
    {
        public static string 경로(음식배달여정Snapshot value)
            => Hash(writer =>
            {
                writer.Write(음식배달여정Policy.SchemaVersion);
                writer.Write(value.RouteStableId ?? string.Empty);
                writer.Write(value.GraphRevision ?? string.Empty);
                writer.Write(value.GraphHash ?? string.Empty);
                var legs = value.Legs ?? Array.Empty<음식배달여정Leg>();
                writer.Write(legs.Length);
                foreach (var leg in legs)
                {
                    writer.Write(leg.StableId ?? string.Empty);
                    writer.Write(leg.Mode ?? string.Empty);
                    writer.Write(leg.DistanceMeters);
                    var points = leg.Points ?? Array.Empty<음식배달여정Point>();
                    writer.Write(points.Length);
                    foreach (var point in points) { writer.Write(point.X); writer.Write(point.Z); }
                }
            });

        public static string 상태(음식배달여정Snapshot value)
            => Hash(writer =>
            {
                writer.Write(value.SchemaVersion ?? string.Empty);
                writer.Write(value.RegionStableId ?? string.Empty);
                writer.Write(value.AdministrativeAreaStableId ?? string.Empty);
                writer.Write(value.OrderStableId ?? string.Empty);
                writer.Write(value.ActorStableId ?? string.Empty);
                writer.Write(value.VehicleStableId ?? string.Empty);
                writer.Write(value.RouteStableId ?? string.Empty);
                writer.Write(value.GraphRevision ?? string.Empty);
                writer.Write(value.GraphHash ?? string.Empty);
                writer.Write(value.RouteFingerprint ?? string.Empty);
                writer.Write(value.Revision);
                writer.Write(value.ObservedAt.UtcDateTime.Ticks);
                writer.Write(value.DistributionApproved);
                writer.Write(value.ReadinessCode ?? string.Empty);
                writer.Write(value.Blocked);
                writer.Write(value.CurrentLegIndex);
                foreach (var leg in value.Legs ?? Array.Empty<음식배달여정Leg>())
                {
                    writer.Write(leg.StableId ?? string.Empty);
                    writer.Write(leg.Mode ?? string.Empty);
                    writer.Write(leg.StateCode ?? string.Empty);
                    writer.Write(leg.DistanceMeters);
                    writer.Write(leg.ProgressMeters);
                    writer.Write(leg.ActorPosition.X); writer.Write(leg.ActorPosition.Z);
                    writer.Write(leg.VehiclePosition != null);
                    if (leg.VehiclePosition != null)
                    { writer.Write(leg.VehiclePosition.X); writer.Write(leg.VehiclePosition.Z); }
                }
            });

        private static string Hash(Action<BinaryWriter> write)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) write(writer);
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-", string.Empty);
        }
    }
}
