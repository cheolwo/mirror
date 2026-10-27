using Ssalddel.Contracts.Common.AgriculturalFisheries;
using Ssalddel.Domain.AgriculturalFisheries;
using 살뜰.Services.External.PublicData;

namespace Ssalddel.Services.AgriculturalFisheries.Information;

// 이미 읽은 관측·매핑 자료를 원 통화·단위와 근거를 보존해 표시한다.
internal static class Kamis중심UsdaAms가격Projector
{
    internal sealed record KamisAnchor(
        string CategoryCode,
        string CategoryName,
        string ItemCode,
        string ItemName,
        DateOnly LatestSurveyDate);

    internal sealed record PreparedAnchor(
        KamisAnchor Anchor,
        Kamis중심UsdaAms품목MappingResolution Mapping);

    internal sealed record MarketStageDefinition(
        string Code,
        string Label);

    internal sealed class KamisLatestObservation
    {
        public long Id { get; init; }

        public string ItemCode { get; init; } = string.Empty;

        public string FrequencyCode { get; init; } = string.Empty;

        public string ProductClassCode { get; init; } = string.Empty;

        public string ProductClassName { get; init; } = string.Empty;

        public DateOnly SurveyDate { get; init; }

        public string KindCode { get; init; } = string.Empty;

        public string KindName { get; init; } = string.Empty;

        public string RankCode { get; init; } = string.Empty;

        public string RankName { get; init; } = string.Empty;

        public string Unit { get; init; } = string.Empty;

        public string SourcePackageLabel { get; init; } = string.Empty;

        public string ComparisonUnit { get; init; } = string.Empty;

        public string PriceNormalizationCode { get; init; } = string.Empty;

        public string PriceNormalizationBasis { get; init; } = string.Empty;

        public decimal? PriceKrw { get; init; }

        public bool IsPriceMissing { get; init; }
    }

    internal sealed class AmsLatestObservation
    {
        public string RecordKey { get; init; } = string.Empty;

        public string SourceKey { get; init; } = string.Empty;

        public string MarketStageCode { get; init; } = string.Empty;

        public DateOnly ReportBeginDate { get; init; }

        public string Commodity { get; init; } = string.Empty;

        public string Variety { get; init; } = string.Empty;

        public string Grade { get; init; } = string.Empty;

        public string Package { get; init; } = string.Empty;

        public string ItemSize { get; init; } = string.Empty;

        public string Organic { get; init; } = string.Empty;

        public string Origin { get; init; } = string.Empty;

        public string MarketLocationName { get; init; } = string.Empty;

        public string MarketLocationState { get; init; } = string.Empty;

        public decimal? LowPrice { get; init; }

        public decimal? HighPrice { get; init; }

        public decimal? MostlyLowPrice { get; init; }

        public decimal? MostlyHighPrice { get; init; }

        public decimal? WeightedAveragePrice { get; init; }

        public int? StoreCount { get; init; }

        public string CurrencyCode { get; init; } = string.Empty;

        public string OriginalUnit { get; init; } = string.Empty;
    }

    private static readonly IReadOnlyList<MarketStageDefinition> MarketStages =
    [
        new(농수산시세시장단계Codes.산지출하, "미국 산지 출하"),
        new(농수산시세시장단계Codes.도매터미널, "미국 도매 터미널"),
        new(농수산시세시장단계Codes.소매광고, "미국 소매 광고·프로모션")
    ];

    internal static Kamis중심상품코드연결응답 BuildProductCodeConnection(
        PreparedAnchor item,
        IReadOnlyList<FoodPriceCrosswalk> crosswalks)
    {
        var hsCandidates = crosswalks
            .Where(crosswalk => string.Equals(
                crosswalk.AtItemCode,
                item.Anchor.ItemCode,
                StringComparison.Ordinal))
            .OrderBy(crosswalk => crosswalk.HsPrefix, StringComparer.Ordinal)
            .Select(crosswalk => new Kamis중심Hs분류후보응답(
                crosswalk.HsPrefix.Length == 6 ? "HS6" : $"HS{crosswalk.HsPrefix.Length}",
                crosswalk.HsPrefix,
                crosswalk.ProductName,
                Kamis중심상품코드연결상태Codes.후보,
                crosswalk.MatchQualityCode,
                crosswalk.MatchQualityLabel,
                $"{crosswalk.Note} 상품의 재질·가공도·용도에 따라 세번이 달라질 수 있습니다."))
            .ToArray();

        return new Kamis중심상품코드연결응답(
            $"kamis:{item.Anchor.CategoryCode}:{item.Anchor.ItemCode}",
            item.Anchor.CategoryCode,
            item.Anchor.ItemCode,
            Kamis중심상품코드연결상태Codes.확인됨,
            item.Mapping.MatchedCommodities,
            item.Mapping.MatchedCommodities.Count > 0
                ? Kamis중심상품코드연결상태Codes.후보
                : Kamis중심상품코드연결상태Codes.후보없음,
            hsCandidates,
            [
                new Kamis중심국가세번검토응답(
                    "KR",
                    "HSK10",
                    null,
                    Kamis중심상품코드연결상태Codes.전문가검토필요,
                    "한국 수입 신고 세번은 국제 HS 후보를 출발점으로 품목 속성과 관세율표를 확인해야 합니다."),
                new Kamis중심국가세번검토응답(
                    "US",
                    "HTSUS10",
                    null,
                    Kamis중심상품코드연결상태Codes.전문가검토필요,
                    "미국 수입 세번은 국제 HS 후보를 출발점으로 HTSUS와 품목 속성을 확인해야 합니다.")
            ]);
    }

    internal static IReadOnlyList<Kamis중심유통단계가격대응답>
        BuildDistributionStagePriceBands(
            IReadOnlyList<Kamis중심가격Point응답> kamisPoints,
            IReadOnlyList<Kamis중심UsdaAms시장단계가격응답> amsStages)
    {
        var result = new List<Kamis중심유통단계가격대응답>();
        result.AddRange(BuildKamisPriceBands(
            kamisPoints,
            productClassCode: "02",
            stageOrder: 20,
            농수산유통비교단계Codes.도매,
            "도매"));
        result.AddRange(BuildKamisPriceBands(
            kamisPoints,
            productClassCode: "01",
            stageOrder: 30,
            농수산유통비교단계Codes.소매,
            "소매"));

        foreach (var stage in amsStages)
        {
            var (stageOrder, comparisonStageCode, comparisonStageLabel) =
                stage.MarketStageCode switch
                {
                    농수산시세시장단계Codes.산지출하 =>
                        (10, 농수산유통비교단계Codes.산지, "산지·출하"),
                    농수산시세시장단계Codes.도매터미널 =>
                        (20, 농수산유통비교단계Codes.도매, "도매"),
                    _ => (30, 농수산유통비교단계Codes.소매, "소매")
                };
            var groups = stage.PricePoints.Count == 0
                ? new[] { Array.Empty<Kamis중심UsdaAms가격Point응답>() }
                : stage.PricePoints
                    .GroupBy(point => new
                    {
                        point.SourceKey,
                        point.CurrencyCode,
                        point.OriginalUnit
                    })
                    .Select(group => group.ToArray())
                    .ToArray();
            foreach (var points in groups)
            {
                var prices = points
                    .SelectMany(point => new[]
                    {
                        point.LowPrice ?? point.WeightedAveragePrice,
                        point.HighPrice ?? point.WeightedAveragePrice
                    })
                    .Where(price => price.HasValue)
                    .Select(price => price!.Value)
                    .ToArray();
                var first = points.FirstOrDefault();
                result.Add(new Kamis중심유통단계가격대응답(
                    stageOrder,
                    comparisonStageCode,
                    comparisonStageLabel,
                    "US",
                    "미국",
                    first?.SourceKey ?? string.Empty,
                    stage.MarketStageCode,
                    stage.MarketStageLabel,
                    prices.Length > 0
                        ? 농수산유통가격대상태Codes.관측값있음
                        : 농수산유통가격대상태Codes.관측값없음,
                    points
                        .Select(point => (DateOnly?)point.ReferenceDate)
                        .DefaultIfEmpty()
                        .Max(),
                    first?.CurrencyCode ?? string.Empty,
                    first?.OriginalUnit ?? string.Empty,
                    string.IsNullOrWhiteSpace(first?.OriginalUnit)
                        ? []
                        : [first!.OriginalUnit],
                    "SourceUnitPreserved",
                    "USDA AMS 원 통화·포장 단위를 보존하고 서버에서 중량이나 통화를 환산하지 않았습니다.",
                    prices.Length > 0 ? prices.Min() : null,
                    prices.Length > 0 ? prices.Max() : null,
                    points.Length,
                    AllowsDirectComparison: false,
                    "미국 AMS의 원 통화·포장 단위와 시장 단계가 같은 관측끼리만 가격대를 묶었습니다."));
            }
        }

        return result
            .OrderBy(item => item.StageOrder)
            .ThenBy(item => item.CountryCode, StringComparer.Ordinal)
            .ThenBy(item => item.OriginalUnit, StringComparer.Ordinal)
            .ToArray();
    }

    internal static IReadOnlyList<Kamis중심유통단계가격대응답>
        BuildKamisPriceBands(
        IReadOnlyList<Kamis중심가격Point응답> kamisPoints,
        string productClassCode,
        int stageOrder,
        string comparisonStageCode,
        string comparisonStageLabel)
    {
        var points = kamisPoints
            .Where(point => point.ProductClassCode == productClassCode)
            .ToArray();
        var groups = points.Length == 0
            ? new[] { Array.Empty<Kamis중심가격Point응답>() }
            : points
                .GroupBy(
                    point => string.IsNullOrWhiteSpace(point.ComparisonUnit)
                        ? point.Unit
                        : point.ComparisonUnit,
                    StringComparer.Ordinal)
                .Select(group => group.ToArray())
                .ToArray();

        return groups
            .Select(group =>
            {
                var prices = group
                    .Where(point => point.PriceKrw.HasValue && !point.IsPriceMissing)
                    .Select(point => point.PriceKrw!.Value)
                    .ToArray();
                var first = group.FirstOrDefault();
                var unit = string.IsNullOrWhiteSpace(first?.ComparisonUnit)
                    ? first?.Unit ?? string.Empty
                    : first.ComparisonUnit;
                var sourcePackageLabels = group
                    .Select(point => point.SourcePackageLabel)
                    .Where(label => !string.IsNullOrWhiteSpace(label))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(label => label, StringComparer.Ordinal)
                    .ToArray();
                var normalizationCode = group
                    .Select(point => point.PriceNormalizationCode)
                    .FirstOrDefault(code => !string.IsNullOrWhiteSpace(code))
                    ?? "Unverified";
                var normalizationBasis = string.Join(
                    " ",
                    group
                        .Select(point => point.PriceNormalizationBasis)
                        .Where(basis => !string.IsNullOrWhiteSpace(basis))
                        .Distinct(StringComparer.Ordinal));
                return new Kamis중심유통단계가격대응답(
                    stageOrder,
                    comparisonStageCode,
                    comparisonStageLabel,
                    "KR",
                    "한국",
                    "KAMIS",
                    productClassCode,
                    productClassCode == "02" ? "한국 도매" : "한국 소매",
                    prices.Length > 0
                        ? 농수산유통가격대상태Codes.관측값있음
                        : 농수산유통가격대상태Codes.관측값없음,
                    group
                        .Select(point => (DateOnly?)point.SurveyDate)
                        .DefaultIfEmpty()
                        .Max(),
                    prices.Length > 0 ? "KRW" : string.Empty,
                    unit,
                    sourcePackageLabels,
                    normalizationCode,
                    normalizationBasis,
                    prices.Length > 0 ? prices.Min() : null,
                    prices.Length > 0 ? prices.Max() : null,
                    group.Length,
                    AllowsDirectComparison: false,
                    group.Length == 0
                        ? "해당 KAMIS 유통 단계의 관측값이 없습니다."
                        : "KAMIS의 같은 비교 단위와 유통 구분 안에서만 관측 가격대를 묶었고 원 포장 표시는 별도로 보존했습니다.");
            })
            .ToArray();
    }

    internal static bool MatchesQuery(
        PreparedAnchor item,
        Kamis중심UsdaAms가격비교Query query)
    {
        if (!string.IsNullOrWhiteSpace(query.CategoryCode)
            && !string.Equals(
                item.Anchor.CategoryCode,
                query.CategoryCode.Trim(),
                StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.ItemCode)
            && !string.Equals(
                item.Anchor.ItemCode,
                query.ItemCode.Trim(),
                StringComparison.Ordinal))
        {
            return false;
        }

        if (query.OnlyMapped && item.Mapping.MatchedCommodities.Count == 0)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(query.Query))
        {
            return true;
        }

        var value = query.Query.Trim();
        return item.Anchor.ItemCode.Contains(value, StringComparison.OrdinalIgnoreCase)
               || item.Anchor.ItemName.Contains(value, StringComparison.OrdinalIgnoreCase)
               || item.Mapping.MatchedCommodities.Any(commodity =>
                   commodity.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    internal static IReadOnlyList<Kamis중심가격Point응답> BuildKamisPoints(
        IReadOnlyList<KamisLatestObservation> observations,
        int take)
        => OrderKamis(observations)
            .Take(take)
            .Select(item => new Kamis중심가격Point응답(
                item.FrequencyCode,
                item.ProductClassCode,
                item.ProductClassName,
                item.SurveyDate,
                item.KindCode,
                item.KindName,
                item.RankCode,
                item.RankName,
                item.Unit,
                item.SourcePackageLabel,
                item.ComparisonUnit,
                item.PriceNormalizationCode,
                item.PriceNormalizationBasis,
                item.PriceKrw,
                item.IsPriceMissing))
            .ToArray();

    internal static IOrderedEnumerable<KamisLatestObservation> OrderKamis(
        IEnumerable<KamisLatestObservation> observations)
        => observations
            .OrderBy(item => item.ProductClassCode == "02" ? 0 : 1)
            .ThenBy(item => item.KindName)
            .ThenBy(item => item.RankCode)
            .ThenByDescending(item => item.SurveyDate)
            .ThenByDescending(item => item.Id);

    internal static IReadOnlyList<Kamis중심UsdaAms시장단계가격응답>
        BuildAmsStages(
            IReadOnlyList<string> matchedCommodities,
            IReadOnlyList<AmsLatestObservation> latestObservations,
            int take,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = new List<Kamis중심UsdaAms시장단계가격응답>(
            MarketStages.Count);
        var commodityNames = matchedCommodities.ToHashSet(
            StringComparer.OrdinalIgnoreCase);
        foreach (var stage in MarketStages)
        {
            if (matchedCommodities.Count == 0)
            {
                result.Add(new Kamis중심UsdaAms시장단계가격응답(
                    stage.Code,
                    stage.Label,
                    null,
                    []));
                continue;
            }

            var stageObservations = OrderAms(
                    latestObservations.Where(item =>
                    item.MarketStageCode == stage.Code
                    && commodityNames.Contains(item.Commodity)))
                .ToArray();
            var points = stageObservations
                .DistinctBy(item => BuildAmsPointIdentity(
                    item,
                    includeMarketStage: false))
                .Take(take)
                .Select(item => new Kamis중심UsdaAms가격Point응답(
                    item.RecordKey,
                    item.SourceKey,
                    item.MarketStageCode,
                    item.ReportBeginDate,
                    item.Commodity,
                    item.Variety,
                    item.Grade,
                    item.Package,
                    item.ItemSize,
                    item.Organic,
                    item.Origin,
                    item.MarketLocationName,
                    item.MarketLocationState,
                    item.LowPrice,
                    item.HighPrice,
                    item.MostlyLowPrice,
                    item.MostlyHighPrice,
                    item.WeightedAveragePrice,
                    item.StoreCount,
                    item.CurrencyCode,
                    item.OriginalUnit))
                .ToArray();
            result.Add(new Kamis중심UsdaAms시장단계가격응답(
                stage.Code,
                stage.Label,
                stageObservations
                    .Select(item => (DateOnly?)item.ReportBeginDate)
                    .DefaultIfEmpty()
                    .Max(),
                points));
        }

        return result;
    }

    internal static IOrderedEnumerable<AmsLatestObservation> OrderAms(
        IEnumerable<AmsLatestObservation> observations)
        => observations
            .OrderByDescending(item => item.ReportBeginDate)
            .ThenBy(item => item.Commodity)
            .ThenBy(item => item.OriginalUnit)
            .ThenBy(item => item.Variety)
            .ThenBy(item => item.Grade)
            .ThenBy(item => item.Origin)
            .ThenBy(item => item.MarketLocationName)
            .ThenBy(item => item.RecordKey);

    internal static string BuildAmsCommodityStageKey(
        AmsLatestObservation observation)
        => string.Join(
            '\u001f',
            observation.Commodity,
            observation.MarketStageCode);

    internal static string BuildAmsPointIdentity(
        AmsLatestObservation observation,
        bool includeMarketStage)
        => string.Join(
            '\u001f',
            includeMarketStage
                ? observation.MarketStageCode
                : string.Empty,
            observation.Commodity,
            observation.OriginalUnit,
            observation.Variety,
            observation.Grade,
            observation.Organic,
            observation.Origin,
            observation.MarketLocationName);
}
