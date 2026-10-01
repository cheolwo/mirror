using System.Net.Http.Headers;
using System.Net.Http.Json;
using Hongdal.Contracts.Common.Content;

namespace Hongdal.Ui.Common.Areas.App.Services;

public sealed class YouTube관리콘텐츠Service
{
    public const string 홍익학당ChannelId = "UCI8HW08rOSlvweOjJ9Gp2Ng";

    private readonly HttpClient httpClient;
    private readonly IHongdalAccessTokenProvider accessTokenProvider;

    public YouTube관리콘텐츠Service(
        HttpClient httpClient,
        IHongdalAccessTokenProvider accessTokenProvider)
    {
        this.httpClient = httpClient;
        this.accessTokenProvider = accessTokenProvider;
    }

    public Task<IReadOnlyList<YouTube재생목록Dto>> 재생목록조회Async(
        string channelId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);
        var path = $"api/v1/admin/content/youtube/playlists?channelId={Uri.EscapeDataString(channelId.Trim())}";
        return GetAsync<IReadOnlyList<YouTube재생목록Dto>>(path, cancellationToken);
    }

    public Task<IReadOnlyList<YouTube재생목록영상Dto>> 재생목록영상조회Async(
        string playlistId,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playlistId);
        var safeTake = Math.Clamp(take, 1, 200);
        var path = $"api/v1/admin/content/youtube/playlists/{Uri.EscapeDataString(playlistId.Trim())}/videos?take={safeTake}";
        return GetAsync<IReadOnlyList<YouTube재생목록영상Dto>>(path, cancellationToken);
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        var accessToken = accessTokenProvider.AccessToken?.Trim();
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
               ?? throw new InvalidOperationException("관리자 콘텐츠 API 응답이 비어 있습니다.");
    }
}
