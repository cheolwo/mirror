using Ssalddel.Contracts.Shipper.Request;

namespace Ssalddel.Contracts.Common.Community;

public static class NeighborhoodMemberDeliveryRoutes
{
    public const string Api = NeighborhoodDeliveryRoutes.Api + "/member";
    public const string Home = NeighborhoodDeliveryRoutes.Home + "/participation";
    public const string Offers = NeighborhoodDeliveryRoutes.Home + "/offers";
    public const string Work = NeighborhoodDeliveryRoutes.Home + "/work";
    public static string Offer(string id) => Offers + "/" + Uri.EscapeDataString(id);
    public static string WorkDetail(string id) => Work + "/" + Uri.EscapeDataString(id);
    public const string NoticeVersion = "neighborhood-member-delivery-v1-2026-10-09";
}

public sealed class NeighborhoodMemberParticipationResponse
{
    public long Revision { get; set; }
    public bool ReceiveOffers { get; set; }
    public string ActivityLabel { get; set; } = "";
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal RadiusKm { get; set; } = 3;
    public string VehicleType { get; set; } = "오토바이";
    public decimal MaxWeightKg { get; set; } = 10;
    public bool Enabled { get; set; }
    public string BlockCode { get; set; } = "";
}

public sealed class NeighborhoodMemberParticipationSaveRequest
{
    public Guid ClientRequestId { get; set; }
    public long ExpectedRevision { get; set; }
    public bool ReceiveOffers { get; set; }
    public string ActivityLabel { get; set; } = "";
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal RadiusKm { get; set; } = 3;
    public string VehicleType { get; set; } = "오토바이";
    public decimal MaxWeightKg { get; set; } = 10;
}

public sealed class NeighborhoodMemberOfferResponse
{
    public string OfferId { get; set; } = "";
    public string RequestId { get; set; } = "";
    public long Revision { get; set; }
    public string StateCode { get; set; } = "Pending";
    public string CargoName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal WeightKg { get; set; }
    public string PickupArea { get; set; } = "";
    public string DropoffArea { get; set; } = "";
    public decimal? DistanceKm { get; set; }
    public decimal FareKrw { get; set; }
    public decimal PlatformFeeKrw { get; set; }
    public decimal DispatchFeeKrw { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public bool CanAccept { get; set; }
}

public class NeighborhoodMemberCommandRequest
{
    public Guid ClientRequestId { get; set; }
    public long ExpectedRevision { get; set; }
}

public sealed class NeighborhoodMemberAcceptRequest : NeighborhoodMemberCommandRequest
{
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal AccuracyM { get; set; }
    public DateTime LocationAtUtc { get; set; }
}

public sealed class NeighborhoodMemberEvidenceRequest : NeighborhoodMemberCommandRequest
{
    public string StageCode { get; set; } = "Pickup";
    public string ContentBase64 { get; set; } = "";
    public string ContentType { get; set; } = "image/jpeg";
}

public sealed class NeighborhoodMemberEvidenceReceiptResponse
{
    public string RequestId { get; set; } = "";
    public Guid ClientRequestId { get; set; }
    public string EvidenceId { get; set; } = "";
    public string StageCode { get; set; } = "";
    public string StateCode { get; set; } = "";
    public bool Committed { get; set; }
    public long WorkRevision { get; set; }
}

public sealed class NeighborhoodMemberIssueRequest : NeighborhoodMemberCommandRequest
{
    public string Reason { get; set; } = "";
}

public sealed class NeighborhoodMemberRecoveryRequest : NeighborhoodMemberCommandRequest
{
    public string SupportCaseId { get; set; } = "";
    public string ResolutionCode { get; set; } = "Resume";
}
public sealed class NeighborhoodMemberRecoveryResponse
{
    public string RequestId { get; set; } = "";
    public long Revision { get; set; }
    public string StateCode { get; set; } = "";
}

/// <summary>종결 지원 사건에 결속한 관리자 복구 판단입니다. 주소·연락처·사진은 제공하지 않습니다.</summary>
public sealed class NeighborhoodMemberRecoveryContextResponse
{
    public string RequestId { get; set; } = "";
    public string SupportCaseId { get; set; } = "";
    public long Revision { get; set; }
    public string StateCode { get; set; } = "";
    public string? IssueCode { get; set; }
    public bool CanResume { get; set; }
    public bool CanCancelBeforePickup { get; set; }
    public string? BlockReason { get; set; }
    /// <summary>같은 운영자의 원 요청 번호·본문 해시가 저장된 복구 영수증과 일치한 경우만 제공합니다.</summary>
    public Guid? AppliedClientRequestId { get; set; }
    public string? AppliedResolutionCode { get; set; }
}

public sealed class NeighborhoodMemberWorkResponse
{
    public string RequestId { get; set; } = "";
    public long Revision { get; set; }
    public string StateCode { get; set; } = "Assigned";
    public string CargoName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal WeightKg { get; set; }
    public decimal FareKrw { get; set; }
    public decimal? DistanceKm { get; set; }
    public decimal PlatformFeeKrw { get; set; }
    public decimal DispatchFeeKrw { get; set; }
    public LocationContactDTO? Pickup { get; set; }
    public LocationContactDTO? Dropoff { get; set; }
    public string? RequestNotes { get; set; }
    /// <summary>사진 유무와 별개인 서버 허용 촬영 단계. 빈 값이면 촬영할 수 없습니다.</summary>
    public string EvidenceStageCode { get; set; } = "";
    public bool CanPickup { get; set; }
    public bool CanDeliver { get; set; }
    public string? PickupEvidenceId { get; set; }
    public string? DeliveryEvidenceId { get; set; }
    public string? IssueCode { get; set; }
    public string? SupportCaseId { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    /// <summary>전달 완료와 별개인 협업 상대방의 실제 수령 확인 시각.</summary>
    public DateTime? ReceiptConfirmedAtUtc { get; set; }
}
