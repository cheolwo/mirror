using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Services.External.YouTube;

public interface IYouTubeDataApiClient
{
    Task<IReadOnlyList<YouTube채널검색응답>> 채널검색Async(
        string 검색어,
        int maxResults,
        string? regionCode,
        string? relevanceLanguage,
        CancellationToken cancellationToken);

    Task<YouTube채널응답?> 채널조회Async(string channelId, CancellationToken cancellationToken);

    Task<YouTube채널응답?> 채널Handle조회Async(string handle, CancellationToken cancellationToken);

    Task<IReadOnlyList<YouTube재생목록응답>> 재생목록목록조회Async(
        string channelId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<YouTube영상응답>> 업로드목록조회Async(
        string uploadsPlaylistId,
        int maxResults,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<YouTube영상응답>> 재생목록영상조회Async(
        string playlistId,
        int maxResults,
        CancellationToken cancellationToken);
}

public sealed record YouTube채널응답(
    string ChannelId,
    string 채널명,
    string UploadsPlaylistId,
    string? 썸네일Url);

public sealed record YouTube채널검색응답(
    string ChannelId,
    string 채널명,
    string 설명,
    DateTime 게시일시Utc,
    string? 썸네일Url);

public sealed record YouTube영상응답(
    string VideoId,
    string ChannelId,
    string 제목,
    string 설명,
    DateTime 게시일시Utc,
    string? 썸네일Url);

public sealed record YouTube재생목록응답(
    string PlaylistId,
    string ChannelId,
    string 제목,
    string 설명,
    DateTime 게시일시Utc,
    int 영상수,
    string? 썸네일Url);

public sealed class YouTubeDataApiClient : IYouTubeDataApiClient
{
    private readonly HttpClient _httpClient;
    private readonly YouTubeOptions _options;

    public YouTubeDataApiClient(HttpClient httpClient, IOptions<YouTubeOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<YouTube채널검색응답>> 채널검색Async(
        string 검색어,
        int maxResults,
        string? regionCode,
        string? relevanceLanguage,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        ArgumentException.ThrowIfNullOrWhiteSpace(검색어);

        var query = 검색어.Trim();
        if (query.Length > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(검색어), "YouTube 검색어는 200자 이하여야 합니다.");
        }

        var safeMaxResults = Math.Clamp(maxResults, 1, 25);
        var path = $"search?part=snippet&type=channel&topicId={Encode("/m/02wbm")}&order=relevance&q={Encode(query)}&maxResults={safeMaxResults}&key={Encode(_options.ApiKey)}";
        if (!string.IsNullOrWhiteSpace(regionCode))
        {
            var normalizedRegion = NormalizeRegionCode(regionCode);
            path += $"&regionCode={Encode(normalizedRegion)}";
        }

        if (!string.IsNullOrWhiteSpace(relevanceLanguage))
        {
            var normalizedLanguage = relevanceLanguage.Trim();
            if (normalizedLanguage.Length > 10)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(relevanceLanguage),
                    "YouTube 관련 언어 코드는 10자 이하여야 합니다.");
            }

            path += $"&relevanceLanguage={Encode(normalizedLanguage)}";
        }

        using var response = await _httpClient.GetAsync(path, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var body = await response.Content.ReadFromJsonAsync<YouTube목록응답<YouTube채널검색항목>>(
            cancellationToken);

        return (body?.Items ?? [])
            .Select(ToChannelSearchResult)
            .Where(item => item is not null)
            .Cast<YouTube채널검색응답>()
            .GroupBy(item => item.ChannelId, StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(safeMaxResults)
            .ToArray();
    }

    public async Task<YouTube채널응답?> 채널조회Async(
        string channelId,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);

        var path = $"channels?part=snippet,contentDetails&id={Encode(channelId)}&key={Encode(_options.ApiKey)}";
        using var response = await _httpClient.GetAsync(path, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await ReadChannelAsync(response, cancellationToken);
    }

    public async Task<YouTube채널응답?> 채널Handle조회Async(
        string handle,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);
        var normalizedHandle = handle.Trim().TrimStart('@');
        if (normalizedHandle.Length is 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(handle), "YouTube handle은 100자 이하여야 합니다.");
        }

        var path = $"channels?part=snippet,contentDetails&forHandle={Encode(normalizedHandle)}&key={Encode(_options.ApiKey)}";
        using var response = await _httpClient.GetAsync(path, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadChannelAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<YouTube영상응답>> 업로드목록조회Async(
        string uploadsPlaylistId,
        int maxResults,
        CancellationToken cancellationToken)
        => await 재생목록영상조회Async(
            uploadsPlaylistId,
            maxResults,
            cancellationToken);

    public async Task<IReadOnlyList<YouTube재생목록응답>> 재생목록목록조회Async(
        string channelId,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);

        var playlists = new List<YouTube재생목록응답>();
        var pageTokens = new HashSet<string>(StringComparer.Ordinal);
        string? pageToken = null;
        do
        {
            var path = $"playlists?part=snippet,contentDetails&channelId={Encode(channelId)}&maxResults=50&key={Encode(_options.ApiKey)}";
            if (!string.IsNullOrWhiteSpace(pageToken))
            {
                path += $"&pageToken={Encode(pageToken)}";
            }

            using var response = await _httpClient.GetAsync(path, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            var body = await response.Content.ReadFromJsonAsync<YouTube목록응답<YouTube재생목록정보항목>>(
                cancellationToken);
            playlists.AddRange((body?.Items ?? [])
                .Select(ToPlaylist)
                .Where(x => x is not null)
                .Cast<YouTube재생목록응답>());

            pageToken = NextPageToken(body?.NextPageToken, pageTokens);
        }
        while (pageToken is not null);

        return playlists
            .GroupBy(x => x.PlaylistId, StringComparer.Ordinal)
            .Select(x => x.First())
            .OrderByDescending(x => x.게시일시Utc)
            .ToArray();
    }

    public async Task<IReadOnlyList<YouTube영상응답>> 재생목록영상조회Async(
        string playlistId,
        int maxResults,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        ArgumentException.ThrowIfNullOrWhiteSpace(playlistId);

        var safeMaxResults = Math.Clamp(maxResults, 1, 200);
        var videos = new List<YouTube영상응답>();
        var pageTokens = new HashSet<string>(StringComparer.Ordinal);
        string? pageToken = null;
        do
        {
            var pageSize = Math.Min(50, safeMaxResults - videos.Count);
            var path = $"playlistItems?part=snippet,contentDetails&playlistId={Encode(playlistId)}&maxResults={pageSize}&key={Encode(_options.ApiKey)}";
            if (!string.IsNullOrWhiteSpace(pageToken))
            {
                path += $"&pageToken={Encode(pageToken)}";
            }

            using var response = await _httpClient.GetAsync(path, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            var body = await response.Content.ReadFromJsonAsync<YouTube목록응답<YouTube재생목록항목>>(
                cancellationToken);
            videos.AddRange((body?.Items ?? [])
                .Select(ToVideo)
                .Where(x => x is not null)
                .Cast<YouTube영상응답>());

            pageToken = videos.Count < safeMaxResults
                ? NextPageToken(body?.NextPageToken, pageTokens)
                : null;
        }
        while (pageToken is not null);

        return videos
            .GroupBy(x => x.VideoId, StringComparer.Ordinal)
            .Select(x => x.OrderByDescending(video => video.게시일시Utc).First())
            .OrderByDescending(x => x.게시일시Utc)
            .Take(safeMaxResults)
            .ToArray();
    }

    private void EnsureConfigured()
    {
        if (!_options.Enabled)
        {
            throw new InvalidOperationException("YouTube Data API가 비활성화되어 있습니다.");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("YouTube:ApiKey 설정이 필요합니다.");
        }
    }

    private static YouTube영상응답? ToVideo(YouTube재생목록항목 item)
    {
        var videoId = item.ContentDetails?.VideoId ?? item.Snippet?.ResourceId?.VideoId;
        var channelId = item.Snippet?.ChannelId;
        var publishedAt = item.ContentDetails?.VideoPublishedAt ?? item.Snippet?.PublishedAt;
        if (string.IsNullOrWhiteSpace(videoId)
            || string.IsNullOrWhiteSpace(channelId)
            || publishedAt is null)
        {
            return null;
        }

        return new YouTube영상응답(
            videoId,
            channelId,
            item.Snippet?.Title?.Trim() ?? videoId,
            item.Snippet?.Description?.Trim() ?? string.Empty,
            publishedAt.Value.ToUniversalTime(),
            SelectThumbnail(item.Snippet?.Thumbnails));
    }

    private static YouTube채널검색응답? ToChannelSearchResult(YouTube채널검색항목 item)
    {
        var channelId = item.Id?.ChannelId;
        if (string.IsNullOrWhiteSpace(channelId))
        {
            return null;
        }

        return new YouTube채널검색응답(
            channelId,
            item.Snippet?.Title?.Trim() ?? channelId,
            item.Snippet?.Description?.Trim() ?? string.Empty,
            item.Snippet?.PublishedAt?.ToUniversalTime() ?? DateTime.UnixEpoch,
            SelectThumbnail(item.Snippet?.Thumbnails));
    }

    private static YouTube재생목록응답? ToPlaylist(YouTube재생목록정보항목 item)
    {
        var channelId = item.Snippet?.ChannelId;
        var publishedAt = item.Snippet?.PublishedAt;
        if (string.IsNullOrWhiteSpace(item.Id)
            || string.IsNullOrWhiteSpace(channelId)
            || publishedAt is null)
        {
            return null;
        }

        return new YouTube재생목록응답(
            item.Id,
            channelId,
            item.Snippet?.Title?.Trim() ?? item.Id,
            item.Snippet?.Description?.Trim() ?? string.Empty,
            publishedAt.Value.ToUniversalTime(),
            Math.Max(0, item.ContentDetails?.ItemCount ?? 0),
            SelectThumbnail(item.Snippet?.Thumbnails));
    }

    private static string? SelectThumbnail(YouTube썸네일목록? thumbnails)
        => thumbnails?.MaxRes?.Url
           ?? thumbnails?.Standard?.Url
           ?? thumbnails?.High?.Url
           ?? thumbnails?.Medium?.Url
           ?? thumbnails?.Default?.Url;

    private static async Task<YouTube채널응답?> ReadChannelAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadFromJsonAsync<YouTube목록응답<YouTube채널항목>>(
            cancellationToken);
        var item = body?.Items?.FirstOrDefault();
        var uploadsPlaylistId = item?.ContentDetails?.RelatedPlaylists?.Uploads;
        if (item is null || string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(uploadsPlaylistId))
        {
            return null;
        }

        return new YouTube채널응답(
            item.Id,
            item.Snippet?.Title?.Trim() ?? item.Id,
            uploadsPlaylistId,
            SelectThumbnail(item.Snippet?.Thumbnails));
    }

    private static string Encode(string value) => Uri.EscapeDataString(value);

    private static string NormalizeRegionCode(string regionCode)
    {
        var normalized = regionCode.Trim().ToUpperInvariant();
        if (normalized.Length != 2 || normalized.Any(character => character is < 'A' or > 'Z'))
        {
            throw new ArgumentException("YouTube 지역 코드는 ISO 3166-1 alpha-2 형식이어야 합니다.", nameof(regionCode));
        }

        return normalized;
    }

    private static string? NextPageToken(string? nextPageToken, HashSet<string> seenTokens)
        => !string.IsNullOrWhiteSpace(nextPageToken) && seenTokens.Add(nextPageToken)
            ? nextPageToken
            : null;

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        if (detail.Length > 1000)
        {
            detail = detail[..1000];
        }

        throw new HttpRequestException(
            $"YouTube Data API 호출 실패: {(int)response.StatusCode} {response.ReasonPhrase}. {detail}",
            null,
            response.StatusCode);
    }

    private sealed record YouTube목록응답<T>(
        [property: JsonPropertyName("items")] IReadOnlyList<T>? Items,
        [property: JsonPropertyName("nextPageToken")] string? NextPageToken);

    private sealed record YouTube채널항목(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("snippet")] YouTube채널Snippet? Snippet,
        [property: JsonPropertyName("contentDetails")] YouTube채널ContentDetails? ContentDetails);

    private sealed record YouTube채널검색항목(
        [property: JsonPropertyName("id")] YouTube채널검색Id? Id,
        [property: JsonPropertyName("snippet")] YouTube검색Snippet? Snippet);

    private sealed record YouTube채널검색Id(
        [property: JsonPropertyName("channelId")] string? ChannelId);

    private sealed record YouTube검색Snippet(
        [property: JsonPropertyName("publishedAt")] DateTime? PublishedAt,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("thumbnails")] YouTube썸네일목록? Thumbnails);

    private sealed record YouTube채널Snippet(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("thumbnails")] YouTube썸네일목록? Thumbnails);

    private sealed record YouTube채널ContentDetails(
        [property: JsonPropertyName("relatedPlaylists")] YouTube관련재생목록? RelatedPlaylists);

    private sealed record YouTube관련재생목록(
        [property: JsonPropertyName("uploads")] string? Uploads);

    private sealed record YouTube재생목록정보항목(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("snippet")] YouTube재생목록Snippet? Snippet,
        [property: JsonPropertyName("contentDetails")] YouTube재생목록ContentDetails? ContentDetails);

    private sealed record YouTube재생목록Snippet(
        [property: JsonPropertyName("publishedAt")] DateTime? PublishedAt,
        [property: JsonPropertyName("channelId")] string? ChannelId,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("thumbnails")] YouTube썸네일목록? Thumbnails);

    private sealed record YouTube재생목록ContentDetails(
        [property: JsonPropertyName("itemCount")] int? ItemCount);

    private sealed record YouTube재생목록항목(
        [property: JsonPropertyName("snippet")] YouTube영상Snippet? Snippet,
        [property: JsonPropertyName("contentDetails")] YouTube영상ContentDetails? ContentDetails);

    private sealed record YouTube영상Snippet(
        [property: JsonPropertyName("publishedAt")] DateTime? PublishedAt,
        [property: JsonPropertyName("channelId")] string? ChannelId,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("thumbnails")] YouTube썸네일목록? Thumbnails,
        [property: JsonPropertyName("resourceId")] YouTube영상ResourceId? ResourceId);

    private sealed record YouTube영상ResourceId(
        [property: JsonPropertyName("videoId")] string? VideoId);

    private sealed record YouTube영상ContentDetails(
        [property: JsonPropertyName("videoId")] string? VideoId,
        [property: JsonPropertyName("videoPublishedAt")] DateTime? VideoPublishedAt);

    private sealed record YouTube썸네일목록(
        [property: JsonPropertyName("default")] YouTube썸네일? Default,
        [property: JsonPropertyName("medium")] YouTube썸네일? Medium,
        [property: JsonPropertyName("high")] YouTube썸네일? High,
        [property: JsonPropertyName("standard")] YouTube썸네일? Standard,
        [property: JsonPropertyName("maxres")] YouTube썸네일? MaxRes);

    private sealed record YouTube썸네일(
        [property: JsonPropertyName("url")] string? Url);
}
