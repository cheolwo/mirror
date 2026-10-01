using System.Net.Http.Headers;
using System.Net.Http.Json;
using Hongdal.Contracts.Common.Content;

namespace HongdalAdminApp.Services;

public sealed class HongikHakdangAdminService
{
    private const string CardBasePath = "api/v1/admin/content/hongik-hakdang/cards";
    private const string YouTubeBasePath = "api/v1/admin/content/youtube";

    private readonly HttpClient httpClient;
    private readonly AdminAuthSession session;

    public HongikHakdangAdminService(HttpClient httpClient, AdminAuthSession session)
    {
        this.httpClient = httpClient;
        this.session = session;
    }

    public async Task<HongikHakdangAdminSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        var collectionsTask = GetAsync<IReadOnlyList<HongikHakdangCardCollectionDto>>(
            $"{CardBasePath}?includeInactive=true",
            cancellationToken);
        var channelsTask = GetAsync<IReadOnlyList<YouTube감시채널Dto>>(
            $"{YouTubeBasePath}/channels",
            cancellationToken);
        var videosTask = GetAsync<IReadOnlyList<YouTube채널영상Dto>>(
            $"{YouTubeBasePath}/videos?take=100",
            cancellationToken);

        await Task.WhenAll(collectionsTask, channelsTask, videosTask);
        return new HongikHakdangAdminSnapshot(
            await collectionsTask,
            await channelsTask,
            await videosTask);
    }

    public Task<HongikHakdangCardSyncResultDto> SyncCardsAsync(CancellationToken cancellationToken = default)
        => PostAsync<HongikHakdangCardSyncResultDto>($"{CardBasePath}/sync", cancellationToken);

    public Task<HongikHakdangCardVariantPreparationResultDto> PrepareVariantsAsync(
        CancellationToken cancellationToken = default)
        => PostAsync<HongikHakdangCardVariantPreparationResultDto>(
            $"{CardBasePath}/variants/prepare",
            cancellationToken);

    public Task<HongikHakdangCardActivationUpdateResponse> SetCollectionActivationAsync(
        long collectionId,
        bool enabled,
        CancellationToken cancellationToken = default)
        => PutAsync<HongikHakdangCardActivationUpdateRequest, HongikHakdangCardActivationUpdateResponse>(
            $"{CardBasePath}/collections/{collectionId}/activation",
            new HongikHakdangCardActivationUpdateRequest(enabled),
            cancellationToken);

    public Task<HongikHakdangCardActivationUpdateResponse> SetCardActivationAsync(
        long cardId,
        bool enabled,
        CancellationToken cancellationToken = default)
        => PutAsync<HongikHakdangCardActivationUpdateRequest, HongikHakdangCardActivationUpdateResponse>(
            $"{CardBasePath}/{cardId}/activation",
            new HongikHakdangCardActivationUpdateRequest(enabled),
            cancellationToken);

    public Task<HongikHakdangCardCommunityPublicationUpdateResponse> SetCardCommunityPublicationAsync(
        long cardId,
        bool approved,
        CancellationToken cancellationToken = default)
        => PutAsync<HongikHakdangCardCommunityPublicationUpdateRequest, HongikHakdangCardCommunityPublicationUpdateResponse>(
            $"{CardBasePath}/{cardId}/community-publication",
            new HongikHakdangCardCommunityPublicationUpdateRequest(approved),
            cancellationToken);

    public async Task<string?> GetCardImageDataUrlAsync(
        long cardId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"{CardBasePath}/{cardId}/image");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
        return $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
    }

    public Task<YouTube채널동기화결과Dto> SyncYouTubeAsync(CancellationToken cancellationToken = default)
        => PostAsync<YouTube채널동기화결과Dto>($"{YouTubeBasePath}/sync", cancellationToken);

    public async Task SetVideoPublicationAsync(
        string videoId,
        bool publish,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(
            HttpMethod.Put,
            $"{YouTubeBasePath}/videos/{Uri.EscapeDataString(videoId)}/publication");
        request.Content = JsonContent.Create(new YouTube영상공개설정요청Dto(publish));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<YouTube감시채널Dto> SetChannelPrajnaPublicationAsync(
        string channelId,
        bool allowed,
        CancellationToken cancellationToken = default)
        => PutAsync<YouTube반야게시채널설정요청Dto, YouTube감시채널Dto>(
            $"{YouTubeBasePath}/channels/{Uri.EscapeDataString(channelId)}/prajna-publication",
            new YouTube반야게시채널설정요청Dto(allowed),
            cancellationToken);

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, path);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
               ?? throw new InvalidOperationException("관리자 API 응답이 비어 있습니다.");
    }

    private async Task<T> PostAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, path);
        request.Content = new ByteArrayContent([]);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
               ?? throw new InvalidOperationException("관리자 API 응답이 비어 있습니다.");
    }

    private async Task<TResponse> PutAsync<TRequest, TResponse>(
        string path,
        TRequest payload,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Put, path);
        request.Content = JsonContent.Create(payload);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken)
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

public sealed record HongikHakdangAdminSnapshot(
    IReadOnlyList<HongikHakdangCardCollectionDto> Collections,
    IReadOnlyList<YouTube감시채널Dto> Channels,
    IReadOnlyList<YouTube채널영상Dto> Videos);
