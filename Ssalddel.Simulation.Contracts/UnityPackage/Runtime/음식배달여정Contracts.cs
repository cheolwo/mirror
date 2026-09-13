using System;

namespace Ssalddel.Simulation.Contracts
{
    /// <summary>
    /// Simulation이 생성한 배달 여정을 Unity가 읽기 전용으로 표현하기 위한 wire 계약입니다.
    /// 이 사본의 도착·보간 결과는 주문·픽업·전달 상태를 변경할 수 없습니다.
    /// </summary>
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E1,
        "가상 음식 배달 여정 wire 판본과 Unity 읽기 전용 권위 경계를 고정한다.",
        Boundary = "SyntheticFixture 상태 사본 계약이며 운영 주문 변경·실제 경로 승인·Scene 결속 증거가 아니다.")]
    public static class 음식배달여정Policy
    {
        public const string SchemaVersion = "food-delivery-journey-snapshot.v1";
        public const string SyntheticFixtureSourceKind = "SyntheticFixture";
        public const bool UnityReadOnly = true;
        public const bool CanonicalOrderMutationAllowed = false;
    }

    public static class 음식배달여정ReadinessCodes
    {
        public const string Ready = "Ready";
        public const string RouteUnresolved = "RouteUnresolved";
    }

    public static class 음식배달여정LegStateCodes
    {
        public const string Pending = "Pending";
        public const string Active = "Active";
        public const string Completed = "Completed";
        public const string Blocked = "Blocked";

        public static bool IsSupported(string value)
            => value == Pending || value == Active || value == Completed || value == Blocked;
    }

    public static class 음식배달여정ModeCodes
    {
        public const string Vehicle = "Vehicle";
        public const string Pedestrian = "Pedestrian";
        public const string Motorcycle = "Motorcycle";

        public static bool IsSupported(string value)
            => value == Vehicle || value == Pedestrian || value == Motorcycle;
    }

    /// <summary>
    /// v1 wire 호환 값. 기존 Vehicle=0, Pedestrian=1을 변경하지 않고
    /// 오토바이 후보를 Motorcycle=2로 분리합니다.
    /// </summary>
    public enum 음식배달여정이동수단
    {
        Vehicle = 0,
        Pedestrian = 1,
        Motorcycle = 2
    }

    public sealed class 음식배달여정Point
    {
        public double X { get; set; }
        public double Z { get; set; }
    }

    public sealed class 음식배달여정Leg
    {
        public string StableId { get; set; } = string.Empty;
        public string Mode { get; set; } = 음식배달여정ModeCodes.Vehicle;
        public string StateCode { get; set; } = 음식배달여정LegStateCodes.Pending;
        public double DistanceMeters { get; set; }
        public double ProgressMeters { get; set; }
        public 음식배달여정Point[] Points { get; set; } = Array.Empty<음식배달여정Point>();
        public 음식배달여정Point ActorPosition { get; set; } = new 음식배달여정Point();
        public 음식배달여정Point? VehiclePosition { get; set; }
    }

    public sealed class 음식배달여정Snapshot
    {
        public string SchemaVersion { get; set; } = 음식배달여정Policy.SchemaVersion;
        public string RegionStableId { get; set; } = string.Empty;
        public string AdministrativeAreaStableId { get; set; } = string.Empty;
        public string OrderStableId { get; set; } = string.Empty;
        public string ActorStableId { get; set; } = string.Empty;
        public string VehicleStableId { get; set; } = string.Empty;
        public string RouteStableId { get; set; } = string.Empty;
        public string GraphRevision { get; set; } = string.Empty;
        public string GraphHash { get; set; } = string.Empty;
        public string RouteFingerprint { get; set; } = string.Empty;
        public long Revision { get; set; }
        public DateTimeOffset ObservedAt { get; set; }
        public bool DistributionApproved { get; set; }
        public string ReadinessCode { get; set; } = 음식배달여정ReadinessCodes.RouteUnresolved;
        public bool Blocked { get; set; }
        public int CurrentLegIndex { get; set; } = -1;
        public 음식배달여정Leg[] Legs { get; set; } = Array.Empty<음식배달여정Leg>();
        public string SourceKindCode { get; set; } = 음식배달여정Policy.SyntheticFixtureSourceKind;
    }
}
