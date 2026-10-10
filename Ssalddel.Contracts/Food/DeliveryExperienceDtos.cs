namespace Ssalddel.Contracts.Food;

/// <summary>실제 방문 횟수가 아닌, 앱 완료 근거로 확인한 음식 배달 건수입니다.</summary>
public sealed class DeliveryExperienceSummaryDto
{
    public string MetricCode { get; set; } = string.Empty;
    public string PlaceKind { get; set; } = string.Empty;
    public string PlaceDisplayName { get; set; } = string.Empty;
    public long? CompletedCount { get; set; }
    public string EvidenceStatusCode { get; set; } = string.Empty;
    public DateTime? AsOfUtc { get; set; }
    public string ExecutionModeCode { get; set; } = string.Empty;
}

public sealed class DeliveryExperienceSharingPreferenceDto
{
    public bool Enabled { get; set; }
    public bool ShareWithCurrentParticipants { get; set; }
    public long Revision { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public sealed class DeliveryExperienceSharingPreferenceRequest
{
    public bool ShareWithCurrentParticipants { get; set; }
    public long ExpectedRevision { get; set; }
}

/// <summary>주소 검색 결과를 선택한 공식 식별자. 서버가 원천 결과와 다시 대조합니다.</summary>
public sealed class FoodDeliveryDestinationIdentityRequest
{
    public string BuildingManagementNumber { get; set; } = string.Empty;
    public string RoadNameCode { get; set; } = string.Empty;
    public string? ApartmentComplexCode { get; set; }
}
