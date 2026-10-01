using System.Globalization;
using Hongdal.Contracts.Common.Content;
using 홍달.Services.External.PublicData;

namespace Hongdal.Services.Content;

public sealed class CommunityFishCooperativeInformationCandidateSource
    : ICommunityInformationCandidateSource
{
    private const int MaximumMonthCount = 13;
    private const string DocumentationUrl = "https://www.data.go.kr/data/15061340/openapi.do";
    private readonly IFishCooperativeStatisticsClient _client;
    private readonly TimeProvider _timeProvider;

    public CommunityFishCooperativeInformationCandidateSource(
        IFishCooperativeStatisticsClient client,
        TimeProvider timeProvider)
    {
        _client = client;
        _timeProvider = timeProvider;
    }

    public CommunityInformationSourceDto Source { get; } = new(
        CommunityInformationSourceKeys.FishCooperativeGeneralStatistics,
        CommunityInformationSourceTypes.PublicData,
        "금융위원회 금융통계",
        "수산업협동조합 월별 임직원 통계",
        CommunityInformationCollectionModes.OnDemandPublicDataQuery,
        "공공데이터포털 기준년월별 조회",
        "공식 일반현황이라도 같은 조합·같은 임직원 구분의 기준월 값만 비교하고, 현재 영업·제휴·물류 역량으로 해석하지 않습니다.",
        DocumentationUrl,
        true);

    public async Task<IReadOnlyList<CommunityInformationCandidateDto>> ReadAsync(
        CommunityInformationCollectionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!MatchesKoreanSource(query.CountryCode)
            || !MatchesOfficialObservation(query.ReviewState))
        {
            return [];
        }

        var months = ResolveMonths(query);
        var collectedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var candidates = new List<CommunityInformationCandidateDto>();
        foreach (var month in months)
        {
            var items = await _client.FetchGeneralStatisticsAsync(month, cancellationToken);
            candidates.AddRange(items
                .Where(item => MatchesSearchText(item, query.SearchText))
                .Select(item => ToCandidate(item, month, collectedAtUtc)));
        }

        return candidates
            .GroupBy(candidate => candidate.CandidateKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(candidate => candidate.ReferenceDate)
            .ThenBy(candidate => candidate.Title)
            .Take(Math.Clamp(query.Take, 1, 100))
            .ToArray();
    }

    private IReadOnlyList<DateOnly> ResolveMonths(CommunityInformationCollectionQuery query)
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var start = query.StartDate ?? query.EndDate ?? today;
        var end = query.EndDate ?? query.StartDate ?? today;
        if (start > end)
        {
            return [];
        }

        var cursor = new DateOnly(start.Year, start.Month, 1);
        var finalMonth = new DateOnly(end.Year, end.Month, 1);
        var months = new List<DateOnly>();
        while (cursor <= finalMonth)
        {
            months.Add(cursor);
            if (months.Count > MaximumMonthCount)
            {
                throw new InvalidOperationException(
                    $"수산업협동조합 월별 통계는 한 번에 최대 {MaximumMonthCount}개월까지 조회할 수 있습니다.");
            }

            cursor = cursor.AddMonths(1);
        }

        return months;
    }

    private static CommunityInformationCandidateDto ToCandidate(
        FishCooperativeGeneralStatisticsItem item,
        DateOnly requestedMonth,
        DateTime collectedAtUtc)
    {
        var referenceMonth = ParseBaseMonth(item.BaseYearMonth) ?? requestedMonth;
        var referenceEndDate = new DateOnly(
            referenceMonth.Year,
            referenceMonth.Month,
            DateTime.DaysInMonth(referenceMonth.Year, referenceMonth.Month));
        var classificationName = string.IsNullOrWhiteSpace(item.EmployeeClassificationName)
            ? "임직원"
            : item.EmployeeClassificationName;
        var financialCompanyKey = string.IsNullOrWhiteSpace(item.FinancialCompanyCode)
            ? Uri.EscapeDataString(item.FinancialCompanyName)
            : Uri.EscapeDataString(item.FinancialCompanyCode);
        var classificationKey = string.IsNullOrWhiteSpace(item.EmployeeClassificationCode)
            ? Uri.EscapeDataString(classificationName)
            : Uri.EscapeDataString(item.EmployeeClassificationCode);
        var employeeCount = item.EmployeeCount!.Value;

        return new CommunityInformationCandidateDto(
            $"fish-coop:{referenceMonth:yyyyMM}:{financialCompanyKey}:{classificationKey}",
            CommunityInformationSourceKeys.FishCooperativeGeneralStatistics,
            CommunityInformationSourceTypes.PublicData,
            "금융위원회 금융통계",
            $"{item.FinancialCompanyName} · {classificationName}",
            $"{referenceMonth:yyyy년 M월} · {employeeCount.ToString("N0", CultureInfo.InvariantCulture)}명"
            + (string.IsNullOrWhiteSpace(item.Title) ? string.Empty : $" · {item.Title}"),
            DocumentationUrl,
            null,
            null,
            referenceMonth,
            collectedAtUtc,
            "KR",
            "ko",
            null,
            "명",
            CommunityInformationReviewStates.OfficialObservation,
            new[] { "수산업협동조합", "수협", "금융통계", classificationName, item.FinancialCompanyName }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            "금융위원회 금융통계 수산업협동조합 일반현황 Open API의 기준년월별 임직원 관측값입니다.",
            "기준월 공시 일반현황이며 현재 인력, 영업상태, 플랫폼 제휴, 재무건전성 또는 물류 수행능력을 뜻하지 않습니다. 같은 조합과 같은 임직원 구분만 시계열로 비교해야 합니다.",
            employeeCount,
            classificationName,
            $"fish-coop|{financialCompanyKey}|{classificationKey}|employee-count",
            referenceEndDate,
            $"{item.FinancialCompanyName} · {classificationName}");
    }

    private static DateOnly? ParseBaseMonth(string value)
    {
        var normalized = value.Trim();
        if (normalized.Length != 6
            || !int.TryParse(normalized[..4], NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(normalized[4..], NumberStyles.None, CultureInfo.InvariantCulture, out var month)
            || year is < 1 or > 9999
            || month is < 1 or > 12)
        {
            return null;
        }

        return new DateOnly(year, month, 1);
    }

    private static bool MatchesSearchText(
        FishCooperativeGeneralStatisticsItem item,
        string? searchText)
    {
        var term = searchText?.Trim();
        return string.IsNullOrWhiteSpace(term)
               || item.FinancialCompanyName.Contains(term, StringComparison.OrdinalIgnoreCase)
               || item.FinancialCompanyCode.Contains(term, StringComparison.OrdinalIgnoreCase)
               || item.EmployeeClassificationName.Contains(term, StringComparison.OrdinalIgnoreCase)
               || item.Title.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesKoreanSource(string? countryCode)
        => string.IsNullOrWhiteSpace(countryCode)
           || string.Equals(countryCode.Trim(), "KR", StringComparison.OrdinalIgnoreCase);

    private static bool MatchesOfficialObservation(string? reviewState)
        => string.IsNullOrWhiteSpace(reviewState)
           || string.Equals(
               reviewState.Trim(),
               CommunityInformationReviewStates.OfficialObservation,
               StringComparison.OrdinalIgnoreCase);
}
