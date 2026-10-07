using Ssalddel.Contracts.Common.Privacy;

namespace Ssalddel.Contracts.Common.Community;

/// <summary>로그인한 본인의 한 생활 배송 지도입니다. 공개 글·추천 지도·실제 이동 이력과 분리합니다.</summary>
public sealed class NeighborhoodDeliveryMapResponse
{
    public string RequestId { get; set; } = string.Empty;
    public string RequestStatusCode { get; set; } = string.Empty;
    public string TransportStatusCode { get; set; } = string.Empty;
    public string StageCode { get; set; } = NeighborhoodDeliveryMapStages.Overview;
    public DateTime ObservedAtUtc { get; set; }
    /// <summary>출발·도착 좌표가 바뀌면 이전 예정 경로를 제거하는 식별자입니다.</summary>
    public string RouteRevision { get; set; } = string.Empty;
    [IsmsPProtectedData(PersonalDataFieldKey.LocationCoordinate, "본인 생활 배송 픽업 위치 확인")]
    public NeighborhoodDeliveryMapPoint? Pickup { get; set; }
    [IsmsPProtectedData(PersonalDataFieldKey.LocationCoordinate, "본인 생활 배송 전달 위치 확인")]
    public NeighborhoodDeliveryMapPoint? Dropoff { get; set; }
    public string RouteStateCode { get; set; } = NeighborhoodDeliveryMapStates.NotRequested;
    public string RouteNotice { get; set; } = string.Empty;
    [IsmsPProtectedData(PersonalDataFieldKey.LocationCoordinate, "본인 생활 배송의 도로 예정 경로 확인")]
    public NeighborhoodDeliveryPlannedRoute? PlannedRoute { get; set; }
    public string DriverLocationStateCode { get; set; } = NeighborhoodDeliveryMapStates.NotAssigned;
    public string DriverLocationNotice { get; set; } = string.Empty;
    [IsmsPProtectedData(PersonalDataFieldKey.LocationCoordinate, "확정된 본인 생활 배송의 최근 기사 위치 확인",
        ProtectionNote = "현재 배정 이후 측정·서버 수신 모두 10분 이내만 제공하며 종료 후에는 제거")]
    public NeighborhoodDeliveryRecentDriverLocation? DriverLocation { get; set; }
}

public sealed class NeighborhoodDeliveryMapPoint
{
    [IsmsPProtectedData(PersonalDataFieldKey.LocationCoordinate, "본인 생활 배송 지도 위도")]
    public decimal Latitude { get; set; }
    [IsmsPProtectedData(PersonalDataFieldKey.LocationCoordinate, "본인 생활 배송 지도 경도")]
    public decimal Longitude { get; set; }
    public string Label { get; set; } = string.Empty;
}

public sealed class NeighborhoodDeliveryPlannedRoute
{
    public string StageCode { get; set; } = NeighborhoodDeliveryMapStages.Overview;
    public string FromPointRoleCode { get; set; } = "Pickup";
    public string ToPointRoleCode { get; set; } = "Dropoff";
    [IsmsPProtectedData(PersonalDataFieldKey.LocationCoordinate, "본인 생활 배송의 도로 예정 경로")]
    public IReadOnlyList<NeighborhoodDeliveryMapPoint> Points { get; set; } = [];
    public decimal? DistanceKm { get; set; }
    public decimal? DurationMinutes { get; set; }
    public string SourceCode { get; set; } = "NaverCloudDirections";
    public DateTime CalculatedAtUtc { get; set; }
    public string TravelModeCode { get; set; } = "AutomobileReference";
    public string Notice { get; set; } = "자동차 도로 경로를 참고한 예정 경로입니다. 오토바이 전용 경로나 실제 이동 기록이 아닙니다.";
}

public sealed class NeighborhoodDeliveryRecentDriverLocation
{
    [IsmsPProtectedData(PersonalDataFieldKey.LocationCoordinate, "본인 생활 배송의 확정 기사 최근 위도")]
    public decimal Latitude { get; set; }
    [IsmsPProtectedData(PersonalDataFieldKey.LocationCoordinate, "본인 생활 배송의 확정 기사 최근 경도")]
    public decimal Longitude { get; set; }
    public decimal? AccuracyMeters { get; set; }
    public DateTime MeasuredAtUtc { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
}

public static class NeighborhoodDeliveryMapStates
{
    public const string Available = "Available";
    public const string NotRequested = "NotRequested";
    public const string Unavailable = "Unavailable";
    public const string NotAssigned = "NotAssigned";
    public const string Closed = "Closed";
    public const string AssignmentEvidenceMissing = "AssignmentEvidenceMissing";
    public const string NoRecentLocation = "NoRecentLocation";
}

public static class NeighborhoodDeliveryMapStages
{
    public const string Pickup = "Pickup";
    public const string Delivery = "Delivery";
    public const string Overview = "Overview";
    public const string Closed = "Closed";
}
