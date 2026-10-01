using Hongdal.Contracts.Common.ContractManagement;
using Hongdal.Contracts.Common.Privacy;
using System.Text.Json.Serialization;

namespace Hongdal.Contracts.Common.Community;

public sealed class CommunityVoteCreateRequest
{
    public string AppKey { get; set; } = "platform";

    public string CommunityScope { get; set; } = "platform";

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string VoteKind { get; set; } = CommunityVoteKindCodes.General;

    public long? SourcePostId { get; set; }

    public string? CommunityLedgerId { get; set; }

    public IReadOnlyList<string> Options { get; set; } = [];

    public IReadOnlyList<CommunityVoteOptionCreateRequest> StructuredOptions { get; set; } = [];

    public bool AllowMultipleSelection { get; set; }

    public bool ResolutionDocumentEnabled { get; set; }

    public bool SignatureRequired { get; set; }

    public DateTime? ClosesAtUtc { get; set; }

    public string CreatedByDisplayName { get; set; } = string.Empty;

    public CommunityGroupPurchaseVoteSettingsRequest? GroupPurchase { get; set; }
}

public sealed class CommunityVoteOptionCreateRequest
{
    public string Text { get; set; } = string.Empty;

    public string ProductKey { get; set; } = string.Empty;

    public string HsCode { get; set; } = string.Empty;

    public string TemperatureCode { get; set; } = string.Empty;

    public string LogisticsMode { get; set; } = string.Empty;

    public string QuantityUnit { get; set; } = string.Empty;
}

public sealed class CommunityVoteCastRequest
{
    [IsmsPProtectedData(
        PersonalDataFieldKey.DisplayName,
        "커뮤니티 투표 참여자 표시",
        ProtectionNote = "투표 결과 공개에는 실명 대신 표시명 또는 해시 기반 참여자 키 사용")]
    public string VoterDisplayName { get; set; } = string.Empty;

    public string VoterKey { get; set; } = string.Empty;

    [JsonIgnore]
    public string? AuthenticatedUserId { get; set; }

    public IReadOnlyList<string> OptionIds { get; set; } = [];

    public int RequestedQuantity { get; set; } = 1;

    public string ParticipationMethodCode { get; set; } = string.Empty;

    public string? CommunityMembershipReference { get; set; }

    public string? ServiceAreaReference { get; set; }

    public string? PickupPointId { get; set; }

    public bool AllowNearbyPickupPointFallback { get; set; }
}

public sealed class CommunityInterestVotePromotionSnapshot
{
    public Guid VoteId { get; set; }
    public long SourcePostId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CommunityLedgerId { get; set; }
    public int ParticipantCount { get; set; }
    public string EvidenceSnapshotHash { get; set; } = string.Empty;
    public IReadOnlyList<CommunityInterestVoteParticipantSnapshot> Participants { get; set; } = [];
    public IReadOnlyDictionary<string, int> RoleCounts { get; set; }
        = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
}

public sealed class CommunityInterestVoteParticipantSnapshot
{
    public string ParticipantReference { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public IReadOnlyList<string> RoleCodes { get; set; } = [];
}

public sealed class CommunityGroupPurchaseVoteSettingsRequest
{
    public string ProposerRoleCode { get; set; } = CommunityGroupPurchaseProposerRoleCodes.GroupPurchaseRepresentative;

    public string SellerCountryCode { get; set; } = string.Empty;

    public string ShipFromCountryCode { get; set; } = string.Empty;

    public string DeliveryCountryCode { get; set; } = string.Empty;

    public string CustomsClearanceStatusCode { get; set; }
        = CommunityGroupPurchaseCustomsClearanceStatusCodes.Unknown;

    public string ParticipationPolicyCode { get; set; } = CommunityVoteParticipationPolicyCodes.Hybrid;

    public string HsCode { get; set; } = string.Empty;

    public string TemperatureCode { get; set; } = "상온";

    public string LogisticsMode { get; set; } = "LCL";

    public string QuantityUnit { get; set; } = "개";

    /// <summary>
    /// 시장가격 비교에 사용하는 표준화된 공동구매 목표단가입니다.
    /// 포장 단위 가격은 실제 중량으로 나누어 원/kg 기준으로 저장합니다.
    /// </summary>
    public decimal? TargetUnitPriceKrwPerKg { get; set; }

    public string ServiceAreaKey { get; set; } = string.Empty;

    public string ServiceAreaLabel { get; set; } = string.Empty;

    public int? RadiusMeters { get; set; }

    public int MinimumParticipantCount { get; set; } = 1;

    public int MinimumTotalQuantity { get; set; } = 1;

    public IReadOnlyList<CommunityVotePickupPointRequest> PickupPoints { get; set; } = [];
}

public sealed class CommunityVotePickupPointRequest
{
    public string PickupPointId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string AddressSummary { get; set; } = string.Empty;

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public string StorageTypeCode { get; set; } = CommunityVotePickupStorageTypeCodes.Ambient;

    public DateTime? PickupStartsAtUtc { get; set; }

    public DateTime? PickupEndsAtUtc { get; set; }

    public int? CapacityQuantity { get; set; }

    public int? MinimumParticipantCount { get; set; }

    public int? MinimumTotalQuantity { get; set; }

    public decimal PickupFee { get; set; }
}

public sealed class CommunityVoteCloseRequest
{
    public string ClosedByDisplayName { get; set; } = string.Empty;
}

public sealed class CommunityVoteResolutionDraftRequest
{
    public string DocumentTitle { get; set; } = string.Empty;

    public string ResolutionText { get; set; } = string.Empty;

    public IReadOnlyList<CommunityVoteResolutionSignerRequest> RequiredSigners { get; set; } = [];

    public bool LegalReviewRequested { get; set; } = true;
}

public sealed class CommunityVoteResolutionSignerRequest
{
    public string PartyId { get; set; } = string.Empty;

    public string RoleCode { get; set; } = "CommunityParticipant";

    [IsmsPProtectedData(
        PersonalDataFieldKey.DisplayName,
        "커뮤니티 결의문 서명자 표시",
        IsContractData = true,
        ProtectionNote = "결의문 서명 요청 목록에는 표시명과 역할 중심으로 노출")]
    public string SignerDisplayName { get; set; } = string.Empty;
}

public sealed class CommunityVoteResolutionSignRequest
{
    public string PartyId { get; set; } = string.Empty;

    public string SignerDisplayName { get; set; } = string.Empty;

    public string SignatureMethodCode { get; set; } = ContractSignatureMethodCode.PlatformClickSign;

    public string ConsentText { get; set; } = string.Empty;

    public string SignatureEvidencePayload { get; set; } = string.Empty;

    public string? ClientIpHash { get; set; }
}

public sealed class CommunityVoteResolutionReadyToSignRequest
{
    public string ReviewedByDisplayName { get; set; } = string.Empty;

    public string ReviewMemo { get; set; } = string.Empty;
}

public sealed class CommunityVoteListResponse
{
    public IReadOnlyList<CommunityVoteResponse> Items { get; set; } = [];
}

public sealed class CommunityVoteResponse
{
    public Guid Id { get; set; }

    public string AppKey { get; set; } = string.Empty;

    public string CommunityScope { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string VoteKind { get; set; } = CommunityVoteKindCodes.General;

    public long? SourcePostId { get; set; }

    public string? CommunityLedgerId { get; set; }

    public string Status { get; set; } = CommunityVoteStatusCodes.Open;

    public bool AllowMultipleSelection { get; set; }

    public bool ResolutionDocumentEnabled { get; set; }

    public bool SignatureRequired { get; set; }

    public string CreatedByDisplayName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ClosesAtUtc { get; set; }

    public DateTime? ClosedAtUtc { get; set; }

    public int TotalVoteCount { get; set; }

    public IReadOnlyList<CommunityVoteOptionResponse> Options { get; set; } = [];

    public CommunityGroupPurchaseVoteResponse? GroupPurchase { get; set; }

    public CommunityVoteResolutionDocumentResponse? ResolutionDocument { get; set; }
}

public sealed class CommunityVoteOptionResponse
{
    public string OptionId { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public string ProductKey { get; set; } = string.Empty;

    public string HsCode { get; set; } = string.Empty;

    public string TemperatureCode { get; set; } = string.Empty;

    public string LogisticsMode { get; set; } = string.Empty;

    public string QuantityUnit { get; set; } = string.Empty;

    public int VoteCount { get; set; }

    public int RequestedQuantity { get; set; }

    public bool IsWinningOption { get; set; }
}

public sealed class CommunityGroupPurchaseVoteResponse
{
    public string ProposerRoleCode { get; set; } = CommunityGroupPurchaseProposerRoleCodes.GroupPurchaseRepresentative;

    public string AgreementPolicyCode { get; set; } = CommunityGroupPurchaseAgreementPolicy.PolicyCode;

    public string ProposalOriginLegalEffectNotice { get; set; }
        = CommunityGroupPurchaseAgreementPolicy.FullLegalEffectNotice;

    public string OperatingMarketCountryCode { get; set; }
        = CommunityGroupPurchaseTradeRoutePolicy.KoreaCountryCode;

    public string SellerCountryCode { get; set; } = string.Empty;

    public string ShipFromCountryCode { get; set; } = string.Empty;

    public string DeliveryCountryCode { get; set; } = string.Empty;

    public string CustomsClearanceStatusCode { get; set; }
        = CommunityGroupPurchaseCustomsClearanceStatusCodes.Unknown;

    public string TradeRouteCode { get; set; } = string.Empty;

    public bool IsGroupImportCandidate { get; set; }

    public bool RequiresTradeRouteReview { get; set; }

    public string RecommendedLedgerTemplateKey { get; set; } = string.Empty;

    public IReadOnlyList<string> TradeRouteReasonCodes { get; set; } = [];

    public IReadOnlyList<string> TradeRouteMissingFieldCodes { get; set; } = [];

    public IReadOnlyList<string> TradeRouteInvalidFieldCodes { get; set; } = [];

    public string ParticipationPolicyCode { get; set; } = string.Empty;

    public string HsCode { get; set; } = string.Empty;

    public string TemperatureCode { get; set; } = string.Empty;

    public string LogisticsMode { get; set; } = string.Empty;

    public string QuantityUnit { get; set; } = string.Empty;

    public decimal? TargetUnitPriceKrwPerKg { get; set; }

    public string ServiceAreaKey { get; set; } = string.Empty;

    public string ServiceAreaLabel { get; set; } = string.Empty;

    public int? RadiusMeters { get; set; }

    public int MinimumParticipantCount { get; set; }

    public int MinimumTotalQuantity { get; set; }

    public int TotalRequestedQuantity { get; set; }

    public int UnassignedPickupParticipantCount { get; set; }

    public int UnassignedPickupQuantity { get; set; }

    public int DemandHandoffPendingCount { get; set; }

    public int DemandHandoffFailedCount { get; set; }

    public bool IsMinimumReached { get; set; }

    public IReadOnlyList<CommunityVotePickupPointResponse> PickupPoints { get; set; } = [];
}

public sealed class CommunityVotePickupPointResponse
{
    public string PickupPointId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string AddressSummary { get; set; } = string.Empty;

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public string StorageTypeCode { get; set; } = string.Empty;

    public DateTime? PickupStartsAtUtc { get; set; }

    public DateTime? PickupEndsAtUtc { get; set; }

    public int? CapacityQuantity { get; set; }

    public int? MinimumParticipantCount { get; set; }

    public int? MinimumTotalQuantity { get; set; }

    public decimal PickupFee { get; set; }

    public int ParticipantCount { get; set; }

    public int RequestedQuantity { get; set; }

    public bool IsMinimumReached { get; set; }

    public bool IsCapacityReached { get; set; }
}

public sealed class CommunityVoteResolutionDocumentResponse
{
    public Guid Id { get; set; }

    public Guid VoteId { get; set; }

    public string DocumentNumber { get; set; } = string.Empty;

    public string DocumentTitle { get; set; } = string.Empty;

    public string ResolutionText { get; set; } = string.Empty;

    public string DocumentHash { get; set; } = string.Empty;

    public string Status { get; set; } = CommunityVoteResolutionStatusCodes.LegalReviewRequired;

    public string LegalEffectNotice { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public ContractElectronicSignaturePlan? SignaturePlan { get; set; }
}

public static class CommunityVoteStatusCodes
{
    public const string Open = "Open";

    public const string Closed = "Closed";

    public const string ResolutionDrafted = "ResolutionDrafted";
}

public static class CommunityVoteKindCodes
{
    public const string General = "General";

    public const string CollectiveActionInterest = "CollectiveActionInterest";

    public const string GroupPurchaseDemand = "GroupPurchaseDemand";
}

public static class CommunityVoteParticipationPolicyCodes
{
    public const string CommunityOnly = "CommunityOnly";

    public const string ServiceAreaOnly = "ServiceAreaOnly";

    public const string PickupPoint = "PickupPoint";

    public const string Hybrid = "Hybrid";
}

public static class CommunityGroupPurchaseProposerRoleCodes
{
    public const string Producer = "Producer";

    public const string GroupPurchaseRepresentative = "GroupPurchaseRepresentative";

    public static bool IsSupported(string? value)
        => value is Producer or GroupPurchaseRepresentative;
}

public static class CommunityGroupPurchaseAgreementPolicy
{
    public const string PolicyCode = "MutualAgreementIndependentOfProposalOrigin";

    public const string ProposalOriginNotice =
        "공동구매 제안 주체 정보는 협상·모집을 시작한 운영 경로를 기록하기 위한 것으로, 제안의 선후만으로 계약상 우선권·우월적 지위 또는 권리·의무가 정해지지 않습니다.";

    public const string MutualAgreementNotice =
        "이 플랫폼에서 공동구매 제안은 비구속적 협의·모집 단계로 취급합니다. 계약상의 최종 권리·의무와 플랫폼상 계약 확정은 생산자와 공동구매 대표가 합의한 최종 계약문 및 필요한 전자서명에 따릅니다. 개별 사안의 법적 효력은 실제 의사표시, 계약 내용과 관련 법령에 따라 달라질 수 있습니다.";

    public const string FullLegalEffectNotice = ProposalOriginNotice + " " + MutualAgreementNotice;
}

public static class CommunityVoteParticipationMethodCodes
{
    public const string CommunityMember = "CommunityMember";

    public const string ServiceArea = "ServiceArea";

    public const string PickupPoint = "PickupPoint";
}

public static class CommunityVotePickupStorageTypeCodes
{
    public const string Ambient = "Ambient";

    public const string Refrigerated = "Refrigerated";

    public const string Frozen = "Frozen";
}

public static class CommunityVoteResolutionStatusCodes
{
    public const string Draft = "Draft";

    public const string LegalReviewRequired = "LegalReviewRequired";

    public const string ReadyToSign = "ReadyToSign";

    public const string PartiallySigned = "PartiallySigned";

    public const string Signed = "Signed";
}
