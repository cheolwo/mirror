namespace Hongdal.Contracts.Common.Community;

public sealed class PlatformCommunityPostListResponse
{
    public IReadOnlyList<PlatformCommunityPostResponse> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class PlatformCommunityBoardListResponse
{
    public IReadOnlyList<PlatformCommunityBoardResponse> Items { get; set; } = [];
}

public sealed class PlatformCommunityBoardResponse
{
    public long Id { get; set; }
    public string AppKey { get; set; } = string.Empty;
    public string BoardKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public string RequestReason { get; set; } = string.Empty;
    public string Status { get; set; } = PlatformCommunityBoardRequestStatuses.Pending;
    public string StatusName { get; set; } = string.Empty;
    public string? OperatorMemo { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
}

public sealed class PlatformCommunityBoardCreateRequest
{
    public string AppKey { get; set; } = "platform";
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public string RequestReason { get; set; } = string.Empty;
}

public sealed class PlatformCommunityBoardReviewRequest
{
    public string OperatorMemo { get; set; } = string.Empty;
}

public static class PlatformCommunityBoardRequestStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

public sealed class PlatformCommunityPostResponse
{
    public long Id { get; set; }
    public string AppKey { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string WorkflowTag { get; set; } = string.Empty;
    public string RoleTag { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string OriginalLanguageCode { get; set; } = CommunityDisplayLanguageCodes.Korean;
    public string? SharedLinkUrl { get; set; }
    public PlatformCommunityPostSalesOfferResponse? SalesOffer { get; set; }
    public string? 커뮤니티원장Id { get; set; }
    public PlatformCommunityPostLedgerContextResponse? 원장Context { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public bool IsAuthorDisplayCountryPublic { get; set; }
    public string? AuthorDisplayCountryCode { get; set; }
    public string? AuthorDisplayCountryName { get; set; }
    public bool IsSystemGenerated { get; set; }
    public string? SystemPostKind { get; set; }
    public string? PrivacyNotice { get; set; }
    public bool IsReportBoardPost { get; set; }
    public string ReporterDisplayName { get; set; } = string.Empty;
    public string ReportedDisplayName { get; set; } = string.Empty;
    public string ViewerReportRole { get; set; } = PlatformCommunityReportViewerRoles.Observer;
    public bool IsReportSubjectMasked { get; set; }
    public bool IsOperatorPinned { get; set; }
    public DateTime? OperatorPinnedAtUtc { get; set; }
    public bool IsCommunityMomentumPromoted { get; set; }
    public string? CommunityMomentumCode { get; set; }
    public string? CommunityMomentumMessage { get; set; }
    public int CommunityMomentumRoleParticipantCount { get; set; }
    public DateTime? CommunityMomentumUpdatedAtUtc { get; set; }
    public int RecommendationCount { get; set; }
    public int CommentCount { get; set; }
    public DateTime? LastEngagedAtUtc { get; set; }
    public bool IsTrending { get; set; }
    public string PublicationStatusCode { get; set; } = PlatformCommunityPostPublicationStatuses.Published;
    public DateTime? ScheduledPublishAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public int PublicationAttemptCount { get; set; }
    public string? PublicationLastError { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public IReadOnlyList<PlatformCommunityPostAttachmentResponse> Attachments { get; set; } = [];
    public IReadOnlyList<PlatformCommunityPostCommentResponse> RecentComments { get; set; } = [];
}

public static class PlatformCommunityPostPublicationStatuses
{
    public const string Scheduled = "scheduled";
    public const string Publishing = "publishing";
    public const string Published = "published";
    public const string Cancelled = "cancelled";
    public const string Failed = "failed";

    public static bool IsSupported(string? value)
        => value is Scheduled or Publishing or Published or Cancelled or Failed;
}

public static class PlatformCommunityPostSchedulePolicy
{
    public static readonly TimeSpan MinimumLeadTime = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan MaximumLeadTime = TimeSpan.FromDays(365);
}

public static class PlatformCommunitySystemPostKinds
{
    public const string LedgerCompletion = "ledger-completion";
    public const string KamisPriceBrief = "kamis-price-brief";
    public const string Reflection = "reflection";
    public const string ActivityDigest = "activity-digest";
    public const string PrajnaContent = "prajna-content";
    public const string AutomatedEditorial = "automated-editorial";
}

public static class PlatformCommunityPostCategories
{
    public const string Vow = "서원";
    public const string General = "자유·생활";
    public const string Sales = "판매·공급";
    public const string ReportDispute = "신고/분쟁";
}

public static class PlatformCommunityPostCategoryPolicy
{
    public static string Resolve(string? requestedCategory, bool hasSalesOffer)
        => CommunityBoardCatalog.ResolveCanonicalCategory(
            hasSalesOffer
                ? PlatformCommunityPostCategories.Sales
                : requestedCategory);
}

public sealed class PlatformCommunityPostCreateRequest
{
    public string AppKey { get; set; } = "platform";
    public string Category { get; set; } = PlatformCommunityPostCategories.General;
    public string WorkflowTag { get; set; } = "국내 화물 운송";
    public string RoleTag { get; set; } = "플랫폼 구성원";
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? OriginalLanguageCode { get; set; }
    public string? SharedLinkUrl { get; set; }
    public PlatformCommunityPostSalesOfferRequest? SalesOffer { get; set; }
    public string? 커뮤니티원장Id { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public bool IsAuthorDisplayCountryPublic { get; set; }
    public string? AuthorDisplayCountryCode { get; set; }
    public string? AuthorDisplayCountryName { get; set; }
    public bool IsReportBoardPost { get; set; }
    public string? ReporterDisplayName { get; set; }
    public string? ReportedDisplayName { get; set; }
    public string Password { get; set; } = string.Empty;
}

public sealed class PlatformCommunityPostScheduleCreateRequest
{
    public PlatformCommunityPostCreateRequest Post { get; set; } = new();

    public DateTime ScheduledPublishAtUtc { get; set; }
}

public sealed class PlatformCommunityPostAudioResponse
{
    public long PostId { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsReady { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string VoiceId { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public IReadOnlyList<PlatformCommunityPostAudioSegmentResponse> Segments { get; set; } = [];
}

public sealed class PlatformCommunityPostTranslationResponse
{
    public long PostId { get; set; }
    public string SourceLanguageCode { get; set; } = CommunityDisplayLanguageCodes.Korean;
    public string TargetLanguageCode { get; set; } = CommunityDisplayLanguageCodes.English;
    public string OriginalTitle { get; set; } = string.Empty;
    public string OriginalBody { get; set; } = string.Empty;
    public string TranslatedTitle { get; set; } = string.Empty;
    public string TranslatedBody { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public bool IsMachineTranslated { get; set; }
    public bool IsCached { get; set; }
    public bool IsHumanReviewed { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class PlatformCommunityPostAudioSegmentResponse
{
    public int Sequence { get; set; }
    public int CharacterCount { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string DownloadPath { get; set; } = string.Empty;
}

public sealed class PlatformCommunityPostUpdateRequest
{
    public string Category { get; set; } = PlatformCommunityPostCategories.General;
    public string WorkflowTag { get; set; } = "국내 화물 운송";
    public string RoleTag { get; set; } = "플랫폼 구성원";
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? OriginalLanguageCode { get; set; }
    public string? SharedLinkUrl { get; set; }
    public PlatformCommunityPostSalesOfferRequest? SalesOffer { get; set; }
    public string? 커뮤니티원장Id { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public bool IsAuthorDisplayCountryPublic { get; set; }
    public string? AuthorDisplayCountryCode { get; set; }
    public string? AuthorDisplayCountryName { get; set; }
    public bool IsReportBoardPost { get; set; }
    public string? ReporterDisplayName { get; set; }
    public string? ReportedDisplayName { get; set; }
    public string Password { get; set; } = string.Empty;
}

public sealed class PlatformCommunityPostSalesOfferRequest
{
    public string ProductTitle { get; set; } = string.Empty;
    public decimal AvailableQuantity { get; set; } = 1;
    public string QuantityUnit { get; set; } = "개";
    public decimal UnitPrice { get; set; }
    public string CurrencyCode { get; set; } = "KRW";
    public IReadOnlyList<string> AcceptedPaymentMethods { get; set; } =
        [PlatformCommunitySalesPaymentMethodCodes.DirectCash];
    public bool AllowsGroupPurchase { get; set; } = true;
    public string Status { get; set; } = PlatformCommunitySalesOfferStatuses.Open;
}

public sealed class PlatformCommunityPostSalesOfferResponse
{
    public string ProductTitle { get; set; } = string.Empty;
    public decimal AvailableQuantity { get; set; }
    public string QuantityUnit { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public IReadOnlyList<string> AcceptedPaymentMethods { get; set; } = [];
    public bool AllowsGroupPurchase { get; set; }
    public string Status { get; set; } = PlatformCommunitySalesOfferStatuses.Open;
}

public static class PlatformCommunitySalesOfferStatuses
{
    public const string Open = "open";
    public const string SoldOut = "sold-out";
    public const string Closed = "closed";

    public static IReadOnlyList<string> All { get; } = [Open, SoldOut, Closed];
}

public static class PlatformCommunitySalesPaymentMethodCodes
{
    public const string TossPayments = "platform.toss-payments";
    public const string NaverPay = "platform.naver-pay";
    public const string PayPal = "platform.paypal";
    public const string DirectCash = "direct.cash";

    public static IReadOnlyList<string> All { get; } =
        [TossPayments, NaverPay, PayPal, DirectCash];
}

public sealed class PlatformCommunityPostLedgerContextResponse
{
    public string 원장Id { get; set; } = string.Empty;
    public long Revision { get; set; }
    public string 원장템플릿Key { get; set; } = string.Empty;
    public string 원장템플릿명 { get; set; } = string.Empty;
    public string 제목 { get; set; } = string.Empty;
    public string 상태 { get; set; } = string.Empty;
    public string 현재단계 { get; set; } = string.Empty;
    public string 처리체계명 { get; set; } = string.Empty;
    public string 업무분류Code { get; set; } = string.Empty;
    public string 업무분류명 { get; set; } = string.Empty;
    public string 기능설정Key { get; set; } = string.Empty;
    public bool 기능활성화여부 { get; set; }
    public bool 상세조회가능여부 { get; set; }
    public bool 참여요청필요여부 { get; set; }
    public bool 재사용허용여부 { get; set; }
    public bool 재공유허용여부 { get; set; }
    public bool 역할범위조회여부 { get; set; }
    public string 접근역할Code { get; set; } = string.Empty;
    public string 접근역할명 { get; set; } = string.Empty;
    public bool 역할권한관리가능여부 { get; set; }
    public IReadOnlyList<string> 조회가능노드Ids { get; set; } = [];
    public IReadOnlyList<string> 편집가능노드Ids { get; set; } = [];
    public bool 운송주선가능여부 { get; set; }
    public DiagramSnapshotDto? 다이어그램 { get; set; }
    public IReadOnlyList<PlatformCommunityLedgerBlockResponse> 블록목록 { get; set; } = [];
    public IReadOnlyList<string> 가능한행동목록 { get; set; } = [];
    public IReadOnlyList<PlatformCommunityLedgerNodeActionResponse> 노드행동목록 { get; set; } = [];
    public IReadOnlyList<PlatformCommunityIncludedLedgerResponse> 포함원장목록 { get; set; } = [];
}

public sealed class PlatformCommunityIncludedLedgerResponse
{
    public string 원장Id { get; set; } = string.Empty;
    public string 원장템플릿Key { get; set; } = string.Empty;
    public string 원장템플릿명 { get; set; } = string.Empty;
    public string 역할 { get; set; } = string.Empty;
    public bool 필수여부 { get; set; }
    public int 표시순서 { get; set; }
    public string 조회상태 { get; set; } = "정상";
    public bool 접근가능여부 { get; set; }
    public PlatformCommunityPostLedgerContextResponse? 원장 { get; set; }
    public IReadOnlyList<PlatformCommunityIncludedLedgerResponse> 포함원장목록 { get; set; } = [];
}

public sealed class PlatformCommunityLedgerBlockResponse
{
    public string 블록Id { get; set; } = string.Empty;
    public string 블록유형 { get; set; } = string.Empty;
    public string 제목 { get; set; } = string.Empty;
    public string? 상태 { get; set; }
    public IReadOnlyList<PlatformCommunityLedgerBlockAssigneeResponse> 담당자목록 { get; set; } = [];
    public IReadOnlyDictionary<string, string> 항목 { get; set; } = new Dictionary<string, string>();
}

public sealed class PlatformCommunityLedgerBlockAssigneeResponse
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string RoleLabel { get; set; } = string.Empty;
    public string ResponsibilityType { get; set; } = CommunityLedgerBlockResponsibilityTypes.Primary;
    public string ResponsibilityName { get; set; } = "주담당";
}

public sealed class PlatformCommunityLedgerNodeActionResponse
{
    public string 행동Code { get; set; } = string.Empty;
    public string 블록Id { get; set; } = string.Empty;
    public string 표시명 { get; set; } = string.Empty;
    public string 설명 { get; set; } = string.Empty;
    public string ApiEndpointKey { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = "POST";
    public string 실행대상Id { get; set; } = string.Empty;
    public string 현재상태 { get; set; } = string.Empty;
    public bool 실행가능여부 { get; set; }
    public bool 확인필요여부 { get; set; } = true;
    public bool 사진필수여부 { get; set; }
    public string? 비활성사유 { get; set; }
}

public static class CommunityLedgerNodeActionCodes
{
    public const string TransportArrivePickup = "TransportArrivePickup";
    public const string TransportCompletePickup = "TransportCompletePickup";
}

public sealed class PlatformCommunityPostLedgerChoiceResponse
{
    public string 원장Id { get; set; } = string.Empty;
    public string 원장템플릿Key { get; set; } = string.Empty;
    public string 원장템플릿명 { get; set; } = string.Empty;
    public string 제목 { get; set; } = string.Empty;
    public string 상태 { get; set; } = string.Empty;
    public string 현재단계 { get; set; } = string.Empty;
    public string 업무분류명 { get; set; } = string.Empty;
    public string WorkflowTag { get; set; } = string.Empty;
    public bool 내가만든원장 { get; set; }
    public bool 내접근원장여부 { get; set; }
    public bool 커뮤니티공유여부 { get; set; }
    public bool 재사용허용여부 { get; set; }
    public bool 재공유허용여부 { get; set; }
    public string 참여역할 { get; set; } = string.Empty;
    public DateTime 수정시각Utc { get; set; }
}

public static class PlatformCommunityReportViewerRoles
{
    public const string Observer = "Observer";
    public const string Reporter = "Reporter";
    public const string Reported = "Reported";
    public const string Operator = "Operator";
}

public sealed class PlatformCommunityPostPasswordRequest
{
    public string Password { get; set; } = string.Empty;
}

public sealed class PlatformCommunityOperatorHiddenRequest
{
    public bool IsOperatorHidden { get; set; }
}

public sealed class PlatformCommunityPostOperatorPinRequest
{
    public bool IsOperatorPinned { get; set; }
}

public sealed class PlatformCommunityPostRecommendationRequest
{
    public string RecommenderKey { get; set; } = string.Empty;
}

public sealed class PlatformCommunityPostCommentCreateRequest
{
    public string Nickname { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public sealed class PlatformCommunityPostCommentResponse
{
    public long Id { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int ReportCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class PlatformCommunityPostAttachmentResponse
{
    public long Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int CommentCount { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public IReadOnlyList<PlatformCommunityPostAttachmentCommentResponse> RecentComments { get; set; } = [];
}

public sealed class PlatformCommunityPostAttachmentCommentCreateRequest
{
    public string Nickname { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public sealed class PlatformCommunityPostAttachmentCommentResponse
{
    public long Id { get; set; }
    public long AttachmentId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int ReportCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
