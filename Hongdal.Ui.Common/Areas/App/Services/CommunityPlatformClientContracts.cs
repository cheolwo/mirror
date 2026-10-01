using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Customs;
using Hongdal.Contracts.Common.Orderer;
using Hongdal.Contracts.Common.PublicData;
using Hongdal.Contracts.Common.Versioning;
using Microsoft.AspNetCore.Components.Forms;

namespace Hongdal.Ui.Common.Areas.App.Services;

public interface ICommunityPostClient
{
    Task<PlatformCommunityPostListResponse> GetPostsAsync(
        string appKey,
        CancellationToken cancellationToken = default);
    Task<PlatformCommunityPostListResponse> GetBoardPostsAsync(
        string appKey,
        string? boardKey = null,
        string? category = null,
        string? workflowTag = null,
        string? roleTag = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CommunityBoardSummaryResponse>> GetBoardSummariesAsync(
        string appKey,
        CancellationToken cancellationToken = default);
    Task<PlatformCommunityPostResponse?> GetPostAsync(
        long postId,
        CancellationToken cancellationToken = default);
    Task<PlatformCommunityPostTranslationResponse?> TranslatePostAsync(
        long postId,
        string targetLanguageCode,
        CancellationToken cancellationToken = default);
    Task<PlatformCommunityBoardResponse?> CreateBoardRequestAsync(
        PlatformCommunityBoardCreateRequest request,
        CancellationToken cancellationToken = default);
    Task<PlatformCommunityBoardResponse?> ApproveBoardAsync(
        long boardRequestId,
        string operatorMemo,
        CancellationToken cancellationToken = default);
    Task<PlatformCommunityBoardResponse?> RejectBoardAsync(
        long boardRequestId,
        string operatorMemo,
        CancellationToken cancellationToken = default);
    Task<PlatformCommunityPostResponse?> CreatePostAsync(
        PlatformCommunityPostCreateRequest request,
        CancellationToken cancellationToken = default);
    Task<PlatformCommunityPostResponse?> SchedulePostAsync(
        PlatformCommunityPostScheduleCreateRequest request,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlatformCommunityPostResponse>> GetScheduledPostsAsync(
        string? status = null,
        int take = 50,
        CancellationToken cancellationToken = default);
    Task<PlatformCommunityPostResponse?> CancelScheduledPostAsync(
        long postId,
        CancellationToken cancellationToken = default);
    Task<PlatformCommunityPostResponse?> UpdatePostAsync(
        long postId,
        PlatformCommunityPostUpdateRequest request,
        CancellationToken cancellationToken = default);
    Task<PlatformCommunityPostResponse?> SetOperatorPinAsync(
        long postId,
        bool isOperatorPinned,
        CancellationToken cancellationToken = default);
    Task<PlatformCommunityPostResponse?> RecommendAsync(
        long postId,
        string recommenderKey,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlatformCommunityPostCommentResponse>> GetCommentsAsync(
        long postId,
        CancellationToken cancellationToken = default);
    Task<PlatformCommunityPostCommentResponse?> CreateCommentAsync(
        long postId,
        PlatformCommunityPostCommentCreateRequest request,
        CancellationToken cancellationToken = default);
    Task DeleteCommentAsync(
        long postId,
        long commentId,
        string password,
        CancellationToken cancellationToken = default);
    Task ReportCommentAsync(long commentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlatformCommunityPostAttachmentCommentResponse>> GetAttachmentCommentsAsync(
        long attachmentId,
        CancellationToken cancellationToken = default);
    Task<PlatformCommunityPostAttachmentCommentResponse?> CreateAttachmentCommentAsync(
        long attachmentId,
        PlatformCommunityPostAttachmentCommentCreateRequest request,
        CancellationToken cancellationToken = default);
    Task DeleteAttachmentCommentAsync(
        long attachmentId,
        long commentId,
        string password,
        CancellationToken cancellationToken = default);
    Task ReportAttachmentCommentAsync(long commentId, CancellationToken cancellationToken = default);
    Task<PlatformCommunityPostAttachmentResponse?> UploadAttachmentAsync(
        long postId,
        string password,
        IBrowserFile file,
        long maxAllowedSize,
        CancellationToken cancellationToken = default);
}

public interface ICommunityParticipationClient
{
    Task<CommunityPostOpportunityListResponse?> GetPostOpportunitiesAsync(
        long postId,
        string displayLanguageCode = CommunityDisplayLanguageCodes.Korean,
        CancellationToken cancellationToken = default);
    Task<StartCommunityPostParticipationResponse?> StartPostParticipationAsync(
        long postId,
        StartCommunityPostParticipationRequest request,
        CancellationToken cancellationToken = default);
    Task<PromoteCommunityPostParticipationResponse?> PromotePostParticipationAsync(
        long postId,
        PromoteCommunityPostParticipationRequest request,
        CancellationToken cancellationToken = default);
    Task<JoinCommunityPostProfessionalResponse?> JoinPostProfessionalRoleAsync(
        long postId,
        JoinCommunityPostProfessionalRequest request,
        CancellationToken cancellationToken = default);
    Task<JoinCommunityPostPartyRoleResponse?> JoinPostPartyRoleAsync(
        long postId,
        JoinCommunityPostPartyRoleRequest request,
        CancellationToken cancellationToken = default);
    Task<CommunityVoteResponse?> CastCommunityVoteAsync(
        Guid voteId,
        CommunityVoteCastRequest request,
        CancellationToken cancellationToken = default);
}

public interface ICommunityLedgerClient
{
    Task<IReadOnlyList<PlatformCommunityPostLedgerChoiceResponse>> GetMyLedgersAsync(
        string? workflowTag = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlatformCommunityPostLedgerChoiceResponse>> GetSharedLedgersAsync(
        string? workflowTag = null,
        CancellationToken cancellationToken = default);
    Task<PlatformCommunityPostLedgerContextResponse?> GetLedgerContextAsync(
        string ledgerId,
        CancellationToken cancellationToken = default);
    Task<커뮤니티원장공개설정Response?> GetLedgerSharingSettingsAsync(
        string ledgerId,
        CancellationToken cancellationToken = default);
    Task<커뮤니티원장공개설정Response?> UpdateLedgerSharingSettingsAsync(
        string ledgerId,
        커뮤니티원장공개설정변경Request request,
        CancellationToken cancellationToken = default);
    Task<CommunityLedgerRoleAccessSettingsResponse?> GetLedgerRoleAccessSettingsAsync(
        string ledgerId,
        CancellationToken cancellationToken = default);
    Task<CommunityLedgerRoleAccessSettingsResponse?> UpdateLedgerRoleAccessSettingsAsync(
        string ledgerId,
        CommunityLedgerRoleAccessUpdateRequest request,
        CancellationToken cancellationToken = default);
    Task<CommunityLedgerBlockAssignmentSettingsResponse?> GetLedgerBlockAssignmentsAsync(
        string ledgerId,
        string blockId,
        CancellationToken cancellationToken = default);
    Task<CommunityLedgerBlockAssignmentSettingsResponse?> UpdateLedgerBlockAssignmentsAsync(
        string ledgerId,
        string blockId,
        CommunityLedgerBlockAssignmentUpdateRequest request,
        CancellationToken cancellationToken = default);
    Task<커뮤니티원장재사용Response?> ReuseSharedLedgerAsync(
        string ledgerId,
        string? newTitle = null,
        CancellationToken cancellationToken = default);
}

public interface ICommunityProcurementClient
{
    Task<PlatformCommunityBoardListResponse> GetBoardsAsync(
        string appKey,
        string status = PlatformCommunityBoardRequestStatuses.Approved,
        CancellationToken cancellationToken = default);
    Task<VersionFeatureFlagsResponse> GetVersionWorkflowMetadataAsync(
        CancellationToken cancellationToken = default);
    Task<GroupImportHsCodeSearchResponse> SearchGroupImportHsCodesAsync(
        string? query = null,
        int? businessCategory = null,
        int page = 1,
        int pageSize = 30,
        CancellationToken cancellationToken = default);
    Task<HsCountryImportUnitPriceSimulationResult?> GetGroupImportUnitPriceAsync(
        HsCountryMonthlyTradeUnitPriceRequest request,
        CancellationToken cancellationToken = default);
    Task<CommunityDriverAvailabilityListResponse> GetCommunityDriverAvailabilityAsync(
        string? operatingArea = null,
        CancellationToken cancellationToken = default);
    Task<CommunityDriverInquiryResponse?> CreateCommunityDriverInquiryAsync(
        Guid postId,
        CommunityDriverInquiryCreateRequest request,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CommunityDriverInquiryResponse>> GetMyCommunityDriverInquiriesAsync(
        CancellationToken cancellationToken = default);
    Task<DomesticProducerCandidateQueryResponse> GetDomesticProducerCandidatesAsync(
        Guid campaignId,
        string? search = null,
        string? regionCode = null,
        string? product = null,
        CancellationToken cancellationToken = default);
    Task<DomesticProducerContactRequestDraftResponse?> CreateDomesticProducerContactDraftAsync(
        Guid campaignId,
        DomesticProducerContactRequestDraftRequest request,
        CancellationToken cancellationToken = default);
    Task<DomesticGroupPurchaseRepresentativeCandidateQueryResponse> GetDomesticGroupPurchaseRepresentativesAsync(
        Guid campaignId,
        string? search = null,
        string? operatingAreaCode = null,
        string? product = null,
        CancellationToken cancellationToken = default);
    Task<DomesticProducerSupplyOfferDraftResponse?> CreateDomesticProducerSupplyOfferDraftAsync(
        Guid campaignId,
        DomesticProducerSupplyOfferDraftRequest request,
        CancellationToken cancellationToken = default);
    Task<DomesticGroupPurchaseSupplyCompatibilityPreviewResponse?> PreviewDomesticSupplyCompatibilityAsync(
        Guid campaignId,
        DomesticGroupPurchaseSupplyCompatibilityPreviewRequest request,
        CancellationToken cancellationToken = default);
    Task<DomesticGroupPurchaseFulfillmentPlanResponse?> PreviewDomesticGroupPurchaseFulfillmentPlanAsync(
        Guid campaignId,
        DomesticGroupPurchaseFulfillmentPlanRequest request,
        CancellationToken cancellationToken = default);
    Task<DomesticGroupPurchaseFulfillmentOrderDraftResponse?> CreateDomesticGroupPurchaseFulfillmentOrderDraftAsync(
        Guid campaignId,
        DomesticGroupPurchaseFulfillmentPlanRequest request,
        CancellationToken cancellationToken = default);
    Task<DomesticGroupPurchaseNegotiationTimelineResponse> GetDomesticGroupPurchaseNegotiationTimelineAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default);
    Task<DomesticGroupPurchaseNegotiationEventResponse?> AppendDomesticGroupPurchaseNegotiationEventAsync(
        Guid campaignId,
        DomesticGroupPurchaseNegotiationEventRequest request,
        CancellationToken cancellationToken = default);
    Task<DomesticGroupPurchaseNegotiationIssueResponse?> OpenDomesticGroupPurchaseNegotiationIssueAsync(
        Guid campaignId,
        DomesticGroupPurchaseNegotiationIssueRequest request,
        CancellationToken cancellationToken = default);
    Task<DomesticGroupPurchaseNegotiationIssueResponse?> AddDomesticGroupPurchaseDeliberationPositionAsync(
        Guid campaignId,
        Guid issueId,
        DomesticGroupPurchaseDeliberationPositionRequest request,
        CancellationToken cancellationToken = default);
    Task<DomesticGroupPurchaseNegotiationIssueResponse?> ResolveDomesticGroupPurchaseNegotiationIssueAsync(
        Guid campaignId,
        Guid issueId,
        DomesticGroupPurchaseNegotiationResolutionRequest request,
        CancellationToken cancellationToken = default);
}

public interface ICommunityVoteClient
{
    Task<CommunityVoteListResponse> GetGroupPurchaseVotesAsync(
        string? communityScope = null,
        string? hsCode = null,
        CancellationToken cancellationToken = default);
    Task<CommunityVoteResponse?> GetGroupPurchaseVoteAsync(
        Guid voteId,
        CancellationToken cancellationToken = default);
    Task<CommunityVoteResponse?> CreateGroupPurchaseVoteAsync(
        CommunityVoteCreateRequest request,
        CancellationToken cancellationToken = default);
    Task<CommunityVoteResponse?> CastGroupPurchaseVoteAsync(
        Guid voteId,
        CommunityVoteCastRequest request,
        CancellationToken cancellationToken = default);
    Task<CommunityVoteResponse?> CloseVoteAsync(
        Guid voteId,
        CommunityVoteCloseRequest request,
        CancellationToken cancellationToken = default);
    Task<CommunityVoteResolutionDocumentResponse?> CreateVoteResolutionAsync(
        Guid voteId,
        CommunityVoteResolutionDraftRequest request,
        CancellationToken cancellationToken = default);
    Task<CommunityVoteResolutionDocumentResponse?> MarkVoteResolutionReadyToSignAsync(
        Guid voteId,
        CommunityVoteResolutionReadyToSignRequest request,
        CancellationToken cancellationToken = default);
    Task<CommunityVoteResolutionDocumentResponse?> SignVoteResolutionAsync(
        Guid voteId,
        CommunityVoteResolutionSignRequest request,
        CancellationToken cancellationToken = default);
}
