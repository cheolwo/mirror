namespace Ssalddel.Contracts.Common.Community;

/// <summary>생활 협업은 당사자 합의 기록이며 전문 업무 권한·결제·배송 수행 증명이 아닙니다.</summary>
public static class NeighborhoodCollaborationRoutes
{
    public const string Api = "api/v1/common/neighborhood-collaborations";
    public const string Home = "/community/exchange/work";
    public static string New(long postId) => $"{Home}/new?postId={postId}";
    public static string Detail(string stableId) => $"{Home}/{Uri.EscapeDataString(stableId)}";
}

public static class NeighborhoodCollaborationKinds
{
    public const string Goods = "goods";
    public const string Food = "food";
    public const string Transport = "transport";
    public const string Storage = "storage";
    public static bool IsKnown(string? kind) => kind is Goods or Food or Transport or Storage;
}

public static class NeighborhoodCollaborationStates
{
    public const string Requested = "requested";
    public const string Agreed = "agreed";
    public const string InProgress = "in-progress";
    public const string CompletionProposed = "completion-proposed";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
    public const string Rejected = "rejected";
    public const string Expired = "expired";
    public static bool IsTerminal(string? state) => state is Completed or Cancelled or Rejected or Expired;
}

public static class NeighborhoodCollaborationActions
{
    public const string UpdateTerms = "update-terms";
    public const string WithdrawHandoverConsent = "withdraw-handover-consent";
    public const string HandoverInfoConsent = "handover-info-consent";
    public const string Agree = "agree";
    public const string Start = "start";
    public const string ProposeCompletion = "propose-completion";
    public const string ConfirmCompletion = "confirm-completion";
    public const string Cancel = "cancel";
    public const string Reject = "reject";
    public const string PublicHistoryConsent = "public-history-consent";
    public const string RequestParticipation = "request-participation";
    public const string AcceptParticipation = "accept-participation";
    public const string RejectParticipation = "reject-participation";
    public const string WithdrawParticipation = "withdraw-participation";
    public const string LinkDelivery = "link-delivery";
}

public static class NeighborhoodCollaborationAgreementNotice
{
    public const string Version = "neighborhood-storage-agreement-20261005-v1";
    public const string Text = "현재 보관 조건에 동의하면 협업 상대방에게 상세 인계 주소·연락처·인계 방법을 제공합니다. 공개 지도에는 동네만 표시하며 이 동의는 공개 기록·배송 기사 제공·결제 확인과 별개입니다.";
}

public sealed class NeighborhoodCollaborationTerms
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? TransferMethod { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    [Ssalddel.Contracts.Common.Privacy.IsmsPProtectedData(Ssalddel.Contracts.Common.Privacy.PersonalDataFieldKey.DetailedAddress, "협업 당사자 인계 장소")]
    public string? HandoverPlace { get; set; }
    public string Summary { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public string Unit { get; set; } = "개";
    public DateTime FromUtc { get; set; }
    public DateTime UntilUtc { get; set; }
    public decimal? AgreedCostKrw { get; set; }
    public string? Notes { get; set; }
    public string? StorageSpaceId { get; set; }
    public int? StorageQuantity { get; set; }
    public string? StorageUnit { get; set; }
    public DateTime? StorageFromUtc { get; set; }
    public DateTime? StorageUntilUtc { get; set; }
}

public sealed class NeighborhoodCollaborationCreateRequest
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Ssalddel.Contracts.Common.Commerce.거래보호확인Request? CommerceProtection { get; set; }
    public Guid ClientRequestId { get; set; }
    public long? SourcePostId { get; set; }
    public DateTime? ExpectedSourceUpdatedAtUtc { get; set; }
    public string? StorageSpaceId { get; set; }
    public long? ExpectedStorageRevision { get; set; }
    public string Kind { get; set; } = NeighborhoodCollaborationKinds.Goods;
    public NeighborhoodCollaborationTerms Terms { get; set; } = new();
}

public sealed class NeighborhoodCollaborationCommandRequest
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Ssalddel.Contracts.Common.Commerce.거래보호확인Request? CommerceProtection { get; set; }
    public Guid ClientRequestId { get; set; }
    public long ExpectedRevision { get; set; }
    public string Action { get; set; } = string.Empty;
    public NeighborhoodCollaborationTerms? Terms { get; set; }
    public string? ApplicantUserId { get; set; }
    public bool? Consented { get; set; }
    public string? LinkedDeliveryRequestId { get; set; }
    public string? PrivacyNoticeVersion { get; set; }
}

public sealed class NeighborhoodCollaborationResponse
{
    public string StableId { get; set; } = string.Empty;
    public long Revision { get; set; }
    public long TermsRevision { get; set; }
    public long? SourcePostId { get; set; }
    public string SourceTitle { get; set; } = string.Empty;
    public string? PublicNeighborhoodRegionKey { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string? ProviderRoleCode { get; set; }
    public string MyRoleCode { get; set; } = string.Empty;
    public bool IsSourceOwner { get; set; }
    public bool IsRequester { get; set; }
    public NeighborhoodCollaborationTerms? Terms { get; set; }
    public bool OwnerAgreed { get; set; }
    public bool RequesterAgreed { get; set; }
    public bool OwnerPublicHistoryConsented { get; set; }
    public bool RequesterPublicHistoryConsented { get; set; }
    public bool MyPublicHistoryConsented { get; set; }
    public bool PublicHistoryVisible { get; set; }
    public bool CompletionProposedByMe { get; set; }
    public string? LinkedDeliveryRequestId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public NeighborhoodDeliveryRequest? MyPendingDeliveryRequest { get; set; }
    public bool CanRequestDelivery { get; set; }
    public bool DeliveryRegistrationPending { get; set; }
    public bool StorageReservationPending { get; set; }
    public Guid? MyPendingStorageRequestId { get; set; }
    public string? MyPendingStorageSpaceId { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public IReadOnlyList<string> AllowedActions { get; set; } = [];
    public IReadOnlyList<NeighborhoodCollaborationParticipationResponse> Participants { get; set; } = [];
    public IReadOnlyList<NeighborhoodCollaborationHistoryResponse> History { get; set; } = [];
    public bool IdempotentReplay { get; set; }
    public string Boundary { get; set; } = "당사자 협업 기록입니다. 실제 지급·전문 업무 자격·배송 수행 완료를 증명하지 않습니다.";
}

public sealed class NeighborhoodCollaborationParticipationResponse
{
    public string ApplicantUserId { get; set; } = string.Empty;
    public string StatusCode { get; set; } = "requested";
    public bool OwnerAccepted { get; set; }
    public bool RequesterAccepted { get; set; }
    public bool IsMe { get; set; }
}

public sealed class NeighborhoodCollaborationHistoryResponse
{
    public long Revision { get; set; }
    public string Action { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string ActorRoleCode { get; set; } = string.Empty;
    public DateTime RecordedAtUtc { get; set; }
}

public sealed class NeighborhoodCollaborationListResponse
{
    public int Page { get; set; }
    public bool HasMore { get; set; }
    public IReadOnlyList<NeighborhoodCollaborationResponse> Items { get; set; } = [];
}

/// <summary>완료·모든 당사자 동의가 있을 때만 비식별 분류와 완료 일자를 공개합니다.</summary>
public sealed class NeighborhoodCollaborationPublicHistoryResponse
{
    public string Kind { get; set; } = string.Empty;
    public string? PublicNeighborhoodRegionKey { get; set; }
    public DateTime CompletedOnUtc { get; set; }
    public string Boundary { get; set; } = "상호 확인한 협업 완료 기록이며 금전 지급 증명이 아닙니다.";
}

public sealed class NeighborhoodCollaborationOpportunityResponse
{
    public string StableId { get; set; } = string.Empty;
    public long Revision { get; set; }
    public long SourcePostId { get; set; }
    public string SourceTitle { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string? PublicNeighborhoodRegionKey { get; set; }
    public string StatusCode { get; set; } = string.Empty;
}
