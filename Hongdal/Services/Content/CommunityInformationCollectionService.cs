using Hongdal.Contracts.Common.Content;

namespace Hongdal.Services.Content;

public interface ICommunityInformationCandidateSource
{
    CommunityInformationSourceDto Source { get; }

    Task<IReadOnlyList<CommunityInformationCandidateDto>> ReadAsync(
        CommunityInformationCollectionQuery query,
        CancellationToken cancellationToken = default);
}

public interface ICommunityInformationCollectionService
{
    IReadOnlyList<CommunityInformationSourceDto> GetSources();

    Task<CommunityInformationCollectionResponse> ReadAsync(
        CommunityInformationCollectionQuery query,
        CancellationToken cancellationToken = default);
}

public sealed class CommunityInformationCollectionService : ICommunityInformationCollectionService
{
    private readonly IReadOnlyDictionary<string, ICommunityInformationCandidateSource> _sources;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CommunityInformationCollectionService> _logger;

    public CommunityInformationCollectionService(
        IEnumerable<ICommunityInformationCandidateSource> sources,
        TimeProvider timeProvider,
        ILogger<CommunityInformationCollectionService> logger)
    {
        var sourceList = sources.ToArray();
        var duplicate = sourceList
            .GroupBy(source => source.Source.SourceKey, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"커뮤니티 정보 원천 키가 중복 등록되었습니다. SourceKey={duplicate.Key}");
        }

        _sources = sourceList.ToDictionary(
            source => source.Source.SourceKey,
            StringComparer.OrdinalIgnoreCase);
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public IReadOnlyList<CommunityInformationSourceDto> GetSources()
        => _sources.Values
            .Select(source => source.Source)
            .OrderBy(source => source.SourceType)
            .ThenBy(source => source.DisplayName)
            .ToArray();

    public async Task<CommunityInformationCollectionResponse> ReadAsync(
        CommunityInformationCollectionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var selectedSources = string.IsNullOrWhiteSpace(query.SourceKey)
            ? _sources.Values
                .Where(source => !string.Equals(
                    source.Source.CollectionMode,
                    CommunityInformationCollectionModes.OnDemandPublicDataQuery,
                    StringComparison.Ordinal))
                .ToArray()
            : _sources.TryGetValue(query.SourceKey.Trim(), out var selected)
                ? [selected]
                : [];
        var candidates = new List<CommunityInformationCandidateDto>();
        var failures = new List<CommunityInformationSourceFailureDto>();

        foreach (var source in selectedSources)
        {
            try
            {
                candidates.AddRange(await source.ReadAsync(query, cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "커뮤니티 정보 후보 원천 조회에 실패했습니다. SourceKey={SourceKey}",
                    source.Source.SourceKey);
                failures.Add(new CommunityInformationSourceFailureDto(
                    source.Source.SourceKey,
                    "자료 원천을 조회하지 못했습니다."));
            }
        }

        var take = Math.Clamp(query.Take, 1, 100);
        var filtered = candidates
            .Where(candidate => MatchesCountry(candidate, query.CountryCode))
            .Where(candidate => MatchesReviewState(candidate, query.ReviewState))
            .Where(candidate => MatchesSearchText(candidate, query.SearchText))
            .Where(candidate => MatchesDateRange(candidate, query.StartDate, query.EndDate))
            .GroupBy(candidate => candidate.CandidateKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(GetSortTime)
            .ThenBy(candidate => candidate.SourceKey)
            .ThenBy(candidate => candidate.CandidateKey)
            .Take(take)
            .ToArray();

        return new CommunityInformationCollectionResponse(
            _timeProvider.GetUtcNow().UtcDateTime,
            GetSources(),
            filtered,
            failures);
    }

    private static bool MatchesCountry(
        CommunityInformationCandidateDto candidate,
        string? countryCode)
        => string.IsNullOrWhiteSpace(countryCode)
           || string.Equals(
               candidate.CountryCode,
               countryCode.Trim(),
               StringComparison.OrdinalIgnoreCase);

    private static bool MatchesReviewState(
        CommunityInformationCandidateDto candidate,
        string? reviewState)
        => string.IsNullOrWhiteSpace(reviewState)
           || string.Equals(
               candidate.ReviewState,
               reviewState.Trim(),
               StringComparison.OrdinalIgnoreCase);

    private static bool MatchesSearchText(
        CommunityInformationCandidateDto candidate,
        string? searchText)
    {
        var term = searchText?.Trim();
        return string.IsNullOrWhiteSpace(term)
               || candidate.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
               || candidate.Summary.Contains(term, StringComparison.OrdinalIgnoreCase)
               || candidate.Provider.Contains(term, StringComparison.OrdinalIgnoreCase)
               || candidate.TopicTags.Any(tag => tag.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesDateRange(
        CommunityInformationCandidateDto candidate,
        DateOnly? startDate,
        DateOnly? endDate)
    {
        var candidateStartDate = GetCandidateDate(candidate);
        var candidateEndDate = candidate.ReferencePeriodEndDate.HasValue
                               && candidate.ReferencePeriodEndDate.Value >= candidateStartDate
            ? candidate.ReferencePeriodEndDate.Value
            : candidateStartDate;
        return (!startDate.HasValue || candidateEndDate >= startDate.Value)
               && (!endDate.HasValue || candidateStartDate <= endDate.Value);
    }

    private static DateOnly GetCandidateDate(CommunityInformationCandidateDto candidate)
        => candidate.ReferenceDate
           ?? (candidate.PublishedAtUtc.HasValue
               ? DateOnly.FromDateTime(candidate.PublishedAtUtc.Value)
               : DateOnly.FromDateTime(candidate.CollectedAtUtc));

    private static DateTime GetSortTime(CommunityInformationCandidateDto candidate)
    {
        if (candidate.PublishedAtUtc.HasValue)
        {
            return candidate.PublishedAtUtc.Value;
        }

        if (candidate.ReferenceDate.HasValue)
        {
            return DateTime.SpecifyKind(
                candidate.ReferenceDate.Value.ToDateTime(TimeOnly.MinValue),
                DateTimeKind.Utc);
        }

        return candidate.CollectedAtUtc;
    }
}
