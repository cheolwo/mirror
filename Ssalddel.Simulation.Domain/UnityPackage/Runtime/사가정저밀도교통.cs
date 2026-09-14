using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Ssalddel.Simulation.Contracts;
using Ssalddel.WorkflowRules;

namespace Ssalddel.Simulation.Domain
{
    public sealed class 사가정저밀도교통Profile
    {
        public string Revision { get; set; } = string.Empty;
        public string SignalPlanRevision { get; set; } = string.Empty;
        public double TickSeconds { get; set; }
        public double NorthSouthGreenSeconds { get; set; }
        public double AmberSeconds { get; set; }
        public double AllRedSeconds { get; set; }
        public double EastWestGreenSeconds { get; set; }
        public double MainRoadSpeedMetersPerSecond { get; set; }
        public double NeighborhoodRoadSpeedMetersPerSecond { get; set; }
        public double AlleySpeedMetersPerSecond { get; set; }
        public double StopLineBufferMeters { get; set; }
        public double MinimumGapMeters { get; set; }

        public double CycleSeconds => NorthSouthGreenSeconds + AmberSeconds
                                      + AllRedSeconds + EastWestGreenSeconds
                                      + AmberSeconds + AllRedSeconds;
    }

    public sealed class 사가정저밀도교통LaneDefinition
    {
        public string StableId { get; set; } = string.Empty;
        public string SourceWayStableId { get; set; } = string.Empty;
        public string AxisCode { get; set; } = string.Empty;
        public string RouteKindCode { get; set; } = string.Empty;
        public string SourceDirectionCode { get; set; } =
            사가정저밀도교통SourceAuthorityCodes.UnknownDirection;
        public string SourceAccessReviewCode { get; set; } =
            사가정저밀도교통SourceAuthorityCodes.PendingHumanReview;
        public bool SourceRuntimeAuthorized { get; set; }
        public double? StopLineProgressMeters { get; set; }
        public bool PingPong { get; set; }
        public 사가정저밀도교통Point[] Points { get; set; } =
            Array.Empty<사가정저밀도교통Point>();

        public double LengthMeters
        {
            get
            {
                var total = 0d;
                for (var index = 1; index < Points.Length; index++)
                {
                    var dx = Points[index].X - Points[index - 1].X;
                    var dz = Points[index].Z - Points[index - 1].Z;
                    total += Math.Sqrt(dx * dx + dz * dz);
                }
                return total;
            }
        }
    }

    public sealed class 사가정저밀도교통ActorSeed
    {
        public string StableId { get; set; } = string.Empty;
        public string KindCode { get; set; } = string.Empty;
        public string LaneStableId { get; set; } = string.Empty;
        public double InitialProgressMeters { get; set; }
    }

    public sealed class 사가정저밀도교통CourierRouteLegDefinition
    {
        public string StableId { get; set; } = string.Empty;
        public string LaneStableId { get; set; } = string.Empty;
        public string StageCode { get; set; } = string.Empty;
        public 사가정저밀도교통SourceEdgeDefinition[] SourceEdges { get; set; } =
            Array.Empty<사가정저밀도교통SourceEdgeDefinition>();
    }

    public sealed class 사가정저밀도교통SourceEdgeDefinition
    {
        public string EdgeStableId { get; set; } = string.Empty;
        public string DisplayGeometryOrderCode { get; set; } =
            사가정저밀도교통SourceGeometryOrderCodes.AsStored;
        public string SourceDirectionCode { get; set; } =
            사가정저밀도교통SourceAuthorityCodes.UnknownDirection;
        public string AccessReviewCode { get; set; } =
            사가정저밀도교통SourceAuthorityCodes.PendingHumanReview;
        public bool RuntimeAuthorized { get; set; }
        public double SourceDistanceMeters { get; set; }
        public 사가정저밀도교통Point DisplayFrom { get; set; } =
            new 사가정저밀도교통Point();
        public 사가정저밀도교통Point DisplayTo { get; set; } =
            new 사가정저밀도교통Point();
    }

    public sealed class 사가정저밀도교통CourierRouteDefinition
    {
        public string Revision { get; set; } = string.Empty;
        public string Fingerprint { get; set; } = string.Empty;
        public string SourceMobilityGraphStableId { get; set; } = string.Empty;
        public string SourceMobilityGraphRevision { get; set; } = string.Empty;
        public string SourceMobilityGraphProjectionHashSha256 { get; set; } = string.Empty;
        public string SourceMobilityGraphContentHashSha256 { get; set; } = string.Empty;
        public string SourceAccessReviewCode { get; set; } =
            사가정저밀도교통SourceAuthorityCodes.PendingHumanReview;
        public string SourceDirectionAuthorityCode { get; set; } =
            사가정저밀도교통SourceAuthorityCodes.UnknownDirection;
        public bool SourceRuntimeAuthorized { get; set; }
        public 사가정저밀도교통CourierRouteLegDefinition[] Legs { get; set; } =
            Array.Empty<사가정저밀도교통CourierRouteLegDefinition>();
    }

    public sealed class 사가정저밀도교통Input
    {
        public string SourceObservationRevision { get; set; } = string.Empty;
        public string LaneGraphRevision { get; set; } = string.Empty;
        public string LaneGraphHash { get; set; } = string.Empty;
        public string CourierJourneyId { get; set; } = string.Empty;
        public string RouteFingerprint { get; set; } = string.Empty;
        public string ConfigurationFingerprint { get; set; } = string.Empty;
        public string InputStateFingerprint { get; set; } = string.Empty;
        public int ScenarioSeed { get; set; }
        public DateTimeOffset EpochUtc { get; set; }
        public 사가정저밀도교통Profile Profile { get; set; } =
            new 사가정저밀도교통Profile();
        public 사가정저밀도교통LaneDefinition[] Lanes { get; set; } =
            Array.Empty<사가정저밀도교통LaneDefinition>();
        public 사가정저밀도교통ActorSeed[] Actors { get; set; } =
            Array.Empty<사가정저밀도교통ActorSeed>();
        public 사가정저밀도교통CourierRouteDefinition CourierDisplayRoute { get; set; } =
            new 사가정저밀도교통CourierRouteDefinition();
        public IReadOnlyDictionary<string, string> OccupiedAlleyResources { get; set; } =
            new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// 승인 연구에 기록된 값과 OSM 관찰 중심선을 명시적으로 조립합니다.
    /// 실제 차로·통행·신호 운영을 확정하는 factory가 아닙니다.
    /// </summary>
    public static class 사가정저밀도교통검증Fixture
    {
        public const string SourceObservationRevision =
            "sagajeong-reference.r3+oa-23081-20260824";
        public const string LaneGraphRevision =
            "sagajeong-low-density-lane-display.r2";
        public const string ProfileRevision = "sagajeong-low-density-traffic.r2";
        public const string SignalPlanRevision = "sagajeong-synthetic-signal-cycle.r1";
        public const string CourierDisplayRouteRevision =
            "sagajeong-courier-display-route.osm-candidate.r2";
        public const string CourierDisplayRouteFingerprint =
            "EAB858B640F90E4EF2E1832B9D3C3D22B096B2F202D8A3AC04D03FEAB8DC7475";
        public const string SourceMobilityGraphStableId =
            "mobility-graph:kr:seoul:jungnang:sagajeong.r1";
        public const string SourceMobilityGraphRevision =
            "sagajeong-mobility-graph.osm-candidate.r1";
        public const string SourceMobilityGraphProjectionHashSha256 =
            "59DD155C0D49BEB0612062DF6E1571A751FE0EC1E9DA4AD4AF41A879EB4FE6F7";
        public const string SourceMobilityGraphContentHashSha256 =
            "CD3E8FBD6428F7499F9E313E1AD3CFC04688EFAEF370E6C109F2321D70B11768";
        public const string CourierJourneyId =
            "route:synthetic:sagajeong-food-delivery:a";
        public const string CourierRouteFingerprint =
            "720664785E1CE71ECB7D492ED5903514FA5D1A6237D91896187C99385146B88A";
        public const int ScenarioSeed = 20260913;
        public const string EastWestLane = "lane:synthetic-display:sagajeong:ew-eastbound.r1";
        public const string NorthSouthLane = "lane:synthetic-display:sagajeong:ns-northbound.r1";
        public const string NeighborhoodAccessLane =
            "lane:manual-visual-candidate:sagajeong:delivery-access.r2";
        public const string AlleyLane = "lane:manual-visual-candidate:osm:way:1256772587.r1";
        public const string AlleyResource = "movement-resource:alley:osm:way:1256772587";

        public static 사가정저밀도교통Input 만들기(
            IReadOnlyDictionary<string, string>? occupiedAlleyResources = null)
        {
            var profile = new 사가정저밀도교통Profile
            {
                Revision = ProfileRevision,
                SignalPlanRevision = SignalPlanRevision,
                TickSeconds = .25d,
                NorthSouthGreenSeconds = 10d,
                AmberSeconds = 2d,
                AllRedSeconds = 2d,
                EastWestGreenSeconds = 10d,
                MainRoadSpeedMetersPerSecond = 6d,
                NeighborhoodRoadSpeedMetersPerSecond = 4d,
                AlleySpeedMetersPerSecond = 2.5d,
                StopLineBufferMeters = 1.5d,
                MinimumGapMeters = 6d
            };
            var lanes = new[]
            {
                Lane(EastWestLane, "osm:way:218790209",
                    사가정저밀도교통AxisCodes.EastWest,
                    사가정저밀도교통RouteKindCodes.SyntheticMainLaneDisplayOnly, 52.59d, false,
                    P(502.470, 10.708), P(530.294, 6.912), P(554.576, 3.594),
                    P(576.941, -.524), P(582.841, -1.900), P(608.696, -7.927)),
                Lane(NorthSouthLane, "osm:way:218798305",
                    사가정저밀도교통AxisCodes.NorthSouth,
                    사가정저밀도교통RouteKindCodes.SyntheticMainLaneDisplayOnly, 37.25d, false,
                    P(545.954, -32.644), P(554.576, 3.594),
                    P(557.234, 24.915), P(560.706, 52.762)),
                Lane(NeighborhoodAccessLane,
                    "osm:way-chain:1112326984+1112326970+576294786",
                    사가정저밀도교통AxisCodes.NeighborhoodUncontrolled,
                    사가정저밀도교통RouteKindCodes.ManualVisualReviewCandidate,
                    null, false,
                    P(608.696, -7.927), P(648.815, -17.061), P(652.384, .342),
                    P(654.168, 8.000), P(665.147, 55.115), P(668.194, 86.381)),
                Lane(AlleyLane, "osm:way:1256772587",
                    사가정저밀도교통AxisCodes.AlleyUncontrolled,
                    사가정저밀도교통RouteKindCodes.ManualVisualReviewCandidate, null, false,
                    P(668.194, 86.381), P(646.429, 89.222),
                    P(645.961, 89.211), P(645.625, 89.089))
            };
            var courierRoute = new 사가정저밀도교통CourierRouteDefinition
            {
                Revision = CourierDisplayRouteRevision,
                SourceMobilityGraphStableId = SourceMobilityGraphStableId,
                SourceMobilityGraphRevision = SourceMobilityGraphRevision,
                SourceMobilityGraphProjectionHashSha256 =
                    SourceMobilityGraphProjectionHashSha256,
                SourceMobilityGraphContentHashSha256 = SourceMobilityGraphContentHashSha256,
                SourceAccessReviewCode =
                    사가정저밀도교통SourceAuthorityCodes.PendingHumanReview,
                SourceDirectionAuthorityCode =
                    사가정저밀도교통SourceAuthorityCodes.UnknownDirection,
                SourceRuntimeAuthorized = false,
                Legs = new[]
                {
                    RouteLeg("route-leg:synthetic:sagajeong:main-road.r2", EastWestLane,
                        사가정저밀도교통JourneyStageCodes.MainRoadApproach,
                        SourceEdge("mobility-tile:sagajeong:x0:z1.r1:osm-way:218790209:segment:21:part:0", 20.033d, 502.470d, 10.708d, 522.319d, 8.000d),
                        SourceEdge("mobility-tile:sagajeong:x0:z0.r1:osm-way:218790209:segment:21:part:1", 8.049d, 522.319d, 8.000d, 530.294d, 6.912d),
                        SourceEdge("mobility-tile:sagajeong:x0:z0.r1:osm-way:218790209:segment:22:part:0", 19.889d, 530.294d, 6.912d, 550.000d, 4.219d),
                        SourceEdge("mobility-tile:sagajeong:x1:z0.r1:osm-way:218790209:segment:22:part:1", 4.618d, 550.000d, 4.219d, 554.576d, 3.594d),
                        SourceEdge("mobility-tile:sagajeong:x1:z0.r1:osm-way:218790209:segment:23:part:0", 22.741d, 554.576d, 3.594d, 576.941d, -.524d),
                        SourceEdge("mobility-tile:sagajeong:x1:z0.r1:osm-way:218790209:segment:24:part:0", 6.058d, 576.941d, -.524d, 582.841d, -1.900d),
                        SourceEdge("mobility-tile:sagajeong:x1:z0.r1:osm-way:218790209:segment:25:part:0", 26.548d, 582.841d, -1.900d, 608.696d, -7.927d)),
                    RouteLeg("route-leg:synthetic:sagajeong:neighborhood-road.r2",
                        NeighborhoodAccessLane,
                        사가정저밀도교통JourneyStageCodes.NeighborhoodRoadApproach,
                        SourceEdge("mobility-tile:sagajeong:x1:z0.r1:osm-way:1112326984:segment:0:part:0", 41.146d, 608.696d, -7.927d, 648.815d, -17.061d),
                        SourceEdge("mobility-tile:sagajeong:x1:z0.r1:osm-way:1112326970:segment:1:part:0", 17.765d, 648.815d, -17.061d, 652.384d, .342d, true),
                        SourceEdge("mobility-tile:sagajeong:x1:z0.r1:osm-way:1112326970:segment:0:part:1", 7.863d, 652.384d, .342d, 654.168d, 8.000d, true),
                        SourceEdge("mobility-tile:sagajeong:x1:z1.r1:osm-way:1112326970:segment:0:part:0", 48.377d, 654.168d, 8.000d, 665.147d, 55.115d, true),
                        SourceEdge("mobility-tile:sagajeong:x1:z1.r1:osm-way:576294786:segment:0:part:0", 31.414d, 665.147d, 55.115d, 668.194d, 86.381d)),
                    RouteLeg("route-leg:synthetic:sagajeong:alley-final.r2", AlleyLane,
                        사가정저밀도교통JourneyStageCodes.AlleyFinalApproach,
                        SourceEdge("mobility-tile:sagajeong:x1:z1.r1:osm-way:1256772587:segment:0:part:0", 21.950d, 668.194d, 86.381d, 646.429d, 89.222d),
                        SourceEdge("mobility-tile:sagajeong:x1:z1.r1:osm-way:1256772587:segment:1:part:0", .468d, 646.429d, 89.222d, 645.961d, 89.211d),
                        SourceEdge("mobility-tile:sagajeong:x1:z1.r1:osm-way:1256772587:segment:2:part:0", .357d, 645.961d, 89.211d, 645.625d, 89.089d))
                }
            };
            var actors = new[]
            {
                Actor("actor:synthetic-courier:sagajeong:1",
                    사가정저밀도교통ActorKindCodes.CourierMotorcycle, EastWestLane, 0d),
                Actor("actor:synthetic-ambient-car:sagajeong:1",
                    사가정저밀도교통ActorKindCodes.AmbientPassengerCar, EastWestLane, 40d),
                Actor("actor:synthetic-ambient-car:sagajeong:2",
                    사가정저밀도교통ActorKindCodes.AmbientPassengerCar, EastWestLane, 30d),
                Actor("actor:synthetic-ambient-car:sagajeong:3",
                    사가정저밀도교통ActorKindCodes.AmbientPassengerCar, NorthSouthLane, 5d)
            };
            var input = new 사가정저밀도교통Input
            {
                SourceObservationRevision = SourceObservationRevision,
                LaneGraphRevision = LaneGraphRevision,
                CourierJourneyId = CourierJourneyId,
                RouteFingerprint = CourierRouteFingerprint,
                ScenarioSeed = ScenarioSeed,
                EpochUtc = new DateTimeOffset(2026, 9, 13, 0, 0, 0, TimeSpan.Zero),
                Profile = profile,
                Lanes = lanes,
                Actors = actors,
                CourierDisplayRoute = courierRoute,
                OccupiedAlleyResources = occupiedAlleyResources
                    ?? new Dictionary<string, string>(StringComparer.Ordinal)
            };
            input.LaneGraphHash = 사가정저밀도교통Fingerprint.LaneGraph(lanes);
            courierRoute.Fingerprint = 사가정저밀도교통Fingerprint.CourierDisplayRoute(
                courierRoute, input.LaneGraphHash, lanes);
            input.ConfigurationFingerprint = 사가정저밀도교통Fingerprint.Configuration(input);
            input.InputStateFingerprint = 사가정저밀도교통Fingerprint.InputState(input);
            return input;
        }

        private static 사가정저밀도교통LaneDefinition Lane(
            string id, string source, string axis, string kind, double? stop,
            bool pingPong, params 사가정저밀도교통Point[] points)
            => new 사가정저밀도교통LaneDefinition
            {
                StableId = id,
                SourceWayStableId = source,
                AxisCode = axis,
                RouteKindCode = kind,
                SourceDirectionCode =
                    사가정저밀도교통SourceAuthorityCodes.UnknownDirection,
                SourceAccessReviewCode =
                    사가정저밀도교통SourceAuthorityCodes.PendingHumanReview,
                SourceRuntimeAuthorized = false,
                StopLineProgressMeters = stop,
                PingPong = pingPong,
                Points = points
            };

        private static 사가정저밀도교통ActorSeed Actor(
            string id, string kind, string lane, double progress)
            => new 사가정저밀도교통ActorSeed
            {
                StableId = id,
                KindCode = kind,
                LaneStableId = lane,
                InitialProgressMeters = progress
            };

        private static 사가정저밀도교통CourierRouteLegDefinition RouteLeg(
            string id, string lane, string stage,
            params 사가정저밀도교통SourceEdgeDefinition[] sourceEdges)
            => new 사가정저밀도교통CourierRouteLegDefinition
            {
                StableId = id,
                LaneStableId = lane,
                StageCode = stage,
                SourceEdges = sourceEdges
            };

        private static 사가정저밀도교통SourceEdgeDefinition SourceEdge(
            string stableId, double sourceDistanceMeters,
            double fromX, double fromZ, double toX, double toZ, bool reverseStored = false)
            => new 사가정저밀도교통SourceEdgeDefinition
            {
                EdgeStableId = stableId,
                DisplayGeometryOrderCode = reverseStored
                    ? 사가정저밀도교통SourceGeometryOrderCodes.ReverseStored
                    : 사가정저밀도교통SourceGeometryOrderCodes.AsStored,
                SourceDirectionCode =
                    사가정저밀도교통SourceAuthorityCodes.UnknownDirection,
                AccessReviewCode =
                    사가정저밀도교통SourceAuthorityCodes.PendingHumanReview,
                RuntimeAuthorized = false,
                SourceDistanceMeters = sourceDistanceMeters,
                DisplayFrom = P(fromX, fromZ),
                DisplayTo = P(toX, toZ)
            };

        private static 사가정저밀도교통Point P(double x, double z)
            => new 사가정저밀도교통Point { X = x, Z = z };
    }

    public static class 사가정저밀도교통Fingerprint
    {
        public static string LaneGraph(IEnumerable<사가정저밀도교통LaneDefinition> lanes)
        {
            if (lanes == null) throw new ArgumentNullException(nameof(lanes));
            var ordered = lanes.OrderBy(value => value?.StableId, StringComparer.Ordinal)
                .ToArray();
            var builder = new StringBuilder();
            Token(builder, "sagajeong-lane-graph.v3");
            Token(builder, ordered.Length.ToString(CultureInfo.InvariantCulture));
            foreach (var lane in ordered)
            {
                if (lane == null) throw new ArgumentException("TrafficLaneMissing", nameof(lanes));
                Token(builder, "lane");
                Token(builder, lane.StableId);
                Token(builder, lane.SourceWayStableId);
                Token(builder, lane.AxisCode);
                Token(builder, lane.RouteKindCode);
                Token(builder, lane.SourceDirectionCode);
                Token(builder, lane.SourceAccessReviewCode);
                Token(builder, lane.SourceRuntimeAuthorized ? "1" : "0");
                Token(builder, Number(lane.StopLineProgressMeters ?? -1d));
                Token(builder, lane.PingPong ? "1" : "0");
                if (lane.Points == null)
                    throw new ArgumentException("TrafficLanePointsMissing", nameof(lanes));
                Token(builder, lane.Points.Length.ToString(CultureInfo.InvariantCulture));
                foreach (var point in lane.Points)
                {
                    if (point == null)
                        throw new ArgumentException("TrafficLanePointMissing", nameof(lanes));
                    Token(builder, Number(point.X));
                    Token(builder, Number(point.Z));
                }
            }
            return Hash(builder.ToString());
        }

        public static string CourierDisplayRoute(
            사가정저밀도교통CourierRouteDefinition route,
            string laneGraphHash,
            IEnumerable<사가정저밀도교통LaneDefinition> lanes)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            if (lanes == null) throw new ArgumentNullException(nameof(lanes));
            var byId = lanes.ToDictionary(value => value?.StableId
                ?? throw new ArgumentException("TrafficLaneMissing", nameof(lanes)),
                StringComparer.Ordinal);
            var legs = route.Legs
                       ?? throw new ArgumentException("CourierRouteLegsMissing", nameof(route));
            var builder = new StringBuilder();
            Token(builder, "sagajeong-courier-display-route.v2");
            Token(builder, route.Revision);
            Token(builder, laneGraphHash);
            Token(builder, route.SourceMobilityGraphStableId);
            Token(builder, route.SourceMobilityGraphRevision);
            Token(builder, route.SourceMobilityGraphProjectionHashSha256);
            Token(builder, route.SourceMobilityGraphContentHashSha256);
            Token(builder, route.SourceAccessReviewCode);
            Token(builder, route.SourceDirectionAuthorityCode);
            Token(builder, route.SourceRuntimeAuthorized ? "1" : "0");
            Token(builder, legs.Length.ToString(CultureInfo.InvariantCulture));
            foreach (var leg in legs)
            {
                if (leg == null || !byId.TryGetValue(leg.LaneStableId, out var lane))
                    throw new ArgumentException("CourierRouteLegInvalid", nameof(route));
                Token(builder, leg.StableId);
                Token(builder, leg.LaneStableId);
                Token(builder, leg.StageCode);
                Token(builder, Number(lane.LengthMeters));
                var sourceEdges = leg.SourceEdges
                                  ?? throw new ArgumentException(
                                      "CourierRouteSourceEdgesMissing", nameof(route));
                Token(builder, sourceEdges.Length.ToString(CultureInfo.InvariantCulture));
                foreach (var edge in sourceEdges)
                {
                    if (edge == null)
                        throw new ArgumentException("CourierRouteSourceEdgeMissing",
                            nameof(route));
                    if (edge.DisplayFrom == null || edge.DisplayTo == null)
                        throw new ArgumentException("CourierRouteSourceEdgePointMissing",
                            nameof(route));
                    Token(builder, edge.EdgeStableId);
                    Token(builder, edge.DisplayGeometryOrderCode);
                    Token(builder, edge.SourceDirectionCode);
                    Token(builder, edge.AccessReviewCode);
                    Token(builder, edge.RuntimeAuthorized ? "1" : "0");
                    Token(builder, Number(edge.SourceDistanceMeters));
                    Token(builder, Number(edge.DisplayFrom.X));
                    Token(builder, Number(edge.DisplayFrom.Z));
                    Token(builder, Number(edge.DisplayTo.X));
                    Token(builder, Number(edge.DisplayTo.Z));
                }
            }
            return Hash(builder.ToString());
        }

        public static string Configuration(사가정저밀도교통Input input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var profile = input.Profile
                          ?? throw new ArgumentException("TrafficProfileMissing", nameof(input));
            var actors = input.Actors
                         ?? throw new ArgumentException("TrafficActorsMissing", nameof(input));
            var builder = new StringBuilder();
            Token(builder, "sagajeong-traffic-configuration.v4");
            Token(builder, input.SourceObservationRevision);
            Token(builder, input.LaneGraphRevision);
            Token(builder, input.LaneGraphHash);
            Token(builder, input.CourierJourneyId);
            Token(builder, input.RouteFingerprint);
            Token(builder, input.CourierDisplayRoute?.Revision);
            Token(builder, input.CourierDisplayRoute?.Fingerprint);
            Token(builder, input.ScenarioSeed.ToString(CultureInfo.InvariantCulture));
            Token(builder, profile.Revision);
            Token(builder, profile.SignalPlanRevision);
            Token(builder, Number(profile.TickSeconds));
            Token(builder, Number(profile.NorthSouthGreenSeconds));
            Token(builder, Number(profile.AmberSeconds));
            Token(builder, Number(profile.AllRedSeconds));
            Token(builder, Number(profile.EastWestGreenSeconds));
            Token(builder, Number(profile.MainRoadSpeedMetersPerSecond));
            Token(builder, Number(profile.NeighborhoodRoadSpeedMetersPerSecond));
            Token(builder, Number(profile.AlleySpeedMetersPerSecond));
            Token(builder, Number(profile.StopLineBufferMeters));
            Token(builder, Number(profile.MinimumGapMeters));
            var ordered = actors.OrderBy(value => value?.StableId, StringComparer.Ordinal).ToArray();
            Token(builder, ordered.Length.ToString(CultureInfo.InvariantCulture));
            foreach (var actor in ordered)
            {
                if (actor == null)
                    throw new ArgumentException("TrafficActorMissing", nameof(input));
                Token(builder, actor.StableId);
                Token(builder, actor.KindCode);
                Token(builder, actor.LaneStableId);
                Token(builder, Number(actor.InitialProgressMeters));
            }
            return Hash(builder.ToString());
        }

        public static string InputState(사가정저밀도교통Input input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var occupied = input.OccupiedAlleyResources
                           ?? throw new ArgumentException("AlleyOccupancyMissing", nameof(input));
            var builder = new StringBuilder();
            Token(builder, "sagajeong-traffic-input-state.v1");
            Token(builder, input.ConfigurationFingerprint);
            Token(builder, input.EpochUtc.UtcTicks.ToString(CultureInfo.InvariantCulture));
            Token(builder, input.EpochUtc.Offset.Ticks.ToString(CultureInfo.InvariantCulture));
            var ordered = occupied.OrderBy(value => value.Key, StringComparer.Ordinal).ToArray();
            Token(builder, ordered.Length.ToString(CultureInfo.InvariantCulture));
            foreach (var pair in ordered)
            {
                Token(builder, pair.Key);
                Token(builder, pair.Value);
            }
            return Hash(builder.ToString());
        }

        public static bool IsSha256(string value)
            => value != null && value.Length == 64
               && value.All(character => character >= '0' && character <= '9'
                                         || character >= 'A' && character <= 'F');

        private static string Number(double value)
            => value.ToString("R", CultureInfo.InvariantCulture);

        private static void Token(StringBuilder builder, string? value)
        {
            value ??= string.Empty;
            builder.Append(Encoding.UTF8.GetByteCount(value).ToString(CultureInfo.InvariantCulture))
                .Append(':').Append(value);
        }

        private static string Hash(string value)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value)))
                .Replace("-", string.Empty);
        }
    }

    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "승인된 저밀도 profile에서 합성 신호·차량 위치·대기열을 결정적으로 계산한다.",
        Boundary = "SyntheticFixture 순수 계산이며 실제 차로·신호 운영·주문 효과·Unity 화면 증거가 아니다.")]
    public sealed class 사가정저밀도교통Engine
    {
        public const long MaximumSimulationTick = 100000L;

        public 사가정저밀도교통Snapshot 생성(
            사가정저밀도교통Input input, long simulationTick)
        {
            검증(input, simulationTick);
            var lanes = input.Lanes.ToDictionary(value => value.StableId,
                StringComparer.Ordinal);
            var states = input.Actors.Select(value => new ActorState(value,
                value.KindCode == 사가정저밀도교통ActorKindCodes.CourierMotorcycle
                    ? input.CourierDisplayRoute : null)).ToArray();
            var alleyOwners = input.OccupiedAlleyResources.ToDictionary(
                pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            for (var step = 0L; step < simulationTick; step++)
                Advance(states, lanes, alleyOwners, input.Profile, step);

            var time = simulationTick * input.Profile.TickSeconds;
            var eastWest = Signal(input.Profile, 사가정저밀도교통AxisCodes.EastWest, time);
            var northSouth = Signal(input.Profile, 사가정저밀도교통AxisCodes.NorthSouth, time);
            AssignQueueIndexes(states);
            return new 사가정저밀도교통Snapshot
            {
                SourceObservationRevision = input.SourceObservationRevision,
                SourceMobilityGraphStableId =
                    input.CourierDisplayRoute.SourceMobilityGraphStableId,
                SourceMobilityGraphRevision =
                    input.CourierDisplayRoute.SourceMobilityGraphRevision,
                SourceMobilityGraphProjectionHashSha256 =
                    input.CourierDisplayRoute.SourceMobilityGraphProjectionHashSha256,
                SourceMobilityGraphContentHashSha256 =
                    input.CourierDisplayRoute.SourceMobilityGraphContentHashSha256,
                SourceAccessReviewCode = input.CourierDisplayRoute.SourceAccessReviewCode,
                SourceDirectionAuthorityCode =
                    input.CourierDisplayRoute.SourceDirectionAuthorityCode,
                SourceRuntimeAuthorized = input.CourierDisplayRoute.SourceRuntimeAuthorized,
                LaneGraphRevision = input.LaneGraphRevision,
                LaneGraphHash = input.LaneGraphHash,
                SignalPlanRevision = input.Profile.SignalPlanRevision,
                TrafficProfileRevision = input.Profile.Revision,
                CourierJourneyId = input.CourierJourneyId,
                RouteFingerprint = input.RouteFingerprint,
                CourierDisplayRouteRevision = input.CourierDisplayRoute.Revision,
                CourierDisplayRouteFingerprint = input.CourierDisplayRoute.Fingerprint,
                ConfigurationFingerprint = input.ConfigurationFingerprint,
                InputStateFingerprint = input.InputStateFingerprint,
                ScenarioSeed = input.ScenarioSeed,
                SimulationTick = simulationTick,
                TickSeconds = input.Profile.TickSeconds,
                Revision = simulationTick + 1L,
                ObservedAt = input.EpochUtc.AddSeconds(time),
                ObservationPresentationOnly = true,
                DistributionApproved = false,
                TraversalReady = false,
                GameplayReady = false,
                JourneyBindingStateCode =
                    사가정저밀도교통SourceAuthorityCodes.DeclaredSyntheticReferenceOnly,
                OperationalBindingReady = false,
                CanonicalDeliveryMutationAllowed = false,
                Signals = new[]
                {
                    ToSignal("signal-group:synthetic:sagajeong:ew.r1",
                        사가정저밀도교통AxisCodes.EastWest, eastWest,
                        "stop-line:synthetic:sagajeong:ew.r1"),
                    ToSignal("signal-group:synthetic:sagajeong:ns.r1",
                        사가정저밀도교통AxisCodes.NorthSouth, northSouth,
                        "stop-line:synthetic:sagajeong:ns.r1")
                },
                Lanes = input.Lanes.OrderBy(value => value.StableId, StringComparer.Ordinal)
                    .Select(value => new 사가정저밀도교통LaneSnapshot
                    {
                        StableId = value.StableId,
                        SourceWayStableId = value.SourceWayStableId,
                        AxisCode = value.AxisCode,
                        RouteKindCode = value.RouteKindCode,
                        SourceDirectionCode = value.SourceDirectionCode,
                        SourceAccessReviewCode = value.SourceAccessReviewCode,
                        SourceRuntimeAuthorized = value.SourceRuntimeAuthorized,
                        HasStopLine = value.StopLineProgressMeters.HasValue,
                        StopLineProgressMeters = value.StopLineProgressMeters ?? 0d,
                        PingPong = value.PingPong,
                        Points = value.Points.Select(point => new 사가정저밀도교통Point
                        {
                            X = point.X,
                            Z = point.Z
                        }).ToArray()
                    }).ToArray(),
                Actors = states.Where(value => !value.ExitedObservation)
                    .OrderBy(value => value.Seed.StableId, StringComparer.Ordinal)
                    .Select(value => ToActor(value, lanes[value.CurrentLaneStableId], input.Profile))
                    .ToArray(),
                CourierRouteLegs = input.CourierDisplayRoute.Legs
                    .Select((value, index) => new 사가정저밀도교통CourierRouteLegSnapshot
                    {
                        StableId = value.StableId,
                        Index = index,
                        LaneStableId = value.LaneStableId,
                        StageCode = value.StageCode,
                        DisplayGeometryDistanceMeters =
                            lanes[value.LaneStableId].LengthMeters,
                        SourceDistanceMeters = value.SourceEdges.Sum(edge =>
                            edge.SourceDistanceMeters),
                        SourceEdges = value.SourceEdges.Select(edge =>
                            new 사가정저밀도교통SourceEdgeSnapshot
                            {
                                EdgeStableId = edge.EdgeStableId,
                                DisplayGeometryOrderCode = edge.DisplayGeometryOrderCode,
                                SourceDirectionCode = edge.SourceDirectionCode,
                                AccessReviewCode = edge.AccessReviewCode,
                                RuntimeAuthorized = edge.RuntimeAuthorized,
                                SourceDistanceMeters = edge.SourceDistanceMeters,
                                DisplayFrom = new 사가정저밀도교통Point
                                {
                                    X = edge.DisplayFrom.X,
                                    Z = edge.DisplayFrom.Z
                                },
                                DisplayTo = new 사가정저밀도교통Point
                                {
                                    X = edge.DisplayTo.X,
                                    Z = edge.DisplayTo.Z
                                }
                            }).ToArray()
                    }).ToArray(),
                CourierRouteState = ToCourierRouteState(states.Single(value =>
                    value.Seed.KindCode == 사가정저밀도교통ActorKindCodes.CourierMotorcycle),
                    input.CourierDisplayRoute, lanes)
            };
        }

        private static void 검증(사가정저밀도교통Input input, long tick)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (tick < 0L || tick > MaximumSimulationTick)
                throw new ArgumentOutOfRangeException(nameof(tick));
            var profile = input.Profile ?? throw new InvalidDataException("TrafficProfileMissing");
            var profileNumbers = new[]
            {
                profile.TickSeconds, profile.NorthSouthGreenSeconds,
                profile.AmberSeconds, profile.AllRedSeconds,
                profile.EastWestGreenSeconds, profile.MainRoadSpeedMetersPerSecond,
                profile.NeighborhoodRoadSpeedMetersPerSecond,
                profile.AlleySpeedMetersPerSecond, profile.StopLineBufferMeters,
                profile.MinimumGapMeters, profile.CycleSeconds
            };
            if (profileNumbers.Any(value => !Finite(value))
                || string.IsNullOrWhiteSpace(profile.Revision)
                || string.IsNullOrWhiteSpace(profile.SignalPlanRevision)
                || profile.TickSeconds <= 0d || profile.TickSeconds > 1d
                || profile.NorthSouthGreenSeconds <= 0d
                || profile.AmberSeconds <= 0d || profile.AllRedSeconds <= 0d
                || profile.EastWestGreenSeconds <= 0d
                || Math.Abs(profile.CycleSeconds - 28d) > .000001d
                || profile.MainRoadSpeedMetersPerSecond <= 0d
                || profile.NeighborhoodRoadSpeedMetersPerSecond <= 0d
                || profile.AlleySpeedMetersPerSecond <= 0d
                || profile.StopLineBufferMeters <= 0d
                || profile.MinimumGapMeters <= profile.StopLineBufferMeters)
                throw new InvalidDataException("TrafficProfileInvalid");
            if (input.EpochUtc.Offset != TimeSpan.Zero
                || string.IsNullOrWhiteSpace(input.SourceObservationRevision)
                || string.IsNullOrWhiteSpace(input.LaneGraphRevision)
                || string.IsNullOrWhiteSpace(input.CourierJourneyId)
                || !사가정저밀도교통Fingerprint.IsSha256(input.RouteFingerprint))
                throw new InvalidDataException("TrafficContextInvalid");
            if (input.Lanes == null || input.Actors == null)
                throw new InvalidDataException("TrafficCollectionMissing");
            if (input.Lanes.Any(value => value == null)
                || input.Actors.Any(value => value == null))
                throw new InvalidDataException("TrafficCollectionEntryMissing");
            if (input.ScenarioSeed != 사가정저밀도교통검증Fixture.ScenarioSeed
                || input.Lanes.Length != 4
                || input.Actors.Length != 4
                || input.Actors.Count(value => value.KindCode ==
                    사가정저밀도교통ActorKindCodes.CourierMotorcycle) != 1
                || input.Actors.Count(value => value.KindCode ==
                    사가정저밀도교통ActorKindCodes.AmbientPassengerCar) != 3)
                throw new InvalidDataException("LowDensityProfileBindingInvalid");
            if (input.Lanes.Select(value => value.StableId).Distinct(StringComparer.Ordinal).Count()
                != input.Lanes.Length
                || input.Actors.Select(value => value.StableId).Distinct(StringComparer.Ordinal).Count()
                != input.Actors.Length)
                throw new InvalidDataException("TrafficIdentityDuplicate");
            foreach (var lane in input.Lanes)
            {
                if (lane == null
                    || string.IsNullOrWhiteSpace(lane.StableId)
                    || string.IsNullOrWhiteSpace(lane.SourceWayStableId)
                    || !string.Equals(lane.SourceDirectionCode,
                        사가정저밀도교통SourceAuthorityCodes.UnknownDirection,
                        StringComparison.Ordinal)
                    || !string.Equals(lane.SourceAccessReviewCode,
                        사가정저밀도교통SourceAuthorityCodes.PendingHumanReview,
                        StringComparison.Ordinal)
                    || lane.SourceRuntimeAuthorized
                    || lane.Points == null || lane.Points.Length < 2
                    || lane.Points.Any(point => point == null
                                                || !Finite(point.X) || !Finite(point.Z))
                    || !Finite(lane.LengthMeters) || lane.LengthMeters <= 0d
                    || lane.StopLineProgressMeters.HasValue
                    && !Finite(lane.StopLineProgressMeters.Value))
                    throw new InvalidDataException("TrafficLaneInvalid");
                var controlled = lane.AxisCode == 사가정저밀도교통AxisCodes.EastWest
                                 || lane.AxisCode == 사가정저밀도교통AxisCodes.NorthSouth;
                var candidate = lane.AxisCode ==
                                사가정저밀도교통AxisCodes.NeighborhoodUncontrolled
                                || lane.AxisCode ==
                                사가정저밀도교통AxisCodes.AlleyUncontrolled;
                if (lane.PingPong || controlled == candidate
                    || controlled && lane.RouteKindCode !=
                    사가정저밀도교통RouteKindCodes.SyntheticMainLaneDisplayOnly
                    || candidate && lane.RouteKindCode !=
                    사가정저밀도교통RouteKindCodes.ManualVisualReviewCandidate
                    || candidate && lane.StopLineProgressMeters.HasValue
                    || lane.StopLineProgressMeters.HasValue
                    && (lane.StopLineProgressMeters <= profile.StopLineBufferMeters
                        || lane.StopLineProgressMeters >= lane.LengthMeters))
                    throw new InvalidDataException("TrafficLaneAuthorityInvalid");
            }
            var laneIds = new HashSet<string>(input.Lanes.Select(value => value.StableId),
                StringComparer.Ordinal);
            foreach (var actor in input.Actors)
            {
                if (actor == null || string.IsNullOrWhiteSpace(actor.StableId)
                    || !laneIds.Contains(actor.LaneStableId))
                    throw new InvalidDataException("TrafficActorInvalid");
                var lane = input.Lanes.Single(value => value.StableId == actor.LaneStableId);
                if (!Finite(actor.InitialProgressMeters) || actor.InitialProgressMeters < 0d
                    || actor.InitialProgressMeters > lane.LengthMeters
                    || actor.KindCode == 사가정저밀도교통ActorKindCodes.AmbientPassengerCar
                    && lane.AxisCode == 사가정저밀도교통AxisCodes.AlleyUncontrolled)
                    throw new InvalidDataException("TrafficActorPlacementInvalid");
            }
            var laneHash = 사가정저밀도교통Fingerprint.LaneGraph(input.Lanes);
            if (!사가정저밀도교통Fingerprint.IsSha256(input.LaneGraphHash)
                || !string.Equals(laneHash, input.LaneGraphHash, StringComparison.Ordinal))
                throw new InvalidDataException("LaneGraphHashMismatch");
            검증CourierRoute(input.CourierDisplayRoute, input.Lanes, input.LaneGraphHash);
            var courierSeed = input.Actors.Single(value => value.KindCode ==
                사가정저밀도교통ActorKindCodes.CourierMotorcycle);
            if (!string.Equals(courierSeed.LaneStableId,
                    input.CourierDisplayRoute.Legs[0].LaneStableId,
                    StringComparison.Ordinal))
                throw new InvalidDataException("CourierRouteStartMismatch");
            if (input.OccupiedAlleyResources == null
                || input.OccupiedAlleyResources.Any(pair =>
                    !string.Equals(pair.Key, 사가정저밀도교통검증Fixture.AlleyResource,
                        StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(pair.Value)))
                throw new InvalidDataException("AlleyOccupancyInvalid");
            var configHash = 사가정저밀도교통Fingerprint.Configuration(input);
            if (!사가정저밀도교통Fingerprint.IsSha256(input.ConfigurationFingerprint)
                || !string.Equals(configHash, input.ConfigurationFingerprint,
                    StringComparison.Ordinal))
                throw new InvalidDataException("TrafficConfigurationFingerprintMismatch");
            var inputStateHash = 사가정저밀도교통Fingerprint.InputState(input);
            if (!사가정저밀도교통Fingerprint.IsSha256(input.InputStateFingerprint)
                || !string.Equals(inputStateHash, input.InputStateFingerprint,
                    StringComparison.Ordinal))
                throw new InvalidDataException("TrafficInputStateFingerprintMismatch");
            foreach (var group in input.Actors.Where(value =>
                             value.KindCode == 사가정저밀도교통ActorKindCodes.AmbientPassengerCar)
                         .GroupBy(value => value.LaneStableId, StringComparer.Ordinal))
            {
                var ordered = group.OrderByDescending(value => value.InitialProgressMeters)
                    .ThenBy(value => value.StableId, StringComparer.Ordinal).ToArray();
                for (var index = 1; index < ordered.Length; index++)
                    if (ordered[index - 1].InitialProgressMeters
                        - ordered[index].InitialProgressMeters < profile.MinimumGapMeters)
                        throw new InvalidDataException("TrafficInitialGapInvalid");
            }
            if (!ApprovedProfile(profile)
                || input.EpochUtc != new DateTimeOffset(2026, 9, 13, 0, 0, 0,
                    TimeSpan.Zero)
                || !string.Equals(input.SourceObservationRevision,
                    사가정저밀도교통검증Fixture.SourceObservationRevision,
                    StringComparison.Ordinal)
                || !string.Equals(input.LaneGraphRevision,
                    사가정저밀도교통검증Fixture.LaneGraphRevision,
                    StringComparison.Ordinal)
                || !string.Equals(input.CourierJourneyId,
                    사가정저밀도교통검증Fixture.CourierJourneyId,
                    StringComparison.Ordinal)
                || !string.Equals(input.RouteFingerprint,
                    사가정저밀도교통검증Fixture.CourierRouteFingerprint,
                    StringComparison.Ordinal)
                || !string.Equals(input.CourierDisplayRoute.Revision,
                    사가정저밀도교통검증Fixture.CourierDisplayRouteRevision,
                    StringComparison.Ordinal)
                || !ApprovedLanes(input.Lanes) || !ApprovedActors(input.Actors)
                || !ApprovedCourierRoute(input.CourierDisplayRoute))
                throw new InvalidDataException("LowDensityApprovedFixtureMismatch");
        }

        private static void 검증CourierRoute(
            사가정저밀도교통CourierRouteDefinition route,
            사가정저밀도교통LaneDefinition[] lanes,
            string laneGraphHash)
        {
            if (route == null || string.IsNullOrWhiteSpace(route.Revision)
                || string.IsNullOrWhiteSpace(route.SourceMobilityGraphStableId)
                || string.IsNullOrWhiteSpace(route.SourceMobilityGraphRevision)
                || !사가정저밀도교통Fingerprint.IsSha256(
                    route.SourceMobilityGraphProjectionHashSha256)
                || !사가정저밀도교통Fingerprint.IsSha256(
                    route.SourceMobilityGraphContentHashSha256)
                || !string.Equals(route.SourceAccessReviewCode,
                    사가정저밀도교통SourceAuthorityCodes.PendingHumanReview,
                    StringComparison.Ordinal)
                || !string.Equals(route.SourceDirectionAuthorityCode,
                    사가정저밀도교통SourceAuthorityCodes.UnknownDirection,
                    StringComparison.Ordinal)
                || route.SourceRuntimeAuthorized
                || route.Legs == null || route.Legs.Length != 3
                || route.Legs.Any(value => value == null
                    || string.IsNullOrWhiteSpace(value.StableId)
                    || string.IsNullOrWhiteSpace(value.LaneStableId)
                    || string.IsNullOrWhiteSpace(value.StageCode)
                    || value.SourceEdges == null || value.SourceEdges.Length == 0)
                || route.Legs.Select(value => value.StableId)
                    .Distinct(StringComparer.Ordinal).Count() != route.Legs.Length
                || route.Legs.Select(value => value.LaneStableId)
                    .Distinct(StringComparer.Ordinal).Count() != route.Legs.Length)
                throw new InvalidDataException("CourierDisplayRouteInvalid");
            var laneIds = new HashSet<string>(lanes.Select(value => value.StableId),
                StringComparer.Ordinal);
            if (route.Legs.Any(value => !laneIds.Contains(value.LaneStableId)))
                throw new InvalidDataException("CourierDisplayRouteLaneMissing");
            var sourceEdgeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var leg in route.Legs)
            {
                var lane = lanes.Single(value => value.StableId == leg.LaneStableId);
                for (var index = 0; index < leg.SourceEdges.Length; index++)
                {
                    var edge = leg.SourceEdges[index];
                    if (edge == null || string.IsNullOrWhiteSpace(edge.EdgeStableId)
                        || !sourceEdgeIds.Add(edge.EdgeStableId)
                        || edge.DisplayGeometryOrderCode !=
                           사가정저밀도교통SourceGeometryOrderCodes.AsStored
                        && edge.DisplayGeometryOrderCode !=
                           사가정저밀도교통SourceGeometryOrderCodes.ReverseStored
                        || !string.Equals(edge.SourceDirectionCode,
                            사가정저밀도교통SourceAuthorityCodes.UnknownDirection,
                            StringComparison.Ordinal)
                        || !string.Equals(edge.AccessReviewCode,
                            사가정저밀도교통SourceAuthorityCodes.PendingHumanReview,
                            StringComparison.Ordinal)
                        || edge.RuntimeAuthorized || !Finite(edge.SourceDistanceMeters)
                        || edge.SourceDistanceMeters <= 0d || edge.DisplayFrom == null
                        || edge.DisplayTo == null || !Finite(edge.DisplayFrom.X)
                        || !Finite(edge.DisplayFrom.Z) || !Finite(edge.DisplayTo.X)
                        || !Finite(edge.DisplayTo.Z)
                        || index > 0 && !SamePoint(leg.SourceEdges[index - 1].DisplayTo,
                            edge.DisplayFrom))
                        throw new InvalidDataException("CourierDisplayRouteSourceEdgeInvalid");
                }
                if (!SamePoint(leg.SourceEdges[0].DisplayFrom, lane.Points[0])
                    || !SamePoint(leg.SourceEdges[leg.SourceEdges.Length - 1].DisplayTo,
                        lane.Points[lane.Points.Length - 1]))
                    throw new InvalidDataException("CourierDisplayRouteSourceEdgeEndpointMismatch");
            }
            var fingerprint = 사가정저밀도교통Fingerprint.CourierDisplayRoute(
                route, laneGraphHash, lanes);
            if (!사가정저밀도교통Fingerprint.IsSha256(route.Fingerprint)
                || !string.Equals(fingerprint, route.Fingerprint, StringComparison.Ordinal))
                throw new InvalidDataException("CourierDisplayRouteFingerprintMismatch");
        }

        private static bool SamePoint(사가정저밀도교통Point left,
            사가정저밀도교통Point right)
            => left.X == right.X && left.Z == right.Z;

        private static bool ApprovedProfile(사가정저밀도교통Profile profile)
            => string.Equals(profile.Revision,
                   사가정저밀도교통검증Fixture.ProfileRevision, StringComparison.Ordinal)
               && string.Equals(profile.SignalPlanRevision,
                   사가정저밀도교통검증Fixture.SignalPlanRevision, StringComparison.Ordinal)
               && profile.TickSeconds == .25d
               && profile.NorthSouthGreenSeconds == 10d
               && profile.AmberSeconds == 2d
               && profile.AllRedSeconds == 2d
               && profile.EastWestGreenSeconds == 10d
               && profile.MainRoadSpeedMetersPerSecond == 6d
               && profile.NeighborhoodRoadSpeedMetersPerSecond == 4d
               && profile.AlleySpeedMetersPerSecond == 2.5d
               && profile.StopLineBufferMeters == 1.5d
               && profile.MinimumGapMeters == 6d;

        private static bool ApprovedLanes(사가정저밀도교통LaneDefinition[] lanes)
        {
            var byId = lanes.ToDictionary(value => value.StableId, StringComparer.Ordinal);
            return byId.TryGetValue(사가정저밀도교통검증Fixture.EastWestLane,
                       out var eastWest)
                   && byId.TryGetValue(사가정저밀도교통검증Fixture.NorthSouthLane,
                        out var northSouth)
                   && byId.TryGetValue(
                       사가정저밀도교통검증Fixture.NeighborhoodAccessLane,
                       out var neighborhood)
                   && byId.TryGetValue(사가정저밀도교통검증Fixture.AlleyLane,
                       out var alley)
                   && MatchesLane(eastWest,
                       "osm:way:218790209", 사가정저밀도교통AxisCodes.EastWest,
                       사가정저밀도교통RouteKindCodes.SyntheticMainLaneDisplayOnly,
                       52.59d, false, (502.470d, 10.708d), (530.294d, 6.912d),
                       (554.576d, 3.594d), (576.941d, -.524d),
                       (582.841d, -1.900d), (608.696d, -7.927d))
                   && MatchesLane(northSouth,
                       "osm:way:218798305", 사가정저밀도교통AxisCodes.NorthSouth,
                       사가정저밀도교통RouteKindCodes.SyntheticMainLaneDisplayOnly,
                        37.25d, false, (545.954d, -32.644d), (554.576d, 3.594d),
                        (557.234d, 24.915d), (560.706d, 52.762d))
                   && MatchesLane(neighborhood,
                       "osm:way-chain:1112326984+1112326970+576294786",
                       사가정저밀도교통AxisCodes.NeighborhoodUncontrolled,
                       사가정저밀도교통RouteKindCodes.ManualVisualReviewCandidate,
                       null, false, (608.696d, -7.927d), (648.815d, -17.061d),
                       (652.384d, .342d), (654.168d, 8.000d),
                       (665.147d, 55.115d), (668.194d, 86.381d))
                   && MatchesLane(alley,
                       "osm:way:1256772587", 사가정저밀도교통AxisCodes.AlleyUncontrolled,
                       사가정저밀도교통RouteKindCodes.ManualVisualReviewCandidate,
                       null, false, (668.194d, 86.381d), (646.429d, 89.222d),
                       (645.961d, 89.211d), (645.625d, 89.089d));
        }

        private static bool MatchesLane(
            사가정저밀도교통LaneDefinition lane, string source, string axis,
            string routeKind, double? stopLine, bool pingPong,
            params (double X, double Z)[] points)
            => string.Equals(lane.SourceWayStableId, source, StringComparison.Ordinal)
               && string.Equals(lane.AxisCode, axis, StringComparison.Ordinal)
               && string.Equals(lane.RouteKindCode, routeKind, StringComparison.Ordinal)
               && string.Equals(lane.SourceDirectionCode,
                   사가정저밀도교통SourceAuthorityCodes.UnknownDirection,
                   StringComparison.Ordinal)
               && string.Equals(lane.SourceAccessReviewCode,
                   사가정저밀도교통SourceAuthorityCodes.PendingHumanReview,
                   StringComparison.Ordinal)
               && !lane.SourceRuntimeAuthorized
               && lane.StopLineProgressMeters == stopLine && lane.PingPong == pingPong
               && lane.Points.Length == points.Length
               && lane.Points.Select((point, index) =>
                       point.X == points[index].X && point.Z == points[index].Z)
                   .All(value => value);

        private static bool ApprovedActors(사가정저밀도교통ActorSeed[] actors)
        {
            var byId = actors.ToDictionary(value => value.StableId, StringComparer.Ordinal);
            return byId.TryGetValue("actor:synthetic-courier:sagajeong:1", out var courier)
                   && byId.TryGetValue("actor:synthetic-ambient-car:sagajeong:1", out var car1)
                   && byId.TryGetValue("actor:synthetic-ambient-car:sagajeong:2", out var car2)
                   && byId.TryGetValue("actor:synthetic-ambient-car:sagajeong:3", out var car3)
                   && MatchesActor(courier,
                       사가정저밀도교통ActorKindCodes.CourierMotorcycle,
                       사가정저밀도교통검증Fixture.EastWestLane, 0d)
                   && MatchesActor(car1,
                       사가정저밀도교통ActorKindCodes.AmbientPassengerCar,
                       사가정저밀도교통검증Fixture.EastWestLane, 40d)
                   && MatchesActor(car2,
                       사가정저밀도교통ActorKindCodes.AmbientPassengerCar,
                       사가정저밀도교통검증Fixture.EastWestLane, 30d)
                   && MatchesActor(car3,
                       사가정저밀도교통ActorKindCodes.AmbientPassengerCar,
                       사가정저밀도교통검증Fixture.NorthSouthLane, 5d);
        }

        private static bool MatchesActor(사가정저밀도교통ActorSeed actor,
            string kind, string lane, double progress)
            => string.Equals(actor.KindCode, kind, StringComparison.Ordinal)
               && string.Equals(actor.LaneStableId, lane, StringComparison.Ordinal)
               && actor.InitialProgressMeters == progress;

        private static bool ApprovedCourierRoute(
            사가정저밀도교통CourierRouteDefinition route)
            => string.Equals(route.SourceMobilityGraphStableId,
                   사가정저밀도교통검증Fixture.SourceMobilityGraphStableId,
                   StringComparison.Ordinal)
               && string.Equals(route.SourceMobilityGraphRevision,
                   사가정저밀도교통검증Fixture.SourceMobilityGraphRevision,
                   StringComparison.Ordinal)
               && string.Equals(route.SourceMobilityGraphProjectionHashSha256,
                   사가정저밀도교통검증Fixture.SourceMobilityGraphProjectionHashSha256,
                   StringComparison.Ordinal)
               && string.Equals(route.SourceMobilityGraphContentHashSha256,
                   사가정저밀도교통검증Fixture.SourceMobilityGraphContentHashSha256,
                   StringComparison.Ordinal)
               && string.Equals(route.Fingerprint,
                   사가정저밀도교통검증Fixture.CourierDisplayRouteFingerprint,
                   StringComparison.Ordinal)
               && route.Legs.Length == 3
               && MatchesRouteLeg(route.Legs[0],
                   "route-leg:synthetic:sagajeong:main-road.r2",
                   사가정저밀도교통검증Fixture.EastWestLane,
                   사가정저밀도교통JourneyStageCodes.MainRoadApproach)
               && MatchesRouteLeg(route.Legs[1],
                   "route-leg:synthetic:sagajeong:neighborhood-road.r2",
                   사가정저밀도교통검증Fixture.NeighborhoodAccessLane,
                   사가정저밀도교통JourneyStageCodes.NeighborhoodRoadApproach)
               && MatchesRouteLeg(route.Legs[2],
                   "route-leg:synthetic:sagajeong:alley-final.r2",
                   사가정저밀도교통검증Fixture.AlleyLane,
                   사가정저밀도교통JourneyStageCodes.AlleyFinalApproach);

        private static bool MatchesRouteLeg(
            사가정저밀도교통CourierRouteLegDefinition leg,
            string stableId, string laneStableId, string stageCode)
            => string.Equals(leg.StableId, stableId, StringComparison.Ordinal)
               && string.Equals(leg.LaneStableId, laneStableId, StringComparison.Ordinal)
               && string.Equals(leg.StageCode, stageCode, StringComparison.Ordinal);

        private static void Advance(
            ActorState[] states,
            IReadOnlyDictionary<string, 사가정저밀도교통LaneDefinition> lanes,
            Dictionary<string, string> alleyOwners,
            사가정저밀도교통Profile profile,
            long step)
        {
            var time = step * profile.TickSeconds;
            foreach (var laneGroup in states.Where(value => !value.ExitedObservation)
                         .GroupBy(value => value.CurrentLaneStableId, StringComparer.Ordinal))
            {
                var lane = lanes[laneGroup.Key];
                if (lane.AxisCode == 사가정저밀도교통AxisCodes.AlleyUncontrolled)
                {
                    foreach (var state in laneGroup)
                        AdvanceAlley(state, lane, alleyOwners, profile, step);
                    continue;
                }
                if (lane.AxisCode ==
                    사가정저밀도교통AxisCodes.NeighborhoodUncontrolled)
                {
                    foreach (var state in laneGroup)
                        AdvanceUncontrolled(state, lane,
                            profile.NeighborhoodRoadSpeedMetersPerSecond, profile);
                    continue;
                }
                var signal = Signal(profile, lane.AxisCode, time).StateCode;
                var ordered = laneGroup.OrderByDescending(value => value.Progress)
                    .ThenBy(value => value.Seed.StableId, StringComparer.Ordinal).ToArray();
                double? leader = null;
                foreach (var state in ordered)
                {
                    state.StateCode = 사가정저밀도교통ActorStateCodes.Moving;
                    state.BlockedReason = string.Empty;
                    var desired = state.Progress
                                  + profile.MainRoadSpeedMetersPerSecond * profile.TickSeconds;
                    var localStop = lane.StopLineProgressMeters!.Value
                                    - profile.StopLineBufferMeters;
                    var stop = NextStopAtOrAfter(state.Progress, localStop,
                        lane.LengthMeters);
                    if (signal != 사가정저밀도교통SignalStateCodes.Green
                        && desired >= stop)
                    {
                        desired = stop;
                        state.StateCode = 사가정저밀도교통ActorStateCodes.WaitingAtSignal;
                        state.BlockedReason = "SyntheticSignalGate";
                    }
                    if (leader.HasValue && desired > leader.Value - profile.MinimumGapMeters)
                    {
                        desired = Math.Max(state.Progress,
                            leader.Value - profile.MinimumGapMeters);
                        state.StateCode = 사가정저밀도교통ActorStateCodes.Queueing;
                        state.BlockedReason = "MinimumGap";
                    }
                    if (state.StateCode == 사가정저밀도교통ActorStateCodes.Moving
                        && desired >= lane.LengthMeters)
                    {
                        state.Progress = lane.LengthMeters;
                        CompleteLane(state, lane);
                        continue;
                    }
                    // 상태 사본의 속도는 이번 tick의 이동 평균이 아니라 tick 종료 시점의
                    // 순간 상태를 나타낸다. 정지선 또는 최소 간격에 도달해 대기 상태가
                    // 되었다면 그 tick에 일부 이동했더라도 현재 속도는 0이다.
                    state.Speed = state.StateCode ==
                                  사가정저밀도교통ActorStateCodes.Moving
                        ? Math.Max(0d, desired - state.Progress) / profile.TickSeconds
                        : 0d;
                    state.Progress = desired;
                    leader = desired;
                }
            }
        }

        private static void AdvanceUncontrolled(ActorState state,
            사가정저밀도교통LaneDefinition lane, double speed,
            사가정저밀도교통Profile profile)
        {
            if (state.CandidateEndpointReached) return;
            state.StateCode = 사가정저밀도교통ActorStateCodes.MovingOnNeighborhoodRoad;
            state.BlockedReason = string.Empty;
            var desired = state.Progress + speed * profile.TickSeconds;
            if (desired >= lane.LengthMeters)
            {
                state.Progress = lane.LengthMeters;
                CompleteLane(state, lane);
                return;
            }
            state.Progress = desired;
            state.Speed = speed;
        }

        private static void AdvanceAlley(
            ActorState state,
            사가정저밀도교통LaneDefinition lane,
            Dictionary<string, string> owners,
            사가정저밀도교통Profile profile,
            long step)
        {
            if (state.CandidateEndpointReached) return;
            var request = new 이동자원진입요청(state.Seed.StableId, step,
                사가정저밀도교통검증Fixture.AlleyResource);
            var occupancy = 이동자원점유Policy.판정(owners, new[] { request });
            var decision = occupancy.판정목록[0];
            if (!decision.진입가능)
            {
                state.StateCode = 사가정저밀도교통ActorStateCodes.WaitingForAlley;
                state.BlockedReason = decision.사유;
                state.Speed = 0d;
                return;
            }
            owners.Clear();
            foreach (var pair in occupancy.점유후보) owners[pair.Key] = pair.Value;
            var movement = profile.AlleySpeedMetersPerSecond * profile.TickSeconds;
            var desired = state.Progress + movement;
            if (desired >= lane.LengthMeters)
            {
                state.Progress = lane.LengthMeters;
                CompleteLane(state, lane);
                return;
            }
            state.Progress = desired;
            state.Speed = profile.AlleySpeedMetersPerSecond;
            state.StateCode = 사가정저밀도교통ActorStateCodes.MovingInAlley;
            state.BlockedReason = string.Empty;
        }

        private static void CompleteLane(ActorState state,
            사가정저밀도교통LaneDefinition lane)
        {
            if (state.Route == null)
            {
                state.ExitedObservation = true;
                return;
            }
            if (state.CurrentRouteLegIndex + 1 < state.Route.Legs.Length)
            {
                state.CurrentRouteLegIndex++;
                state.CurrentLaneStableId =
                    state.Route.Legs[state.CurrentRouteLegIndex].LaneStableId;
                state.Progress = 0d;
                state.Speed = 0d;
                state.StateCode = state.Route.Legs[state.CurrentRouteLegIndex].StageCode ==
                                  사가정저밀도교통JourneyStageCodes.AlleyFinalApproach
                    ? 사가정저밀도교통ActorStateCodes.MovingInAlley
                    : 사가정저밀도교통ActorStateCodes.MovingOnNeighborhoodRoad;
                state.BlockedReason = string.Empty;
                return;
            }
            state.Progress = lane.LengthMeters;
            state.Speed = 0d;
            state.CandidateEndpointReached = true;
            state.StateCode = 사가정저밀도교통ActorStateCodes.StoppedAtCandidateEndpoint;
            state.BlockedReason = string.Empty;
        }

        private static void AssignQueueIndexes(IEnumerable<ActorState> states)
        {
            foreach (var state in states) state.QueueIndex = -1;
            foreach (var group in states.Where(value => !value.ExitedObservation && (
                     value.StateCode == 사가정저밀도교통ActorStateCodes.WaitingAtSignal
                         || value.StateCode == 사가정저밀도교통ActorStateCodes.Queueing))
                     .GroupBy(value => value.CurrentLaneStableId, StringComparer.Ordinal))
            {
                var index = 0;
                foreach (var state in group.OrderByDescending(value => value.Progress))
                    state.QueueIndex = index++;
            }
        }

        private static SignalState Signal(
            사가정저밀도교통Profile profile, string axis, double seconds)
        {
            var cycle = seconds % profile.CycleSeconds;
            var nsGreenEnd = profile.NorthSouthGreenSeconds;
            var nsAmberEnd = nsGreenEnd + profile.AmberSeconds;
            var firstAllRedEnd = nsAmberEnd + profile.AllRedSeconds;
            var ewGreenEnd = firstAllRedEnd + profile.EastWestGreenSeconds;
            var ewAmberEnd = ewGreenEnd + profile.AmberSeconds;
            if (axis == 사가정저밀도교통AxisCodes.NorthSouth)
            {
                if (cycle < nsGreenEnd)
                    return new SignalState(사가정저밀도교통SignalStateCodes.Green,
                        nsGreenEnd - cycle);
                if (cycle < nsAmberEnd)
                    return new SignalState(사가정저밀도교통SignalStateCodes.Amber,
                        nsAmberEnd - cycle);
                return new SignalState(사가정저밀도교통SignalStateCodes.Red,
                    profile.CycleSeconds - cycle);
            }
            if (cycle < firstAllRedEnd)
                return new SignalState(사가정저밀도교통SignalStateCodes.Red,
                    firstAllRedEnd - cycle);
            if (cycle < ewGreenEnd)
                return new SignalState(사가정저밀도교통SignalStateCodes.Green,
                    ewGreenEnd - cycle);
            if (cycle < ewAmberEnd)
                return new SignalState(사가정저밀도교통SignalStateCodes.Amber,
                    ewAmberEnd - cycle);
            return new SignalState(사가정저밀도교통SignalStateCodes.Red,
                profile.CycleSeconds - cycle);
        }

        private static 사가정저밀도교통SignalGroupSnapshot ToSignal(
            string id, string axis, SignalState state, string stopLine)
            => new 사가정저밀도교통SignalGroupSnapshot
            {
                StableId = id,
                AxisCode = axis,
                StateCode = state.StateCode,
                SecondsUntilTransition = state.SecondsUntilTransition,
                StopLineStableId = stopLine
            };

        private static 사가정저밀도교통ActorSnapshot ToActor(
            ActorState state, 사가정저밀도교통LaneDefinition lane,
            사가정저밀도교통Profile profile)
        {
            var sampleProgress = state.Progress;
            Sample(lane, sampleProgress, out var point, out var heading);
            if (!state.Forward) heading = NormalizeHeading(heading + 180d);
            return new 사가정저밀도교통ActorSnapshot
            {
                StableId = state.Seed.StableId,
                KindCode = state.Seed.KindCode,
                LaneStableId = lane.StableId,
                RouteKindCode = lane.RouteKindCode,
                StateCode = state.StateCode,
                DisplayPolylineOrderCode = state.Forward
                    ? 사가정저밀도교통DisplayPolylineOrderCodes.AsDefined
                    : 사가정저밀도교통DisplayPolylineOrderCodes.ReverseDefined,
                ProgressMeters = sampleProgress,
                SpeedMetersPerSecond = state.Speed,
                QueueIndex = state.QueueIndex,
                BlockedReasonCode = state.BlockedReason,
                Position = point,
                HeadingDegrees = heading
            };
        }

        private static 사가정저밀도교통CourierRouteStateSnapshot ToCourierRouteState(
            ActorState state, 사가정저밀도교통CourierRouteDefinition route,
            IReadOnlyDictionary<string, 사가정저밀도교통LaneDefinition> lanes)
        {
            var completed = route.Legs.Take(state.CurrentRouteLegIndex)
                .Sum(value => lanes[value.LaneStableId].LengthMeters);
            var total = route.Legs.Sum(value => lanes[value.LaneStableId].LengthMeters);
            var leg = route.Legs[state.CurrentRouteLegIndex];
            return new 사가정저밀도교통CourierRouteStateSnapshot
            {
                CurrentRouteLegStableId = leg.StableId,
                CurrentRouteLegIndex = state.CurrentRouteLegIndex,
                RouteLegCount = route.Legs.Length,
                CurrentLaneStableId = state.CurrentLaneStableId,
                StageCode = state.CandidateEndpointReached
                    ? 사가정저밀도교통JourneyStageCodes.CandidateEndpointReached
                    : leg.StageCode,
                LegProgressMeters = state.Progress,
                CumulativeProgressMeters = completed + state.Progress,
                TotalDistanceMeters = total,
                CandidateEndpointReached = state.CandidateEndpointReached
            };
        }

        private static void Sample(사가정저밀도교통LaneDefinition lane,
            double progress, out 사가정저밀도교통Point point, out double heading)
        {
            var remaining = Math.Max(0d, Math.Min(lane.LengthMeters, progress));
            for (var index = 1; index < lane.Points.Length; index++)
            {
                var from = lane.Points[index - 1];
                var to = lane.Points[index];
                var dx = to.X - from.X;
                var dz = to.Z - from.Z;
                var length = Math.Sqrt(dx * dx + dz * dz);
                if (remaining <= length || index == lane.Points.Length - 1)
                {
                    var ratio = length <= 0d ? 0d : Math.Min(1d, remaining / length);
                    point = new 사가정저밀도교통Point
                    {
                        X = from.X + dx * ratio,
                        Z = from.Z + dz * ratio
                    };
                    heading = NormalizeHeading(Math.Atan2(dx, dz) * 180d / Math.PI);
                    return;
                }
                remaining -= length;
            }
            point = new 사가정저밀도교통Point
            {
                X = lane.Points[0].X,
                Z = lane.Points[0].Z
            };
            heading = 0d;
        }

        private static double NormalizeHeading(double value)
        {
            value %= 360d;
            return value < 0d ? value + 360d : value;
        }

        private static bool Finite(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value);

        private static double NextStopAtOrAfter(
            double progress, double localStop, double length)
        {
            const double epsilon = .0000001d;
            var lap = Math.Ceiling((progress - localStop - epsilon) / length);
            return localStop + Math.Max(0d, lap) * length;
        }

        private sealed class ActorState
        {
            public ActorState(사가정저밀도교통ActorSeed seed,
                사가정저밀도교통CourierRouteDefinition? route)
            {
                Seed = seed;
                Route = route;
                CurrentLaneStableId = seed.LaneStableId;
                Progress = seed.InitialProgressMeters;
            }

            public 사가정저밀도교통ActorSeed Seed { get; }
            public 사가정저밀도교통CourierRouteDefinition? Route { get; }
            public string CurrentLaneStableId { get; set; }
            public int CurrentRouteLegIndex { get; set; }
            public double Progress { get; set; }
            public double Speed { get; set; }
            public bool Forward { get; set; } = true;
            public bool ExitedObservation { get; set; }
            public bool CandidateEndpointReached { get; set; }
            public string StateCode { get; set; } = 사가정저밀도교통ActorStateCodes.Moving;
            public string BlockedReason { get; set; } = string.Empty;
            public int QueueIndex { get; set; } = -1;
        }

        private readonly struct SignalState
        {
            public SignalState(string stateCode, double secondsUntilTransition)
            {
                StateCode = stateCode;
                SecondsUntilTransition = secondsUntilTransition;
            }

            public string StateCode { get; }
            public double SecondsUntilTransition { get; }
        }
    }
}
