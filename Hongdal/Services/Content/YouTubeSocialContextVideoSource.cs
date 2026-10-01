using Hongdal.Contracts.Common.Content;

namespace Hongdal.Services.Content;

public interface IYouTubeSocialContextVideoSource
{
    Task<YouTubeSocialContextVideoDto?> GetAsync(
        string videoId,
        CancellationToken cancellationToken);
}

public sealed class YouTubeMonitoringSocialContextVideoSource : IYouTubeSocialContextVideoSource
{
    private readonly IYouTube채널감시저장소 _store;

    public YouTubeMonitoringSocialContextVideoSource(IYouTube채널감시저장소 store)
    {
        _store = store;
    }

    public async Task<YouTubeSocialContextVideoDto?> GetAsync(
        string videoId,
        CancellationToken cancellationToken)
    {
        var video = await _store.영상추적조회Async(videoId, cancellationToken);
        if (video is null)
        {
            return null;
        }

        var channel = video.감시채널;
        return new YouTubeSocialContextVideoDto(
            video.VideoId,
            string.IsNullOrWhiteSpace(channel?.채널명) ? video.ChannelId : channel.채널명.Trim(),
            Normalize(video.제목, 300) ?? video.VideoId,
            Normalize(video.설명, 1_500) ?? string.Empty,
            $"https://www.youtube.com/watch?v={Uri.EscapeDataString(video.VideoId)}",
            NormalizeUrl(video.썸네일Url),
            DateTime.SpecifyKind(video.게시일시Utc, DateTimeKind.Utc),
            YouTube채널수집국가코드.정규화(channel?.국가코드),
            string.IsNullOrWhiteSpace(channel?.기본언어코드) ? "und" : channel.기본언어코드.Trim());
    }

    private static string? Normalize(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string? NormalizeUrl(string? value)
        => Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
           && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            ? uri.AbsoluteUri
            : null;
}
