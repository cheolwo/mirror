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
    public async Task<PlatformCommunityPostListResponse> GetPostsAsync(
        string appKey,
        CancellationToken cancellationToken = default)
    {
        var path = $"api/v1/community/posts?appKey={Uri.EscapeDataString(appKey)}&page=1&pageSize=20";
        return await _httpClient.GetFromJsonAsync<PlatformCommunityPostListResponse>(path, cancellationToken)
               ?? new PlatformCommunityPostListResponse();
    }

    public async Task<PlatformCommunityPostListResponse> GetBoardPostsAsync(
        string appKey,
        string? boardKey = null,
        string? category = null,
        string? workflowTag = null,
        string? roleTag = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"appKey={Uri.EscapeDataString(appKey)}",
            $"page={Math.Max(1, page)}",
            $"pageSize={Math.Clamp(pageSize, 1, 50)}"
        };
        AddQueryValue(query, "boardKey", boardKey);
        AddQueryValue(query, "category", category);
        AddQueryValue(query, "workflowTag", workflowTag);
        AddQueryValue(query, "roleTag", roleTag);

        return await _httpClient.GetFromJsonAsync<PlatformCommunityPostListResponse>(
                   $"api/v1/community/posts?{string.Join("&", query)}",
                   cancellationToken)
               ?? new PlatformCommunityPostListResponse();
    }

    public async Task<IReadOnlyList<CommunityBoardSummaryResponse>> GetBoardSummariesAsync(
        string appKey,
        CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<CommunityBoardSummaryResponse>>(
               $"api/v1/community/posts/board-summaries?appKey={Uri.EscapeDataString(appKey)}",
               cancellationToken)
           ?? [];

    public async Task<PlatformCommunityPostResponse?> GetPostAsync(
        long postId,
        CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<PlatformCommunityPostResponse>(
            $"api/v1/community/posts/{postId}",
            cancellationToken);

    public async Task<PlatformCommunityPostTranslationResponse?> TranslatePostAsync(
        long postId,
        string targetLanguageCode,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync(
            $"api/v1/community/posts/{postId}/translations/{Uri.EscapeDataString(targetLanguageCode)}",
            content: null,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PlatformCommunityPostTranslationResponse>(
            cancellationToken: cancellationToken);
    }
}
