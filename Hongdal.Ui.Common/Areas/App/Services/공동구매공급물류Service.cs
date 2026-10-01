using Hongdal.Contracts.Common.Orderer;

namespace Hongdal.Ui.Common.Areas.App.Services;

/// <summary>
/// 국내 공동구매의 생산자 연결, 공급 제안과 공개 협상 API 경계입니다.
/// </summary>
public interface I공동구매공급Service
{
    Task<DomesticProducerCandidateQueryResponse> 생산자후보조회Async(
        Guid campaignId,
        string? search = null,
        string? regionCode = null,
        string? product = null,
        CancellationToken cancellationToken = default);

    Task<DomesticProducerContactRequestDraftResponse?> 연락요청초안생성Async(
        Guid campaignId,
        DomesticProducerContactRequestDraftRequest request,
        CancellationToken cancellationToken = default);

    Task<DomesticGroupPurchaseRepresentativeCandidateQueryResponse> 대표후보조회Async(
        Guid campaignId,
        string? search = null,
        string? operatingAreaCode = null,
        string? product = null,
        CancellationToken cancellationToken = default);

    Task<DomesticProducerSupplyOfferDraftResponse?> 공급제안초안생성Async(
        Guid campaignId,
        DomesticProducerSupplyOfferDraftRequest request,
        CancellationToken cancellationToken = default);

    Task<DomesticGroupPurchaseSupplyCompatibilityPreviewResponse?> 공급적합성미리보기Async(
        Guid campaignId,
        DomesticGroupPurchaseSupplyCompatibilityPreviewRequest request,
        CancellationToken cancellationToken = default);

    Task<DomesticGroupPurchaseNegotiationTimelineResponse> 협상이력조회Async(
        Guid campaignId,
        CancellationToken cancellationToken = default);

    Task<DomesticGroupPurchaseNegotiationEventResponse?> 협상이벤트등록Async(
        Guid campaignId,
        DomesticGroupPurchaseNegotiationEventRequest request,
        CancellationToken cancellationToken = default);

    Task<DomesticGroupPurchaseNegotiationIssueResponse?> 협상쟁점등록Async(
        Guid campaignId,
        DomesticGroupPurchaseNegotiationIssueRequest request,
        CancellationToken cancellationToken = default);

    Task<DomesticGroupPurchaseNegotiationIssueResponse?> 숙고의견등록Async(
        Guid campaignId,
        Guid issueId,
        DomesticGroupPurchaseDeliberationPositionRequest request,
        CancellationToken cancellationToken = default);

    Task<DomesticGroupPurchaseNegotiationIssueResponse?> 협상쟁점합의Async(
        Guid campaignId,
        Guid issueId,
        DomesticGroupPurchaseNegotiationResolutionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class PlatformCommunity공동구매공급Service(
    PlatformCommunityService communityService) : I공동구매공급Service
{
    public Task<DomesticProducerCandidateQueryResponse> 생산자후보조회Async(
        Guid campaignId,
        string? search = null,
        string? regionCode = null,
        string? product = null,
        CancellationToken cancellationToken = default)
        => communityService.GetDomesticProducerCandidatesAsync(
            campaignId, search, regionCode, product, cancellationToken);

    public Task<DomesticProducerContactRequestDraftResponse?> 연락요청초안생성Async(
        Guid campaignId,
        DomesticProducerContactRequestDraftRequest request,
        CancellationToken cancellationToken = default)
        => communityService.CreateDomesticProducerContactDraftAsync(campaignId, request, cancellationToken);

    public Task<DomesticGroupPurchaseRepresentativeCandidateQueryResponse> 대표후보조회Async(
        Guid campaignId,
        string? search = null,
        string? operatingAreaCode = null,
        string? product = null,
        CancellationToken cancellationToken = default)
        => communityService.GetDomesticGroupPurchaseRepresentativesAsync(
            campaignId, search, operatingAreaCode, product, cancellationToken);

    public Task<DomesticProducerSupplyOfferDraftResponse?> 공급제안초안생성Async(
        Guid campaignId,
        DomesticProducerSupplyOfferDraftRequest request,
        CancellationToken cancellationToken = default)
        => communityService.CreateDomesticProducerSupplyOfferDraftAsync(campaignId, request, cancellationToken);

    public Task<DomesticGroupPurchaseSupplyCompatibilityPreviewResponse?> 공급적합성미리보기Async(
        Guid campaignId,
        DomesticGroupPurchaseSupplyCompatibilityPreviewRequest request,
        CancellationToken cancellationToken = default)
        => communityService.PreviewDomesticSupplyCompatibilityAsync(campaignId, request, cancellationToken);

    public Task<DomesticGroupPurchaseNegotiationTimelineResponse> 협상이력조회Async(
        Guid campaignId,
        CancellationToken cancellationToken = default)
        => communityService.GetDomesticGroupPurchaseNegotiationTimelineAsync(campaignId, cancellationToken);

    public Task<DomesticGroupPurchaseNegotiationEventResponse?> 협상이벤트등록Async(
        Guid campaignId,
        DomesticGroupPurchaseNegotiationEventRequest request,
        CancellationToken cancellationToken = default)
        => communityService.AppendDomesticGroupPurchaseNegotiationEventAsync(campaignId, request, cancellationToken);

    public Task<DomesticGroupPurchaseNegotiationIssueResponse?> 협상쟁점등록Async(
        Guid campaignId,
        DomesticGroupPurchaseNegotiationIssueRequest request,
        CancellationToken cancellationToken = default)
        => communityService.OpenDomesticGroupPurchaseNegotiationIssueAsync(campaignId, request, cancellationToken);

    public Task<DomesticGroupPurchaseNegotiationIssueResponse?> 숙고의견등록Async(
        Guid campaignId,
        Guid issueId,
        DomesticGroupPurchaseDeliberationPositionRequest request,
        CancellationToken cancellationToken = default)
        => communityService.AddDomesticGroupPurchaseDeliberationPositionAsync(
            campaignId, issueId, request, cancellationToken);

    public Task<DomesticGroupPurchaseNegotiationIssueResponse?> 협상쟁점합의Async(
        Guid campaignId,
        Guid issueId,
        DomesticGroupPurchaseNegotiationResolutionRequest request,
        CancellationToken cancellationToken = default)
        => communityService.ResolveDomesticGroupPurchaseNegotiationIssueAsync(
            campaignId, issueId, request, cancellationToken);
}

/// <summary>
/// 확정된 공급 조건을 발주 주문 원장과 후속 물류 원장 계획으로 바꾸는 API 경계입니다.
/// </summary>
public interface I공동구매물류Service
{
    Task<DomesticGroupPurchaseFulfillmentPlanResponse?> 이행계획미리보기Async(
        Guid campaignId,
        DomesticGroupPurchaseFulfillmentPlanRequest request,
        CancellationToken cancellationToken = default);

    Task<DomesticGroupPurchaseFulfillmentOrderDraftResponse?> 발주초안생성Async(
        Guid campaignId,
        DomesticGroupPurchaseFulfillmentPlanRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class PlatformCommunity공동구매물류Service(
    PlatformCommunityService communityService) : I공동구매물류Service
{
    public Task<DomesticGroupPurchaseFulfillmentPlanResponse?> 이행계획미리보기Async(
        Guid campaignId,
        DomesticGroupPurchaseFulfillmentPlanRequest request,
        CancellationToken cancellationToken = default)
        => communityService.PreviewDomesticGroupPurchaseFulfillmentPlanAsync(
            campaignId, request, cancellationToken);

    public Task<DomesticGroupPurchaseFulfillmentOrderDraftResponse?> 발주초안생성Async(
        Guid campaignId,
        DomesticGroupPurchaseFulfillmentPlanRequest request,
        CancellationToken cancellationToken = default)
        => communityService.CreateDomesticGroupPurchaseFulfillmentOrderDraftAsync(
            campaignId, request, cancellationToken);
}
