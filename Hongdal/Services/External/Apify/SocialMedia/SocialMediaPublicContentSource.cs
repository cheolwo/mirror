using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hongdal.Contracts.Common.Content;
using 홍달.Services.Options;

namespace Hongdal.Services.External.Apify.SocialMedia;

public sealed record SocialMediaPublicContentQuery(
    IReadOnlyList<string> SearchTerms,
    IReadOnlyList<string> StartUrls,
    int Take,
    string CountryCode,
    string LanguageCode);

public interface ISocialMediaPublicContentSource
{
    CommunityInformationSourceDto Source { get; }

    bool IsEnabled { get; }

    SocialMediaResearchSourceDto Describe();

    Task<IReadOnlyList<CommunityInformationCandidateDto>> SearchAsync(
        SocialMediaPublicContentQuery query,
        CancellationToken cancellationToken);
}

public abstract class ApifySocialMediaPublicContentSource : ISocialMediaPublicContentSource
{
    private readonly IApifyActorGateway _gateway;
    private readonly ApifySocialMediaOptions _moduleOptions;
    private readonly ApifySocialMediaProviderOptions _providerOptions;
    private readonly TimeProvider _timeProvider;

    protected ApifySocialMediaPublicContentSource(
        IApifyActorGateway gateway,
        ApifySocialMediaOptions moduleOptions,
        ApifySocialMediaProviderOptions providerOptions,
        TimeProvider timeProvider)
    {
        _gateway = gateway;
        _moduleOptions = moduleOptions;
        _providerOptions = providerOptions;
        _timeProvider = timeProvider;
    }

    public abstract CommunityInformationSourceDto Source { get; }

    protected abstract IReadOnlySet<string> AllowedHosts { get; }

    protected abstract bool SupportsKeywordSearch { get; }

    protected abstract bool RequiresStartUrl { get; }

    public bool IsEnabled => _moduleOptions.Enabled && _providerOptions.Enabled;

    public SocialMediaResearchSourceDto Describe()
        => new(
            Source.SourceKey,
            Source.Provider,
            Source.DisplayName,
            Source.DocumentationUrl,
            IsEnabled,
            SupportsKeywordSearch,
            RequiresStartUrl);

    public async Task<IReadOnlyList<CommunityInformationCandidateDto>> SearchAsync(
        SocialMediaPublicContentQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        EnsureEnabled();

        var terms = NormalizeTerms(query.SearchTerms);
        var startUrls = NormalizeStartUrls(
            (_providerOptions.DefaultStartUrls ?? []).Concat(query.StartUrls ?? []));
        if (RequiresStartUrl && startUrls.Count == 0)
        {
            throw new ArgumentException($"{Source.Provider} 조사는 공개 페이지 URL이 필요합니다.");
        }

        if (!SupportsKeywordSearch && startUrls.Count == 0)
        {
            throw new ArgumentException($"{Source.Provider} 조사 대상 URL이 필요합니다.");
        }

        if (SupportsKeywordSearch && startUrls.Count == 0 && terms.Count == 0)
        {
            throw new ArgumentException($"{Source.Provider} 조사 검색어 또는 URL이 필요합니다.");
        }

        var take = Math.Clamp(
            query.Take,
            1,
            Math.Clamp(_providerOptions.MaxDatasetItems, 1, 100));
        var normalizedQuery = query with
        {
            SearchTerms = terms,
            StartUrls = startUrls,
            Take = take,
            CountryCode = NormalizeCountryCode(query.CountryCode),
            LanguageCode = NormalizeLanguageCode(query.LanguageCode)
        };
        var result = await _gateway.RunSyncGetDatasetItemsAsync(
            new ApifyActorSyncRequest(
                _providerOptions.ActorId,
                BuildActorInput(normalizedQuery),
                _providerOptions.ActorTimeoutSeconds,
                _providerOptions.MemoryMegabytes,
                take,
                _providerOptions.MaxTotalChargeUsd),
            cancellationToken);
        var collectedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        return result.Items
            .Select(item => MapItem(item, normalizedQuery, collectedAtUtc))
            .Where(item => item is not null)
            .Cast<CommunityInformationCandidateDto>()
            .GroupBy(item => item.CandidateKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(take)
            .ToArray();
    }

    protected abstract JsonElement BuildActorInput(SocialMediaPublicContentQuery query);

    protected abstract CommunityInformationCandidateDto? MapItem(
        JsonElement item,
        SocialMediaPublicContentQuery query,
        DateTime collectedAtUtc);

    protected CommunityInformationCandidateDto? CreateCandidate(
        SocialMediaPublicContentQuery query,
        DateTime collectedAtUtc,
        string? itemId,
        string? author,
        string? title,
        string? summary,
        string? originalUrl,
        string? thumbnailUrl,
        DateTime? publishedAtUtc,
        string? detectedLanguageCode,
        IEnumerable<string>? tags = null)
    {
        var normalizedUrl = SocialMediaJson.NormalizeHttpsUrl(originalUrl);
        if (normalizedUrl is null
            || !Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var originalUri)
            || !AllowedHosts.Any(host =>
                string.Equals(originalUri.Host, host, StringComparison.OrdinalIgnoreCase)
                || originalUri.Host.EndsWith($".{host}", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var normalizedSummary = SocialMediaJson.NormalizeText(summary, 500);
        var normalizedTitle = SocialMediaJson.NormalizeText(title, 200)
                              ?? SocialMediaJson.BuildTitle(author, normalizedSummary);
        if (normalizedUrl is null || normalizedTitle is null || normalizedSummary is null)
        {
            return null;
        }

        var normalizedAuthor = SocialMediaJson.NormalizeText(author, 100);
        var provider = normalizedAuthor is null
            ? Source.Provider
            : $"{Source.Provider} · {normalizedAuthor}";
        var identity = SocialMediaJson.NormalizeText(itemId, 200)
                       ?? SocialMediaJson.StableId(normalizedUrl);
        var topicTags = query.SearchTerms
            .Concat(tags ?? [])
            .Append(Source.Provider)
            .Select(tag => SocialMediaJson.NormalizeText(tag, 80))
            .Where(tag => tag is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToArray();

        return new CommunityInformationCandidateDto(
            $"{Source.SourceKey}:{identity}",
            Source.SourceKey,
            CommunityInformationSourceTypes.SocialMedia,
            provider,
            normalizedTitle,
            normalizedSummary,
            normalizedUrl,
            SocialMediaJson.NormalizeHttpsUrl(thumbnailUrl),
            publishedAtUtc,
            publishedAtUtc.HasValue ? DateOnly.FromDateTime(publishedAtUtc.Value) : null,
            collectedAtUtc,
            query.CountryCode,
            NormalizeLanguageCode(detectedLanguageCode ?? query.LanguageCode),
            null,
            null,
            CommunityInformationReviewStates.PendingReview,
            topicTags,
            $"Apify Actor로 수집한 {Source.Provider} 공개 게시물의 짧은 발췌와 원문 링크입니다.",
            "검색 결과는 전체 여론을 대표하지 않고, 작성자의 주장·신원·국가·상품 사실을 홍달이 확인했다는 뜻이 아닙니다. 원문과 권리·개인정보 조건을 운영자가 다시 확인해야 합니다.");
    }

    private IReadOnlyList<string> NormalizeTerms(IEnumerable<string>? values)
        => (values ?? [])
            .Select(value => SocialMediaJson.NormalizeText(value, 160))
            .Where(value => value is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(_moduleOptions.MaxSearchTerms, 1, 20))
            .ToArray();

    private IReadOnlyList<string> NormalizeStartUrls(IEnumerable<string>? values)
    {
        var result = new List<string>();
        foreach (var value in values ?? [])
        {
            if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
                || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                || !AllowedHosts.Any(host =>
                    string.Equals(uri.Host, host, StringComparison.OrdinalIgnoreCase)
                    || uri.Host.EndsWith($".{host}", StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException($"{Source.Provider} 공개 HTTPS URL만 조사할 수 있습니다: {value}");
            }

            result.Add(uri.AbsoluteUri);
        }

        return result
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(_moduleOptions.MaxStartUrlsPerSource, 1, 20))
            .ToArray();
    }

    private void EnsureEnabled()
    {
        if (!_moduleOptions.Enabled)
        {
            throw new InvalidOperationException("Apify SNS 공개 자료 조사가 비활성화되어 있습니다.");
        }

        if (!_providerOptions.Enabled)
        {
            throw new InvalidOperationException($"{Source.Provider} 공개 자료 조사가 비활성화되어 있습니다.");
        }

        if (string.IsNullOrWhiteSpace(_providerOptions.ActorId))
        {
            throw new InvalidOperationException($"{Source.Provider} Apify ActorId 설정이 필요합니다.");
        }
    }

    private static string NormalizeCountryCode(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        return normalized?.Length == 2 ? normalized : "ZZ";
    }

    protected static string NormalizeLanguageCode(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) || normalized.Length > 20
            ? "und"
            : normalized;
    }
}

internal static class SocialMediaJson
{
    public static string? GetString(JsonElement item, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (TryGetProperty(item, propertyName, out var value))
            {
                var result = value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString(),
                    JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToString(),
                    _ => null
                };
                if (!string.IsNullOrWhiteSpace(result))
                {
                    return result;
                }
            }
        }

        return null;
    }

    public static string? GetNestedString(
        JsonElement item,
        string objectPropertyName,
        params string[] propertyNames)
        => TryGetProperty(item, objectPropertyName, out var value)
           && value.ValueKind == JsonValueKind.Object
            ? GetString(value, propertyNames)
            : null;

    public static IReadOnlyList<string> GetStringArray(JsonElement item, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!TryGetProperty(item, propertyName, out var value)
                || value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            return value.EnumerateArray()
                .Where(element => element.ValueKind == JsonValueKind.String)
                .Select(element => NormalizeText(element.GetString(), 2_000))
                .Where(text => text is not null)
                .Cast<string>()
                .ToArray();
        }

        return [];
    }

    public static string? GetFirstArrayObjectString(
        JsonElement item,
        string arrayPropertyName,
        params string[] propertyNames)
    {
        if (!TryGetProperty(item, arrayPropertyName, out var array)
            || array.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var value = GetString(element, propertyNames);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    public static DateTime? GetDateTime(JsonElement item, params string[] propertyNames)
    {
        var value = GetString(item, propertyNames);
        if (DateTimeOffset.TryParse(
                value,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AllowWhiteSpaces,
                out var timestamp))
        {
            return timestamp.UtcDateTime;
        }

        foreach (var propertyName in propertyNames)
        {
            if (TryGetProperty(item, propertyName, out var number)
                && number.ValueKind == JsonValueKind.Number
                && number.TryGetInt64(out var unixSeconds))
            {
                try
                {
                    return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
                }
                catch (ArgumentOutOfRangeException)
                {
                    return null;
                }
            }
        }

        return null;
    }

    public static string? NormalizeText(string? value, int maxLength)
    {
        var normalized = string.Join(
            ' ',
            (value ?? string.Empty).Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (normalized.Length == 0)
        {
            return null;
        }

        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    public static string? NormalizeHttpsUrl(string? value)
        => Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
           && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            ? uri.AbsoluteUri
            : null;

    public static string? BuildTitle(string? author, string? summary)
    {
        var normalizedSummary = NormalizeText(summary, 120);
        if (normalizedSummary is null)
        {
            return null;
        }

        var normalizedAuthor = NormalizeText(author, 60);
        return normalizedAuthor is null
            ? normalizedSummary
            : $"{normalizedAuthor}: {normalizedSummary}";
    }

    public static string StableId(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash.AsSpan(0, 10)).ToLowerInvariant();
    }

    private static bool TryGetProperty(JsonElement item, string propertyName, out JsonElement value)
    {
        if (item.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in item.EnumerateObject())
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
}
