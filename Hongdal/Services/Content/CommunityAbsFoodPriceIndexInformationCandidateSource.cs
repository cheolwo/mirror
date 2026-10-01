using System.Globalization;
using Hongdal.Contracts.Common.AgriculturalFisheries;
using Hongdal.Contracts.Common.Content;
using Hongdal.Services.AgriculturalFisheries.Information;

namespace Hongdal.Services.Content;

public sealed class CommunityAbsFoodPriceIndexInformationCandidateSource
    : ICommunityInformationCandidateSource
{
    private const string DocumentationUrl =
        "https://www.abs.gov.au/statistics/application-programming-interfaces-apis/data-api-user-guide";
    private readonly I호주농수산식품가격조회Service _service;

    public CommunityAbsFoodPriceIndexInformationCandidateSource(
        I호주농수산식품가격조회Service service)
    {
        _service = service;
    }

    public CommunityInformationSourceDto Source { get; } = new(
        CommunityInformationSourceKeys.AbsFoodPriceIndex,
        CommunityInformationSourceTypes.PublicData,
        "Australian Bureau of Statistics (ABS)",
        "ABS 호주 식품 소비자물가지수",
        CommunityInformationCollectionModes.OnDemandPublicDataQuery,
        "월별, 선택 기간 조회 시 ABS Data API 호출",
        "선택 기간과 식품 지수 항목을 명시했을 때만 조회하며 실제 A$/kg 가격으로 표현하지 않습니다.",
        DocumentationUrl,
        true);

    public async Task<IReadOnlyList<CommunityInformationCandidateDto>> ReadAsync(
        CommunityInformationCollectionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!MatchesAustraliaSource(query.CountryCode)
            || !MatchesOfficialObservation(query.ReviewState))
        {
            return [];
        }

        var index = ResolveIndex(query.SearchText);
        if (index is null)
        {
            return [];
        }

        var startDate = query.StartDate ?? DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-11);
        var endDate = query.EndDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var response = await _service.조회Async(
            new 호주농수산식품가격조회요청
            {
                SourceKey = 호주농수산식품가격출처Keys.AbsConsumerPriceIndex,
                IndexCode = index.Code,
                MeasureCode = 호주식품가격지수측정Codes.IndexNumber,
                RegionCode = 호주식품가격지수지역Codes.Australia,
                StartPeriod = startDate.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                EndPeriod = endDate.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                MaxItems = Math.Clamp(query.Take, 1, 100)
            },
            cancellationToken);
        if (!response.Success)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(response.ErrorMessage)
                    ? "ABS 식품 소비자물가지수를 조회하지 못했습니다."
                    : response.ErrorMessage);
        }

        return response.Items
            .Select(item => ToCandidate(item, response.CollectedAtUtc))
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .Where(candidate => OverlapsRange(candidate, query.StartDate, query.EndDate))
            .Take(Math.Clamp(query.Take, 1, 100))
            .ToArray();
    }

    private 호주식품가격지수선택항목? ResolveIndex(string? searchText)
    {
        var indexes = _service.GetCatalog().Indexes;
        var term = searchText?.Trim();
        if (string.IsNullOrWhiteSpace(term))
        {
            return indexes.FirstOrDefault(item => string.Equals(
                item.Code,
                호주식품가격지수Codes.FoodAndNonAlcoholicBeverages,
                StringComparison.Ordinal));
        }

        return indexes.FirstOrDefault(item => string.Equals(item.Code, term, StringComparison.OrdinalIgnoreCase))
               ?? indexes.FirstOrDefault(item => item.Label.Contains(term, StringComparison.OrdinalIgnoreCase))
               ?? indexes.FirstOrDefault(item => item.OfficialLabel.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static CommunityInformationCandidateDto? ToCandidate(
        호주농수산식품가격항목 item,
        DateTime collectedAtUtc)
    {
        if (!TryResolveReferenceMonth(item.ReferencePeriod, out var startDate, out var endDate)
            || !item.NumericValue.HasValue)
        {
            return null;
        }

        var unit = string.IsNullOrWhiteSpace(item.UnitLabel) ? "지수" : item.UnitLabel.Trim();
        var basePeriod = string.IsNullOrWhiteSpace(item.BasePeriod)
            ? string.Empty
            : $" · 기준 {item.BasePeriod.Trim()}";
        return new CommunityInformationCandidateDto(
            $"abs-cpi:{item.IndexCode}:{item.MeasureCode}:{item.RegionCode}:{startDate:yyyyMM}",
            CommunityInformationSourceKeys.AbsFoodPriceIndex,
            CommunityInformationSourceTypes.PublicData,
            "Australian Bureau of Statistics (ABS)",
            $"{item.IndexLabel} · {item.RegionLabel}",
            $"{startDate:yyyy년 M월} · {item.MeasureLabel} {item.NumericValue.Value.ToString("N2", CultureInfo.InvariantCulture)} {unit}{basePeriod}",
            DocumentationUrl,
            null,
            null,
            startDate,
            DateTime.SpecifyKind(collectedAtUtc, DateTimeKind.Utc),
            "AU",
            "en",
            null,
            unit,
            CommunityInformationReviewStates.OfficialObservation,
            ["농수산물", "호주", "소비자물가지수", item.IndexCode, item.IndexLabel, item.OfficialIndexLabel],
            "Australian Bureau of Statistics Data API에서 선택 기간에 조회한 월별 공식 지수입니다. Based on Australian Bureau of Statistics data.",
            "실제 A$/kg 가격이 아니라 기준시점 대비 소비자 가격 변동 지수입니다. 같은 지수·측정방식·지역·기준시점 계열만 비교하며 도시 간 절대 가격이나 공동구매 매입가로 해석하지 않습니다.",
            item.NumericValue,
            item.MeasureLabel,
            $"abs-cpi|{item.IndexCode}|{item.MeasureCode}|{item.RegionCode}|{item.UnitCode}",
            endDate,
            $"{item.IndexLabel} · {item.MeasureLabel} · {item.RegionLabel}");
    }

    private static bool TryResolveReferenceMonth(
        string? value,
        out DateOnly startDate,
        out DateOnly endDate)
    {
        startDate = default;
        endDate = default;
        if (!DateTime.TryParseExact(
                value?.Trim(),
                ["yyyy-MM", "yyyy-MM-dd"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return false;
        }

        startDate = new DateOnly(parsed.Year, parsed.Month, 1);
        endDate = new DateOnly(parsed.Year, parsed.Month, DateTime.DaysInMonth(parsed.Year, parsed.Month));
        return true;
    }

    private static bool OverlapsRange(
        CommunityInformationCandidateDto candidate,
        DateOnly? startDate,
        DateOnly? endDate)
    {
        var candidateStart = candidate.ReferenceDate!.Value;
        var candidateEnd = candidate.ReferencePeriodEndDate ?? candidateStart;
        return (!startDate.HasValue || candidateEnd >= startDate.Value)
               && (!endDate.HasValue || candidateStart <= endDate.Value);
    }

    private static bool MatchesAustraliaSource(string? countryCode)
        => string.IsNullOrWhiteSpace(countryCode)
           || string.Equals(countryCode.Trim(), "AU", StringComparison.OrdinalIgnoreCase);

    private static bool MatchesOfficialObservation(string? reviewState)
        => string.IsNullOrWhiteSpace(reviewState)
           || string.Equals(
               reviewState.Trim(),
               CommunityInformationReviewStates.OfficialObservation,
               StringComparison.OrdinalIgnoreCase);
}
