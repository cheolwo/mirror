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
    public async Task<CommunityPostOpportunityListResponse?> GetPostOpportunitiesAsync(
        long postId,
        string displayLanguageCode = CommunityDisplayLanguageCodes.Korean,
        CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<CommunityPostOpportunityListResponse>(
            $"api/v1/community/posts/{postId}/opportunities?displayLanguage={Uri.EscapeDataString(displayLanguageCode)}",
            cancellationToken);

    public async Task<StartCommunityPostParticipationResponse?> StartPostParticipationAsync(
        long postId,
        StartCommunityPostParticipationRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _protectedApiClient.PostAsProtectedJsonAsync(
            $"api/v1/community/posts/{postId}/opportunities/participation/start",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StartCommunityPostParticipationResponse>(
            cancellationToken: cancellationToken);
    }

    public async Task<PromoteCommunityPostParticipationResponse?> PromotePostParticipationAsync(
        long postId,
        PromoteCommunityPostParticipationRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _protectedApiClient.PostAsProtectedJsonAsync(
            $"api/v1/community/posts/{postId}/opportunities/participation/provisional-ledger",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PromoteCommunityPostParticipationResponse>(
            cancellationToken: cancellationToken);
    }

    public async Task<JoinCommunityPostProfessionalResponse?> JoinPostProfessionalRoleAsync(
        long postId,
        JoinCommunityPostProfessionalRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _protectedApiClient.PostAsProtectedJsonAsync(
            $"api/v1/community/posts/{postId}/opportunities/participation/professionals",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JoinCommunityPostProfessionalResponse>(
            cancellationToken: cancellationToken);
    }

    public async Task<JoinCommunityPostPartyRoleResponse?> JoinPostPartyRoleAsync(
        long postId,
        JoinCommunityPostPartyRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _protectedApiClient.PostAsProtectedJsonAsync(
            $"api/v1/community/posts/{postId}/opportunities/participation/party-roles",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JoinCommunityPostPartyRoleResponse>(
            cancellationToken: cancellationToken);
    }

    public async Task<CommunityVoteResponse?> CastCommunityVoteAsync(
        Guid voteId,
        CommunityVoteCastRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _protectedApiClient.PostAsProtectedJsonAsync(
            $"api/v1/community/votes/{voteId}/votes",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CommunityVoteResponse>(
            cancellationToken: cancellationToken);
    }
}
