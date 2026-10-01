using System.Net.Http.Json;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Customs;
using Hongdal.Contracts.Common.Orderer;
using Hongdal.Contracts.Common.PublicData;
using Hongdal.Contracts.Common.Versioning;
using Microsoft.AspNetCore.Components.Forms;

namespace Hongdal.Ui.Common.Areas.App.Services;

public partial class CommunityPlatformClient
{
    public async Task<PlatformCommunityBoardListResponse> GetBoardsAsync(
        string appKey,
        string status = PlatformCommunityBoardRequestStatuses.Approved,
        CancellationToken cancellationToken = default)
    {
        var isPublicList = string.Equals(
            status,
            PlatformCommunityBoardRequestStatuses.Approved,
            StringComparison.OrdinalIgnoreCase);
        var path = isPublicList
            ? $"api/v1/community/boards?appKey={Uri.EscapeDataString(appKey)}"
            : $"api/v1/community/boards/requests?appKey={Uri.EscapeDataString(appKey)}&status={Uri.EscapeDataString(status)}";
        if (isPublicList)
        {
            return await _httpClient.GetFromJsonAsync<PlatformCommunityBoardListResponse>(path, cancellationToken)
                   ?? new PlatformCommunityBoardListResponse();
        }

        using var response = await _protectedApiClient.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PlatformCommunityBoardListResponse>(cancellationToken: cancellationToken)
               ?? new PlatformCommunityBoardListResponse();
    }

    public async Task<VersionFeatureFlagsResponse> GetVersionWorkflowMetadataAsync(
        CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<VersionFeatureFlagsResponse>(
                   "api/v1/version-feature-flags",
                   cancellationToken)
               ?? new VersionFeatureFlagsResponse();
    }

    public async Task<GroupImportHsCodeSearchResponse> SearchGroupImportHsCodesAsync(
        string? query = null,
        int? businessCategory = null,
        int page = 1,
        int pageSize = 30,
        CancellationToken cancellationToken = default)
    {
        var queryValues = new List<string>();
        AddQueryValue(queryValues, "query", query);
        if (businessCategory.HasValue)
        {
            queryValues.Add($"businessCategory={businessCategory.Value}");
        }

        queryValues.Add($"page={Math.Max(1, page)}");
        queryValues.Add($"pageSize={Math.Clamp(pageSize, 10, 50)}");
        var path = $"api/v1/customs/hs-codes?{string.Join("&", queryValues)}";

        return await _httpClient.GetFromJsonAsync<GroupImportHsCodeSearchResponse>(path, cancellationToken)
               ?? new GroupImportHsCodeSearchResponse();
    }

    public async Task<HsCountryImportUnitPriceSimulationResult?> GetGroupImportUnitPriceAsync(
        HsCountryMonthlyTradeUnitPriceRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/v1/orderer/public-data/customs/hs-country-import-unit-price-simulation",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<HsCountryImportUnitPriceSimulationResult>(
            cancellationToken: cancellationToken);
    }

    public async Task<CommunityDriverAvailabilityListResponse> GetCommunityDriverAvailabilityAsync(
        string? operatingArea = null,
        CancellationToken cancellationToken = default)
    {
        var suffix = string.IsNullOrWhiteSpace(operatingArea)
            ? string.Empty
            : $"?operatingArea={Uri.EscapeDataString(operatingArea.Trim())}";
        return await _httpClient.GetFromJsonAsync<CommunityDriverAvailabilityListResponse>(
                   $"api/v1/community/driver-availability{suffix}",
                   cancellationToken)
               ?? new CommunityDriverAvailabilityListResponse();
    }

    public async Task<CommunityDriverInquiryResponse?> CreateCommunityDriverInquiryAsync(
        Guid postId,
        CommunityDriverInquiryCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _protectedApiClient.PostAsProtectedJsonAsync(
            $"api/v1/community/driver-availability/{postId}/inquiries",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CommunityDriverInquiryResponse>(
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<CommunityDriverInquiryResponse>> GetMyCommunityDriverInquiriesAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await _protectedApiClient.GetAsync(
            "api/v1/community/driver-availability/my-inquiries",
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<CommunityDriverInquiryResponse>>(
                   cancellationToken: cancellationToken)
               ?? [];
    }

    public async Task<DomesticProducerCandidateQueryResponse> GetDomesticProducerCandidatesAsync(
        Guid campaignId,
        string? search = null,
        string? regionCode = null,
        string? product = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        AddQueryValue(query, "search", search);
        AddQueryValue(query, "regionCode", regionCode);
        AddQueryValue(query, "product", product);
        var suffix = query.Count == 0 ? string.Empty : $"?{string.Join("&", query)}";

        using var response = await _protectedApiClient.GetAsync(
            $"api/v1/orderer/domestic-group-purchases/{campaignId}/producer-connections/candidates{suffix}",
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DomesticProducerCandidateQueryResponse>(
                   cancellationToken: cancellationToken)
               ?? new DomesticProducerCandidateQueryResponse();
    }

    public async Task<DomesticProducerContactRequestDraftResponse?> CreateDomesticProducerContactDraftAsync(
        Guid campaignId,
        DomesticProducerContactRequestDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _protectedApiClient.PostAsProtectedJsonAsync(
            $"api/v1/orderer/domestic-group-purchases/{campaignId}/producer-connections/contact-request-drafts",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DomesticProducerContactRequestDraftResponse>(
            cancellationToken: cancellationToken);
    }

    public async Task<DomesticGroupPurchaseRepresentativeCandidateQueryResponse> GetDomesticGroupPurchaseRepresentativesAsync(
        Guid campaignId,
        string? search = null,
        string? operatingAreaCode = null,
        string? product = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        AddQueryValue(query, "search", search);
        AddQueryValue(query, "operatingAreaCode", operatingAreaCode);
        AddQueryValue(query, "product", product);
        var suffix = query.Count == 0 ? string.Empty : $"?{string.Join("&", query)}";

        using var response = await _protectedApiClient.GetAsync(
            $"api/v1/orderer/domestic-group-purchases/{campaignId}/producer-connections/representatives{suffix}",
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DomesticGroupPurchaseRepresentativeCandidateQueryResponse>(
                   cancellationToken: cancellationToken)
               ?? new DomesticGroupPurchaseRepresentativeCandidateQueryResponse();
    }

    public async Task<DomesticProducerSupplyOfferDraftResponse?> CreateDomesticProducerSupplyOfferDraftAsync(
        Guid campaignId,
        DomesticProducerSupplyOfferDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _protectedApiClient.PostAsProtectedJsonAsync(
            $"api/v1/orderer/domestic-group-purchases/{campaignId}/producer-connections/supply-offer-drafts",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DomesticProducerSupplyOfferDraftResponse>(
            cancellationToken: cancellationToken);
    }

    public async Task<DomesticGroupPurchaseSupplyCompatibilityPreviewResponse?> PreviewDomesticSupplyCompatibilityAsync(
        Guid campaignId,
        DomesticGroupPurchaseSupplyCompatibilityPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _protectedApiClient.PostAsProtectedJsonAsync(
            $"api/v1/orderer/domestic-group-purchases/{campaignId}/producer-connections/compatibility-previews",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DomesticGroupPurchaseSupplyCompatibilityPreviewResponse>(
            cancellationToken: cancellationToken);
    }

    public async Task<DomesticGroupPurchaseFulfillmentPlanResponse?> PreviewDomesticGroupPurchaseFulfillmentPlanAsync(
        Guid campaignId,
        DomesticGroupPurchaseFulfillmentPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _protectedApiClient.PostAsProtectedJsonAsync(
            $"api/v1/orderer/domestic-group-purchases/{campaignId}/fulfillment-plans/preview",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DomesticGroupPurchaseFulfillmentPlanResponse>(
            cancellationToken: cancellationToken);
    }

    public async Task<DomesticGroupPurchaseFulfillmentOrderDraftResponse?> CreateDomesticGroupPurchaseFulfillmentOrderDraftAsync(
        Guid campaignId,
        DomesticGroupPurchaseFulfillmentPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _protectedApiClient.PostAsProtectedJsonAsync(
            $"api/v1/orderer/domestic-group-purchases/{campaignId}/fulfillment-plans/order-drafts",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DomesticGroupPurchaseFulfillmentOrderDraftResponse>(
            cancellationToken: cancellationToken);
    }

    public async Task<DomesticGroupPurchaseNegotiationTimelineResponse> GetDomesticGroupPurchaseNegotiationTimelineAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _protectedApiClient.GetAsync(
            $"api/v1/orderer/domestic-group-purchases/{campaignId}/negotiation",
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DomesticGroupPurchaseNegotiationTimelineResponse>(
                   cancellationToken: cancellationToken)
               ?? new DomesticGroupPurchaseNegotiationTimelineResponse { GroupPurchaseCampaignId = campaignId };
    }

    public async Task<DomesticGroupPurchaseNegotiationEventResponse?> AppendDomesticGroupPurchaseNegotiationEventAsync(
        Guid campaignId,
        DomesticGroupPurchaseNegotiationEventRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _protectedApiClient.PostAsProtectedJsonAsync(
            $"api/v1/orderer/domestic-group-purchases/{campaignId}/negotiation/events",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DomesticGroupPurchaseNegotiationEventResponse>(
            cancellationToken: cancellationToken);
    }

    public async Task<DomesticGroupPurchaseNegotiationIssueResponse?> OpenDomesticGroupPurchaseNegotiationIssueAsync(
        Guid campaignId,
        DomesticGroupPurchaseNegotiationIssueRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _protectedApiClient.PostAsProtectedJsonAsync(
            $"api/v1/orderer/domestic-group-purchases/{campaignId}/negotiation/issues",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DomesticGroupPurchaseNegotiationIssueResponse>(
            cancellationToken: cancellationToken);
    }

    public async Task<DomesticGroupPurchaseNegotiationIssueResponse?> AddDomesticGroupPurchaseDeliberationPositionAsync(
        Guid campaignId,
        Guid issueId,
        DomesticGroupPurchaseDeliberationPositionRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _protectedApiClient.PostAsProtectedJsonAsync(
            $"api/v1/orderer/domestic-group-purchases/{campaignId}/negotiation/issues/{issueId}/positions",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DomesticGroupPurchaseNegotiationIssueResponse>(
            cancellationToken: cancellationToken);
    }

    public async Task<DomesticGroupPurchaseNegotiationIssueResponse?> ResolveDomesticGroupPurchaseNegotiationIssueAsync(
        Guid campaignId,
        Guid issueId,
        DomesticGroupPurchaseNegotiationResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _protectedApiClient.PostAsProtectedJsonAsync(
            $"api/v1/orderer/domestic-group-purchases/{campaignId}/negotiation/issues/{issueId}/resolution",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DomesticGroupPurchaseNegotiationIssueResponse>(
            cancellationToken: cancellationToken);
    }
}
