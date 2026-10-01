using System.Net.Http.Headers;
using System.Net.Http.Json;
using Hongdal.Contracts.Admin.Audit;
using Hongdal.Contracts.Admin.Community;

namespace HongdalAdminApp.Services;

public sealed class CommunityManagementAdminService
{
    private const string BasePath = "api/v1/admin/community-management";
    private readonly HttpClient httpClient;
    private readonly AdminAuthSession session;

    public CommunityManagementAdminService(HttpClient httpClient, AdminAuthSession session)
    {
        this.httpClient = httpClient;
        this.session = session;
    }

    public Task<CommunityManagementUserResponse> GetUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
        => GetAsync<CommunityManagementUserResponse>(
            $"{BasePath}/users/{Uri.EscapeDataString(userId.Trim())}",
            cancellationToken);

    public Task<사용자행위로그목록응답> GetActivityLogsAsync(
        string userId,
        CancellationToken cancellationToken = default)
        => GetAsync<사용자행위로그목록응답>(
            $"api/v1/admin/activity-logs?userId={Uri.EscapeDataString(userId.Trim())}&page=1&pageSize=100",
            cancellationToken);

    public Task<CommunityManagementActionResponse> UpdatePostAsync(
        long postId,
        CommunityManagementPostUpdateRequest request,
        CancellationToken cancellationToken = default)
        => PutAsync<CommunityManagementPostUpdateRequest, CommunityManagementActionResponse>(
            $"{BasePath}/posts/{postId}",
            request,
            cancellationToken);

    public Task<CommunityManagementActionResponse> SetPostHiddenAsync(
        long postId,
        bool hidden,
        string reason,
        CancellationToken cancellationToken = default)
        => PutAsync<CommunityManagementVisibilityRequest, CommunityManagementActionResponse>(
            $"{BasePath}/posts/{postId}/visibility",
            new CommunityManagementVisibilityRequest { Hidden = hidden, Reason = reason },
            cancellationToken);

    public Task<CommunityManagementActionResponse> SetCommentHiddenAsync(
        long commentId,
        bool hidden,
        string reason,
        CancellationToken cancellationToken = default)
        => PutAsync<CommunityManagementVisibilityRequest, CommunityManagementActionResponse>(
            $"{BasePath}/comments/{commentId}/visibility",
            new CommunityManagementVisibilityRequest { Hidden = hidden, Reason = reason },
            cancellationToken);

    public Task<CommunityManagementActionResponse> SetAttachmentCommentHiddenAsync(
        long commentId,
        bool hidden,
        string reason,
        CancellationToken cancellationToken = default)
        => PutAsync<CommunityManagementVisibilityRequest, CommunityManagementActionResponse>(
            $"{BasePath}/attachment-comments/{commentId}/visibility",
            new CommunityManagementVisibilityRequest { Hidden = hidden, Reason = reason },
            cancellationToken);

    public Task<CommunityManagementActionResponse> RecordContactAsync(
        string userId,
        string channel,
        string note,
        CancellationToken cancellationToken = default)
        => PostAsync<CommunityManagementContactRequest, CommunityManagementActionResponse>(
            $"{BasePath}/users/{Uri.EscapeDataString(userId.Trim())}/contact-actions",
            new CommunityManagementContactRequest { Channel = channel, Note = note },
            cancellationToken);

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, path);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadAsync<T>(response, cancellationToken);
    }

    private async Task<TResponse> PutAsync<TRequest, TResponse>(
        string path,
        TRequest payload,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Put, path);
        request.Content = JsonContent.Create(payload);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadAsync<TResponse>(response, cancellationToken);
    }

    private async Task<TResponse> PostAsync<TRequest, TResponse>(
        string path,
        TRequest payload,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, path);
        request.Content = JsonContent.Create(payload);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadAsync<TResponse>(response, cancellationToken);
    }

    private async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(detail)
                    ? $"관리자 API 요청에 실패했습니다. ({(int)response.StatusCode})"
                    : detail);
        }

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
               ?? throw new InvalidOperationException("관리자 API 응답이 비어 있습니다.");
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        if (!session.IsServerAdmin || string.IsNullOrWhiteSpace(session.AccessToken))
        {
            throw new UnauthorizedAccessException("서버관리자 로그인이 필요합니다.");
        }

        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return request;
    }
}
