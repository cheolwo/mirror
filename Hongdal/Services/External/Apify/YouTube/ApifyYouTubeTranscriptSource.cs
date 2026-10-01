using System.Globalization;
using System.Text.Json;
using Hongdal.Contracts.Common.Content;
using Hongdal.Services.External.Apify;
using Hongdal.Services.External.YouTube;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Services.External.Apify.YouTube;

public sealed class ApifyYouTubeTranscriptSource : IYouTubeTranscriptSource
{
    private const string ProviderName = "Apify YouTube Transcript Scraper";

    private readonly IApifyActorGateway _gateway;
    private readonly ApifyYouTubeTranscriptOptions _options;
    private readonly TimeProvider _timeProvider;

    public ApifyYouTubeTranscriptSource(
        IApifyActorGateway gateway,
        IOptions<ApifyYouTubeTranscriptOptions> options,
        TimeProvider timeProvider)
    {
        _gateway = gateway;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public bool IsEnabled => _options.Enabled;

    public async Task<YouTubeTranscriptResponse?> GetAsync(
        YouTubeTranscriptRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureEnabled();

        var videoId = NormalizeVideoId(request.VideoId);
        var languageCode = NormalizeLanguageCode(request.TargetLanguage);
        var videoUrl = $"https://www.youtube.com/watch?v={videoId}";
        var input = JsonSerializer.SerializeToElement(new
        {
            videoUrl,
            targetLanguage = languageCode
        });

        var result = await _gateway.RunSyncGetDatasetItemsAsync(
            new ApifyActorSyncRequest(
                _options.ActorId,
                input,
                _options.ActorTimeoutSeconds,
                _options.MemoryMegabytes,
                Math.Clamp(_options.MaxDatasetItems, 1, 100),
                _options.MaxTotalChargeUsd),
            cancellationToken);

        var segments = result.Items
            .SelectMany(ParseSegments)
            .Take(Math.Clamp(_options.MaxSegments, 1, 20_000))
            .ToArray();
        if (segments.Length == 0)
        {
            return null;
        }

        var transcript = LimitText(
            string.Join(' ', segments.Select(segment => segment.Text)),
            Math.Clamp(_options.MaxTranscriptCharacters, 1_000, 100_000));
        if (transcript is null)
        {
            return null;
        }

        return new YouTubeTranscriptResponse(
            videoId,
            videoUrl,
            languageCode,
            ProviderName,
            _timeProvider.GetUtcNow().UtcDateTime,
            segments,
            transcript);
    }

    private IEnumerable<YouTubeTranscriptSegmentDto> ParseSegments(JsonElement item)
    {
        foreach (var propertyName in new[] { "transcript", "searchResult", "segments", "captions" })
        {
            if (!TryGetProperty(item, propertyName, out var collection)
                || collection.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var parsedAny = false;
            foreach (var segment in collection.EnumerateArray())
            {
                var parsed = ParseSegment(segment);
                if (parsed is not null)
                {
                    parsedAny = true;
                    yield return parsed;
                }
            }

            if (parsedAny)
            {
                yield break;
            }
        }
    }

    private YouTubeTranscriptSegmentDto? ParseSegment(JsonElement segment)
    {
        if (segment.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var text = NormalizeText(
            GetString(segment, "text", "content"),
            Math.Clamp(_options.MaxSegmentTextCharacters, 1, 10_000));
        if (text is null)
        {
            return null;
        }

        return new YouTubeTranscriptSegmentDto(
            ParseSeconds(segment, "start", "startTime", "startSeconds"),
            ParseSeconds(segment, "dur", "duration", "durationSeconds"),
            text);
    }

    private static string? GetString(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!TryGetProperty(element, propertyName, out var value))
            {
                continue;
            }

            var result = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.ToString(),
                _ => null
            };
            if (!string.IsNullOrWhiteSpace(result))
            {
                return result;
            }
        }

        return null;
    }

    private static decimal? ParseSeconds(JsonElement element, params string[] propertyNames)
    {
        var value = GetString(element, propertyNames);
        return decimal.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var seconds)
            && seconds >= 0
            ? seconds
            : null;
    }

    private static bool TryGetProperty(
        JsonElement element,
        string propertyName,
        out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string? NormalizeText(string? value, int maxLength)
    {
        var normalized = string.Join(
            ' ',
            (value ?? string.Empty).Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return normalized.Length == 0
            ? null
            : normalized.Length <= maxLength
                ? normalized
                : normalized[..maxLength];
    }

    private static string? LimitText(string value, int maxLength)
        => NormalizeText(value, maxLength);

    private string NormalizeLanguageCode(string? value)
    {
        var normalized = (string.IsNullOrWhiteSpace(value)
                ? _options.DefaultTargetLanguage
                : value)
            .Trim();
        if (normalized.Length is < 2 or > 10
            || normalized.Any(character =>
                !(character is >= 'a' and <= 'z') && character != '-'))
        {
            throw new ArgumentException(
                "YouTube 자막 언어는 ISO 639-1 또는 언어 변형 코드여야 합니다.",
                nameof(value));
        }

        return normalized;
    }

    private static string NormalizeVideoId(string value)
    {
        var normalized = value.Trim();
        if (normalized.Length is < 1 or > 100
            || normalized.Any(character =>
                !(character is >= 'A' and <= 'Z')
                && !(character is >= 'a' and <= 'z')
                && !(character is >= '0' and <= '9')
                && character is not '_' and not '-'))
        {
            throw new ArgumentException(
                "YouTube VideoId는 영상 ID 형식이어야 합니다.",
                nameof(value));
        }

        return normalized;
    }

    private void EnsureEnabled()
    {
        if (!_options.Enabled)
        {
            throw new InvalidOperationException("Apify YouTube 자막 조회가 비활성화되어 있습니다.");
        }
    }
}
