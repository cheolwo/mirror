using System;

namespace Ssalddel.Simulation.Contracts
{
    /// <summary>
    /// 사가정역 저밀도 교통 검증 상태를 Unity가 읽기 전용으로 표현하기 위한 계약입니다.
    /// 공개자료는 위치 관찰 근거일 뿐이고, 신호 현시와 차량은 SyntheticFixture입니다.
    /// </summary>
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E1,
        "사가정 저밀도 신호·차량 상태 사본과 비권위 경계를 고정한다.",
        Boundary = "실제 신호 주기·교통량·통행 승인·운영 주문 변경·Scene 결속 증거가 아니다.")]
    public static class 사가정저밀도교통Policy
    {
        public const string SchemaVersion = "station-synthetic-traffic-snapshot.v2";
        public const string SyntheticFixtureSourceKind = "SyntheticFixture";
        public const string Ready = "ReadyForPrivateSyntheticDeliveryObservation";
        public const string StationStableId = "station:kr:kric:s1107:0722";
        public const string RegionStableId = "world-region:kr:seoul:jungnang:sagajeong.r1";
        public const string AdministrativeAreaStableId = "region:kr:hjd:1126057500";
        public const bool UnityReadOnly = true;
        public const bool ObservationPresentationOnly = true;
        public const bool DistributionApproved = false;
        public const bool TraversalReady = false;
        public const bool GameplayReady = false;
        public const bool OperationalBindingReady = false;
        public const bool CanonicalDeliveryMutationAllowed = false;
    }

    public static class 사가정저밀도교통SignalStateCodes
    {
        public const string Red = "Red";
        public const string Amber = "Amber";
        public const string Green = "Green";

        public static bool IsSupported(string value)
            => value == Red || value == Amber || value == Green;
    }

    public static class 사가정저밀도교통ActorKindCodes
    {
        public const string CourierMotorcycle = "CourierMotorcycle";
        public const string AmbientPassengerCar = "AmbientPassengerCar";
    }

    public static class 사가정저밀도교통ActorStateCodes
    {
        public const string Moving = "Moving";
        public const string MovingOnNeighborhoodRoad = "MovingOnNeighborhoodRoad";
        public const string MovingInAlley = "MovingInAlley";
        public const string WaitingAtSignal = "WaitingAtSignal";
        public const string Queueing = "Queueing";
        public const string WaitingForAlley = "WaitingForAlley";
        public const string StoppedAtCandidateEndpoint = "StoppedAtCandidateEndpoint";
    }

    public static class 사가정저밀도교통AxisCodes
    {
        public const string EastWest = "EastWest";
        public const string NorthSouth = "NorthSouth";
        public const string AlleyUncontrolled = "AlleyUncontrolled";
        public const string NeighborhoodUncontrolled = "NeighborhoodUncontrolled";
    }

    public static class 사가정저밀도교통RouteKindCodes
    {
        public const string SyntheticMainLaneDisplayOnly = "SyntheticLaneDisplayOnly";
        public const string ManualVisualReviewCandidate = "ManualVisualReviewCandidate";
    }

    public static class 사가정저밀도교통DisplayPolylineOrderCodes
    {
        public const string AsDefined = "AsDefined";
        public const string ReverseDefined = "ReverseDefined";
    }

    public static class 사가정저밀도교통SourceGeometryOrderCodes
    {
        public const string AsStored = "AsStored";
        public const string ReverseStored = "ReverseStored";
    }

    public static class 사가정저밀도교통SourceAuthorityCodes
    {
        public const string UnknownDirection = "Unknown";
        public const string PendingHumanReview = "PendingHumanReview";
        public const string DeclaredSyntheticReferenceOnly =
            "DeclaredSyntheticReferenceOnly";
    }

    public static class 사가정저밀도교통JourneyStageCodes
    {
        public const string MainRoadApproach = "MainRoadApproach";
        public const string NeighborhoodRoadApproach = "NeighborhoodRoadApproach";
        public const string AlleyFinalApproach = "AlleyFinalApproach";
        public const string CandidateEndpointReached = "CandidateEndpointReached";
    }

    public sealed class 사가정저밀도교통Point
    {
        public double X { get; set; }
        public double Z { get; set; }
    }

    public sealed class 사가정저밀도교통SignalGroupSnapshot
    {
        public string StableId { get; set; } = string.Empty;
        public string AxisCode { get; set; } = string.Empty;
        public string StateCode { get; set; } = 사가정저밀도교통SignalStateCodes.Red;
        public double SecondsUntilTransition { get; set; }
        public string StopLineStableId { get; set; } = string.Empty;
    }

    public sealed class 사가정저밀도교통LaneSnapshot
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
        public bool HasStopLine { get; set; }
        public double StopLineProgressMeters { get; set; }
        public bool PingPong { get; set; }
        public 사가정저밀도교통Point[] Points { get; set; } =
            Array.Empty<사가정저밀도교통Point>();
    }

    public sealed class 사가정저밀도교통ActorSnapshot
    {
        public string StableId { get; set; } = string.Empty;
        public string KindCode { get; set; } = string.Empty;
        public string LaneStableId { get; set; } = string.Empty;
        public string RouteKindCode { get; set; } = string.Empty;
        public string StateCode { get; set; } = 사가정저밀도교통ActorStateCodes.Moving;
        /// <summary>
        /// 제공된 표시 polyline의 점 순서에 대한 이동 방향입니다. OSM 통행 방향이 아닙니다.
        /// </summary>
        public string DisplayPolylineOrderCode { get; set; } =
            사가정저밀도교통DisplayPolylineOrderCodes.AsDefined;
        public double ProgressMeters { get; set; }
        public double SpeedMetersPerSecond { get; set; }
        public int QueueIndex { get; set; } = -1;
        public string BlockedReasonCode { get; set; } = string.Empty;
        public 사가정저밀도교통Point Position { get; set; } = new 사가정저밀도교통Point();
        public double HeadingDegrees { get; set; }
    }

    public sealed class 사가정저밀도교통SourceEdgeSnapshot
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

    public sealed class 사가정저밀도교통CourierRouteLegSnapshot
    {
        public string StableId { get; set; } = string.Empty;
        public int Index { get; set; }
        public string LaneStableId { get; set; } = string.Empty;
        public string StageCode { get; set; } = string.Empty;
        public double DisplayGeometryDistanceMeters { get; set; }
        public double SourceDistanceMeters { get; set; }
        public 사가정저밀도교통SourceEdgeSnapshot[] SourceEdges { get; set; } =
            Array.Empty<사가정저밀도교통SourceEdgeSnapshot>();
    }

    /// <summary>
    /// 주문 상태가 아니라, 동결된 OSM 중심선 후보 위에서 합성 배달 오토바이가
    /// 어느 표시 구간을 지나는지만 설명합니다.
    /// </summary>
    public sealed class 사가정저밀도교통CourierRouteStateSnapshot
    {
        public string CurrentRouteLegStableId { get; set; } = string.Empty;
        public int CurrentRouteLegIndex { get; set; }
        public int RouteLegCount { get; set; }
        public string CurrentLaneStableId { get; set; } = string.Empty;
        public string StageCode { get; set; } = string.Empty;
        public double LegProgressMeters { get; set; }
        public double CumulativeProgressMeters { get; set; }
        public double TotalDistanceMeters { get; set; }
        public bool CandidateEndpointReached { get; set; }
    }

    public sealed class 사가정저밀도교통Snapshot
    {
        public string SchemaVersion { get; set; } = 사가정저밀도교통Policy.SchemaVersion;
        public string StationStableId { get; set; } = 사가정저밀도교통Policy.StationStableId;
        public string RegionStableId { get; set; } = 사가정저밀도교통Policy.RegionStableId;
        public string AdministrativeAreaStableId { get; set; } =
            사가정저밀도교통Policy.AdministrativeAreaStableId;
        public string SourceKindCode { get; set; } = 사가정저밀도교통Policy.SyntheticFixtureSourceKind;
        public string SourceObservationRevision { get; set; } = string.Empty;
        public string SourceMobilityGraphStableId { get; set; } = string.Empty;
        public string SourceMobilityGraphRevision { get; set; } = string.Empty;
        public string SourceMobilityGraphProjectionHashSha256 { get; set; } = string.Empty;
        public string SourceMobilityGraphContentHashSha256 { get; set; } = string.Empty;
        public string SourceAccessReviewCode { get; set; } =
            사가정저밀도교통SourceAuthorityCodes.PendingHumanReview;
        public string SourceDirectionAuthorityCode { get; set; } =
            사가정저밀도교통SourceAuthorityCodes.UnknownDirection;
        public bool SourceRuntimeAuthorized { get; set; }
        public string LaneGraphRevision { get; set; } = string.Empty;
        public string LaneGraphHash { get; set; } = string.Empty;
        public string SignalPlanRevision { get; set; } = string.Empty;
        public string TrafficProfileRevision { get; set; } = string.Empty;
        public string CourierJourneyId { get; set; } = string.Empty;
        public string RouteFingerprint { get; set; } = string.Empty;
        public string CourierDisplayRouteRevision { get; set; } = string.Empty;
        public string CourierDisplayRouteFingerprint { get; set; } = string.Empty;
        public string ConfigurationFingerprint { get; set; } = string.Empty;
        public string InputStateFingerprint { get; set; } = string.Empty;
        public int ScenarioSeed { get; set; }
        public long SimulationTick { get; set; }
        public double TickSeconds { get; set; }
        public long Revision { get; set; }
        public DateTimeOffset ObservedAt { get; set; }
        public bool ObservationPresentationOnly { get; set; } = true;
        public bool DistributionApproved { get; set; }
        public bool TraversalReady { get; set; }
        public bool GameplayReady { get; set; }
        public string JourneyBindingStateCode { get; set; } =
            사가정저밀도교통SourceAuthorityCodes.DeclaredSyntheticReferenceOnly;
        public bool OperationalBindingReady { get; set; }
        public bool CanonicalDeliveryMutationAllowed { get; set; }
        public string ReadinessCode { get; set; } = 사가정저밀도교통Policy.Ready;
        public 사가정저밀도교통SignalGroupSnapshot[] Signals { get; set; } =
            Array.Empty<사가정저밀도교통SignalGroupSnapshot>();
        public 사가정저밀도교통LaneSnapshot[] Lanes { get; set; } =
            Array.Empty<사가정저밀도교통LaneSnapshot>();
        public 사가정저밀도교통ActorSnapshot[] Actors { get; set; } =
            Array.Empty<사가정저밀도교통ActorSnapshot>();
        public 사가정저밀도교통CourierRouteLegSnapshot[] CourierRouteLegs { get; set; } =
            Array.Empty<사가정저밀도교통CourierRouteLegSnapshot>();
        public 사가정저밀도교통CourierRouteStateSnapshot CourierRouteState { get; set; } =
            new 사가정저밀도교통CourierRouteStateSnapshot();
    }
}
