using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Contracts.Common.Privacy;

namespace Ssalddel.Contracts.Common.Community;

/// <summary>생활 교류와 별도로, 본인이 명시적으로 제출하는 생활 화물 배송 의뢰입니다.</summary>
public static class NeighborhoodDeliveryRoutes
{
    public const string Api = "api/v1/common/neighborhood-deliveries";
    public const string Home = "/community/exchange/deliveries";
    public const string Create = "/community/exchange/deliveries/new";
    public const string ClientRequestPrefix = "neighborhood-delivery:";
    public const string PrivacyConsentSourceCode = 신청개인정보출처Codes.생활교류;
    public static string Detail(string requestId) => $"{Home}/{Uri.EscapeDataString(requestId)}";
}

public sealed class NeighborhoodDeliveryRequest
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Ssalddel.Contracts.Common.Commerce.거래보호확인Request? CommerceProtection { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? DispatchMode { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? CollaborationId { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public long? ExpectedTermsRevision { get; set; }
    public Guid ClientRequestId { get; set; }
    public string CargoName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal WeightKg { get; set; }
    public string VehicleType { get; set; } = "오토바이";
    [IsmsPProtectedData(PersonalDataFieldKey.DetailedAddress, "생활 배송 픽업 주소 처리")]
    [IsmsPProtectedData(PersonalDataFieldKey.PhoneNumber, "생활 배송 픽업 연락처 처리")]
    [IsmsPProtectedData(PersonalDataFieldKey.LocationCoordinate, "생활 배송 경로 견적")]
    public LocationContactDTO Pickup { get; set; } = new();
    [IsmsPProtectedData(PersonalDataFieldKey.DetailedAddress, "생활 배송 전달 주소 처리")]
    [IsmsPProtectedData(PersonalDataFieldKey.PhoneNumber, "생활 배송 전달 연락처 처리")]
    [IsmsPProtectedData(PersonalDataFieldKey.LocationCoordinate, "생활 배송 경로 견적")]
    public LocationContactDTO Dropoff { get; set; } = new();
    public string? Notes { get; set; }
    public long? SourcePostId { get; set; }
    public Guid? PrivacyConsentEvidenceId { get; set; }
    public string PrivacyConsentSourceCode { get; set; } = NeighborhoodDeliveryRoutes.PrivacyConsentSourceCode;
    public bool DispatchRequested { get; set; }
    public bool DirectPaymentAgreed { get; set; }
    /// <summary>등록 직전 사용자가 확인한 서버 견적. 바뀌면 다시 확인합니다.</summary>
    public decimal? AgreedFareKrw { get; set; }
}

public sealed class NeighborhoodDeliveryQuoteResponse
{
    public 화주운송기준운임견적응답 Fare { get; set; } = new();
    public bool AutomaticDispatchEnabled { get; set; }
    public string AutomaticDispatchBlockCode { get; set; } = string.Empty;
    public string SettlementNotice { get; set; } = "배송비는 기사에게 직접 지급하기로 합의합니다. 접수는 수금이나 지급 완료를 뜻하지 않습니다.";
}

public static class NeighborhoodDeliveryProposalStates
{
    public const string RegistrationPending = "RegistrationPending";
    public const string Queued = "Queued";
    public const string AwaitingActivation = "AwaitingActivation";
    public const string Proposed = "Proposed";
    public const string Assigned = "Assigned";
    public const string Closed = "Closed";
}

public sealed class NeighborhoodDeliveryResponse
{
    public string? DispatchMode { get; set; }
    public long DispatchRevision { get; set; }
    public bool CanChangeDispatchMode { get; set; }
    public string? CollaborationId { get; set; }
    public string RequestId { get; set; } = string.Empty;
    /// <summary>서버의 보존된 신청 근거가 있는 경우에만 반환합니다. 과거 기록에서 추정하지 않습니다.</summary>
    public long? SourcePostId { get; set; }
    public string CargoName { get; set; } = string.Empty;
    public bool IdempotentReplay { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string RequestStatusCode { get; set; } = string.Empty;
    public string DispatchStatusCode { get; set; } = string.Empty;
    public string QueueStageCode { get; set; } = string.Empty;
    public string ProposalStateCode { get; set; } = NeighborhoodDeliveryProposalStates.Queued;
    public bool AutomaticDispatchEnabled { get; set; }
    public string AutomaticDispatchBlockCode { get; set; } = string.Empty;
    public decimal? FareKrw { get; set; }
    public decimal? DistanceKm { get; set; }
    public string? DistanceBasis { get; set; }
    public string? RateSource { get; set; }
    public string PaymentStatusCode { get; set; } = string.Empty;
    public string SettlementStatusCode { get; set; } = string.Empty;
    public NeighborhoodDeliveryDisclosureResponse? DriverDisclosure { get; set; }
    /// <summary>기존 본인 운송 의뢰 권한 정책을 통과한 상세 정보입니다. 공개 게시글에 연결하지 않습니다.</summary>
    public 화주운송의뢰응답 Request { get; set; } = new();
}

/// <summary>신청 동의와 별도로, 선정된 한 기사에게 해당 배송의 필요한 정보를 제공하는 동의입니다.</summary>
public static class NeighborhoodDeliveryDisclosureNotice
{
    public const string Version = "neighborhood-driver-disclosure-v1-2026-10-05";
    public const string EventType = "NeighborhoodDeliveryDriverDisclosure";
    public const string Purpose = "이 배송의 픽업·전달 및 담당자 연락";
    public const string Retention = "해당 배송이 끝나거나 동의를 철회할 때까지, 동의 시점부터 최대 72시간 동안";
    public static IReadOnlyList<string> Fields { get; } = Array.AsReadOnly(new[] { "PickupAddress", "DropoffAddress", "LocationCoordinate", "ContactName", "PhoneNumber", "DeliveryInstructions" });
}

public sealed class NeighborhoodDeliveryDisclosureRequest
{
    public Guid ClientRequestId { get; set; }
    public string ConfirmedDriverId { get; set; } = string.Empty;
    public int ExpectedRecommendationRound { get; set; }
    public bool Consented { get; set; }
    public string NoticeVersion { get; set; } = NeighborhoodDeliveryDisclosureNotice.Version;
}

public sealed class NeighborhoodDeliveryDisclosureResponse
{
    public string RequestId { get; set; } = string.Empty;
    public string? ConfirmedDriverId { get; set; }
    public string? ConfirmedDriverName { get; set; }
    public int RecommendationRound { get; set; }
    public bool Consented { get; set; }
    public bool CanRecordConsent { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public string NoticeVersion { get; set; } = NeighborhoodDeliveryDisclosureNotice.Version;
    public string Purpose { get; set; } = NeighborhoodDeliveryDisclosureNotice.Purpose;
    public IReadOnlyList<string> Fields { get; set; } = NeighborhoodDeliveryDisclosureNotice.Fields;
    public string RetentionNotice { get; set; } = NeighborhoodDeliveryDisclosureNotice.Retention;
}
