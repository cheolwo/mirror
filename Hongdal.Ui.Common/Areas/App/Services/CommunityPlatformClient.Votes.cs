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
    public async Task<CommunityVoteListResponse> GetGroupPurchaseVotesAsync(
        string? communityScope = null,
        string? hsCode = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        AddQueryValue(query, "communityScope", communityScope);
        AddQueryValue(query, "hsCode", hsCode);
        var suffix = query.Count == 0 ? string.Empty : $"?{string.Join("&", query)}";
        var path = $"api/v1/orderer/group-purchase-demand-votes{suffix}";

        using var response = await _protectedApiClient.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CommunityVoteListResponse>(cancellationToken: cancellationToken)
               ?? new CommunityVoteListResponse();
    }

    public async Task<CommunityVoteResponse?> GetGroupPurchaseVoteAsync(
        Guid voteId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _protectedApiClient.GetAsync(
            $"api/v1/orderer/group-purchase-demand-votes/{voteId}",
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CommunityVoteResponse>(cancellationToken: cancellationToken);
    }

    public async Task<CommunityVoteResponse?> CreateGroupPurchaseVoteAsync(
        CommunityVoteCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _protectedApiClient.PostAsProtectedJsonAsync(
            "api/v1/orderer/group-purchase-demand-votes",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CommunityVoteResponse>(cancellationToken: cancellationToken);
    }

    public async Task<CommunityVoteResponse?> CastGroupPurchaseVoteAsync(
        Guid voteId,
        CommunityVoteCastRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _protectedApiClient.PostAsProtectedJsonAsync(
            $"api/v1/orderer/group-purchase-demand-votes/{voteId}/votes",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CommunityVoteResponse>(cancellationToken: cancellationToken);
    }

    public async Task<CommunityVoteResponse?> CloseVoteAsync(
        Guid voteId,
        CommunityVoteCloseRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _protectedApiClient.PostAsProtectedJsonAsync(
            $"api/v1/community/votes/{voteId}/close",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CommunityVoteResponse>(cancellationToken: cancellationToken);
    }

    public async Task<CommunityVoteResolutionDocumentResponse?> CreateVoteResolutionAsync(
        Guid voteId,
        CommunityVoteResolutionDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _protectedApiClient.PostAsProtectedJsonAsync(
            $"api/v1/community/votes/{voteId}/resolution-documents",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CommunityVoteResolutionDocumentResponse>(cancellationToken: cancellationToken);
    }

    public async Task<CommunityVoteResolutionDocumentResponse?> MarkVoteResolutionReadyToSignAsync(
        Guid voteId,
        CommunityVoteResolutionReadyToSignRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _protectedApiClient.PostAsProtectedJsonAsync(
            $"api/v1/community/votes/{voteId}/resolution-documents/ready-to-sign",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CommunityVoteResolutionDocumentResponse>(cancellationToken: cancellationToken);
    }

    public async Task<CommunityVoteResolutionDocumentResponse?> SignVoteResolutionAsync(
        Guid voteId,
        CommunityVoteResolutionSignRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _protectedApiClient.PostAsProtectedJsonAsync(
            $"api/v1/community/votes/{voteId}/resolution-documents/signatures",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CommunityVoteResolutionDocumentResponse>(cancellationToken: cancellationToken);
    }
}
