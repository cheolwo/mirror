using static Ssalddel.Services.AgriculturalFisheries.Information.Kamis중심UsdaAms가격Projector;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.AgriculturalFisheries;
using Ssalddel.Domain.AgriculturalFisheries;
using Ssalddel.Infrastructure.Persistence.AgriculturalFisheries;
using 살뜰.Services.External.PublicData;

namespace Ssalddel.Services.AgriculturalFisheries.Information;

public interface IKamis중심UsdaAms가격비교QueryService
{
    Task<Kamis중심UsdaAms가격비교응답> GetAsync(
        Kamis중심UsdaAms가격비교Query query,
        CancellationToken cancellationToken = default);
}

public sealed class Kamis중심UsdaAms가격비교QueryService(
    AgriculturalFisheriesDbContext db,
    TimeProvider timeProvider,
    IFoodPriceCrosswalkCatalog crosswalkCatalog) : IKamis중심UsdaAms가격비교QueryService
{
    public async Task<Kamis중심UsdaAms가격비교응답> GetAsync(
        Kamis중심UsdaAms가격비교Query query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var now = timeProvider.GetUtcNow();
        var year = query.Year <= 0 ? now.Year : query.Year;
        if (year is < 1990 or > 2100 || year > now.Year)
        {
            throw new ArgumentException(
                "비교 연도는 1990년부터 현재 연도 사이여야 합니다.",
                nameof(query));
        }

        var frequencyCode = string.IsNullOrWhiteSpace(query.FrequencyCode)
            ? null
            : query.FrequencyCode.Trim();
        if (frequencyCode is not null
            && frequencyCode is not ("Daily" or "Monthly"))
        {
            throw new ArgumentException(
                "KAMIS 빈도는 Daily 또는 Monthly만 지원합니다.",
                nameof(query));
        }

        var yearFrom = new DateOnly(year, 1, 1);
        var yearToExclusive = yearFrom.AddYears(1);
        var anchorRows = await db.KamisPriceObservations
            .AsNoTracking()
            .Where(item =>
                item.SurveyDate >= yearFrom
                && item.SurveyDate < yearToExclusive
                && item.ItemCode != string.Empty)
            .GroupBy(item => new
            {
                item.CategoryCode,
                item.CategoryName,
                item.ItemCode,
                item.ItemName
            })
            .Select(group => new
            {
                group.Key.CategoryCode,
                group.Key.CategoryName,
                group.Key.ItemCode,
                group.Key.ItemName,
                LatestSurveyDate = group.Max(item => item.SurveyDate)
            })
            .ToArrayAsync(cancellationToken);
        var allAnchors = anchorRows
            .Select(item => new KamisAnchor(
                item.CategoryCode,
                item.CategoryName,
                item.ItemCode,
                item.ItemName,
                item.LatestSurveyDate))
            .OrderBy(item => item.CategoryCode, StringComparer.Ordinal)
            .ThenBy(item => item.ItemCode, StringComparer.Ordinal)
            .ToArray();

        var availableAmsCommodities = await db.UsdaAmsYearCommodityCatalog
            .AsNoTracking()
            .Where(item => item.Year == year)
            .Select(item => item.Commodity)
            .ToArrayAsync(cancellationToken);
        if (availableAmsCommodities.Length == 0)
        {
            availableAmsCommodities = await db.UsdaAmsMarketPriceObservations
                .AsNoTracking()
                .Where(item =>
                    item.ReportBeginDate >= yearFrom
                    && item.ReportBeginDate < yearToExclusive
                    && item.Commodity != string.Empty
                    && item.Commodity != "N/A")
                .Select(item => item.Commodity)
                .Distinct()
                .ToArrayAsync(cancellationToken);
        }
        var prepared = allAnchors
            .Select(anchor => new PreparedAnchor(
                anchor,
                Kamis중심UsdaAms품목MappingCatalog.Resolve(
                    anchor.ItemCode,
                    availableAmsCommodities)))
            .Where(item => MatchesQuery(item, query))
            .ToArray();
        var mappedCount = prepared.Count(item =>
            item.Mapping.MatchedCommodities.Count > 0);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var selected = prepared
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArray();
        var selectedItemCodes = selected
            .Select(item => item.Anchor.ItemCode)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var kamisPointLimit = Math.Clamp(query.KamisPointsPerItem, 1, 50);
        var kamisObservations = await LoadLatestKamisObservationsAsync(
            selectedItemCodes,
            frequencyCode,
            yearFrom,
            yearToExclusive,
            kamisPointLimit,
            cancellationToken);
        var kamisByItemCode = kamisObservations
            .GroupBy(item => item.ItemCode, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<KamisLatestObservation>)group.ToArray(),
                StringComparer.Ordinal);
        var selectedAmsCommodities = selected
            .SelectMany(item => item.Mapping.MatchedCommodities)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var amsPointLimit = Math.Clamp(query.AmsPointsPerStage, 1, 20);
        var latestAmsObservations = await LoadLatestAmsObservationsAsync(
            selectedAmsCommodities,
            yearFrom,
            yearToExclusive,
            amsPointLimit,
            cancellationToken);

        var items = new List<Kamis중심UsdaAms품목가격응답>(selected.Length);
        foreach (var item in selected)
        {
            kamisByItemCode.TryGetValue(
                item.Anchor.ItemCode,
                out var itemKamisObservations);
            var kamisPoints = BuildKamisPoints(
                itemKamisObservations ?? [],
                kamisPointLimit);
            var amsStages = BuildAmsStages(
                item.Mapping.MatchedCommodities,
                latestAmsObservations,
                amsPointLimit,
                cancellationToken);
            var productCodeConnection = BuildProductCodeConnection(
                item,
                crosswalkCatalog.GetAll());
            var distributionStagePriceBands = BuildDistributionStagePriceBands(
                kamisPoints,
                amsStages);
            items.Add(new Kamis중심UsdaAms품목가격응답(
                item.Anchor.CategoryCode,
                item.Anchor.CategoryName,
                item.Anchor.ItemCode,
                item.Anchor.ItemName,
                item.Anchor.LatestSurveyDate,
                item.Mapping.MappingStatusCode,
                item.Mapping.MatchQualityCode,
                item.Mapping.MatchQualityLabel,
                item.Mapping.MatchedCommodities,
                item.Mapping.ReviewNote,
                AllowsDirectPriceDifference: false,
                kamisPoints,
                amsStages,
                productCodeConnection,
                distributionStagePriceBands));
        }

        return new Kamis중심UsdaAms가격비교응답(
            items.Count == 0
                ? Kamis중심UsdaAms가격비교상태Codes.자료없음
                : Kamis중심UsdaAms가격비교상태Codes.완료,
            now.UtcDateTime,
            year,
            allAnchors.Length,
            prepared.Length,
            mappedCount,
            prepared.Length - mappedCount,
            page,
            pageSize,
            items,
            [
                "KAMIS 품목코드와 품목명을 기준축으로 정렬하고 USDA AMS 품목명은 검토 가능한 후보로 연결합니다.",
                "KAMIS 가격은 저장된 원화·거래단위를 그대로 표시하고 USDA AMS 가격은 USD·원 포장단위를 보존합니다.",
                "산지 출하·도매 터미널·소매 광고 가격은 서로 다른 시장 단계이므로 단계별로 분리합니다.",
                "환율·중량·포장·품종·등급·원산지·관측일이 일치하기 전에는 가격 차액이나 우열을 계산하지 않습니다.",
                "국제 HS 후보와 한국 HSK·미국 HTSUS 국가 세번을 분리하며, 국가 세번은 전문가 검토 전까지 확정 코드로 표시하지 않습니다.",
                "USDA AMS 전문청과 보고서에 없는 곡물·축산·수산 KAMIS 품목은 후보 없음으로 유지합니다."
            ]);
    }

    private async Task<KamisLatestObservation[]> LoadLatestKamisObservationsAsync(
        IReadOnlyList<string> itemCodes,
        string? frequencyCode,
        DateOnly yearFrom,
        DateOnly yearToExclusive,
        int takePerItem,
        CancellationToken cancellationToken)
    {
        if (itemCodes.Count == 0)
        {
            return [];
        }

        var itemCodeList = itemCodes.ToList();
        if (db.Database.IsRelational())
        {
            var itemCodePlaceholders = string.Join(
                ", ",
                Enumerable.Range(0, itemCodeList.Count)
                    .Select(index => $"{{{index}}}"));
            var yearFromParameterIndex = itemCodeList.Count;
            var yearToParameterIndex = itemCodeList.Count + 1;
            var takeParameterIndex = itemCodeList.Count + 2;
            var frequencyParameterIndex = itemCodeList.Count + 3;
            var frequencyPredicate = frequencyCode is null
                ? string.Empty
                : $"AND observation.FrequencyCode = {{{frequencyParameterIndex}}}";
            var sql = """
                      SELECT
                          ranked.Id,
                          ranked.ItemCode,
                          ranked.FrequencyCode,
                          ranked.ProductClassCode,
                          ranked.ProductClassName,
                          ranked.SurveyDate,
                          ranked.KindCode,
                          ranked.KindName,
                          ranked.RankCode,
                          ranked.RankName,
                          ranked.Unit,
                          ranked.SourcePackageLabel,
                          ranked.ComparisonUnit,
                          ranked.PriceNormalizationCode,
                          ranked.PriceNormalizationBasis,
                          ranked.PriceKrw,
                          ranked.IsPriceMissing
                      FROM (
                          SELECT
                              latest.*,
                              ROW_NUMBER() OVER (
                                  PARTITION BY latest.ItemCode
                                  ORDER BY
                                      CASE
                                          WHEN latest.ProductClassCode = '02' THEN 0
                                          ELSE 1
                                      END,
                                      latest.KindName,
                                      latest.RankCode,
                                      latest.SurveyDate DESC,
                                      latest.Id DESC
                              ) AS ItemRowNumber
                          FROM (
                              SELECT
                                  observation.Id,
                                  observation.ItemCode,
                                  observation.FrequencyCode,
                                  observation.ProductClassCode,
                                  observation.ProductClassName,
                                  observation.SurveyDate,
                                  observation.KindCode,
                                  observation.KindName,
                                  observation.RankCode,
                                  observation.RankName,
                                  observation.Unit,
                                  observation.SourcePackageLabel,
                                  observation.ComparisonUnit,
                                  observation.PriceNormalizationCode,
                                  observation.PriceNormalizationBasis,
                                  observation.PriceKrw,
                                  observation.IsPriceMissing,
                                  ROW_NUMBER() OVER (
                                      PARTITION BY
                                          observation.ItemCode,
                                          observation.FrequencyCode,
                                          observation.ProductClassCode,
                                          observation.KindCode,
                                          observation.RankCode,
                                          observation.Unit
                                      ORDER BY
                                          observation.SurveyDate DESC,
                                          observation.Id DESC
                                  ) AS LatestRowNumber
                              FROM agri_kamis_price_observations AS observation
                              WHERE observation.ItemCode IN (__ITEM_CODE_PARAMETERS__)
                                AND observation.SurveyDate >= __YEAR_FROM_PARAMETER__
                                AND observation.SurveyDate < __YEAR_TO_PARAMETER__
                                __FREQUENCY_PREDICATE__
                          ) AS latest
                          WHERE latest.LatestRowNumber = 1
                      ) AS ranked
                      WHERE ranked.ItemRowNumber <= __TAKE_PARAMETER__
                      """
                .Replace(
                    "__ITEM_CODE_PARAMETERS__",
                    itemCodePlaceholders,
                    StringComparison.Ordinal)
                .Replace(
                    "__YEAR_FROM_PARAMETER__",
                    $"{{{yearFromParameterIndex}}}",
                    StringComparison.Ordinal)
                .Replace(
                    "__YEAR_TO_PARAMETER__",
                    $"{{{yearToParameterIndex}}}",
                    StringComparison.Ordinal)
                .Replace(
                    "__TAKE_PARAMETER__",
                    $"{{{takeParameterIndex}}}",
                    StringComparison.Ordinal)
                .Replace(
                    "__FREQUENCY_PREDICATE__",
                    frequencyPredicate,
                    StringComparison.Ordinal);
            var parameters = itemCodeList
                .Cast<object>()
                .Append(yearFrom)
                .Append(yearToExclusive)
                .Append(takePerItem);
            if (frequencyCode is not null)
            {
                parameters = parameters.Append(frequencyCode);
            }

            return await db.Database.SqlQueryRaw<KamisLatestObservation>(
                    sql,
                    parameters.ToArray())
                .ToArrayAsync(cancellationToken);
        }

        var observations = await db.KamisPriceObservations
            .AsNoTracking()
            .Where(item =>
                itemCodeList.Contains(item.ItemCode)
                && item.SurveyDate >= yearFrom
                && item.SurveyDate < yearToExclusive
                && (frequencyCode == null || item.FrequencyCode == frequencyCode))
            .Select(item => new KamisLatestObservation
            {
                Id = item.Id,
                ItemCode = item.ItemCode,
                FrequencyCode = item.FrequencyCode,
                ProductClassCode = item.ProductClassCode,
                ProductClassName = item.ProductClassName,
                SurveyDate = item.SurveyDate,
                KindCode = item.KindCode,
                KindName = item.KindName,
                RankCode = item.RankCode,
                RankName = item.RankName,
                Unit = item.Unit,
                SourcePackageLabel = item.SourcePackageLabel,
                ComparisonUnit = item.ComparisonUnit,
                PriceNormalizationCode = item.PriceNormalizationCode,
                PriceNormalizationBasis = item.PriceNormalizationBasis,
                PriceKrw = item.PriceKrw,
                IsPriceMissing = item.IsPriceMissing
            })
            .ToArrayAsync(cancellationToken);
        return observations
            .GroupBy(item => new
            {
                item.ItemCode,
                item.FrequencyCode,
                item.ProductClassCode,
                item.KindCode,
                item.RankCode,
                item.Unit
            })
            .Select(group => group
                .OrderByDescending(item => item.SurveyDate)
                .ThenByDescending(item => item.Id)
                .First())
            .GroupBy(item => item.ItemCode, StringComparer.Ordinal)
            .SelectMany(group => OrderKamis(group)
                .Take(takePerItem))
            .ToArray();
    }

    private async Task<AmsLatestObservation[]> LoadLatestAmsObservationsAsync(
        IReadOnlyList<string> matchedCommodities,
        DateOnly yearFrom,
        DateOnly yearToExclusive,
        int takePerCommodityStage,
        CancellationToken cancellationToken)
    {
        var commodityNames = matchedCommodities.ToList();
        if (commodityNames.Count == 0)
        {
            return [];
        }

        if (db.Database.IsRelational())
        {
            var commodityPlaceholders = string.Join(
                ", ",
                Enumerable.Range(0, commodityNames.Count)
                    .Select(index => $"{{{index}}}"));
            var yearFromParameterIndex = commodityNames.Count;
            var yearToParameterIndex = commodityNames.Count + 1;
            var takeParameterIndex = commodityNames.Count + 2;
            var sql = """
                     SELECT
                         ranked.RecordKey,
                         ranked.SourceKey,
                         ranked.MarketStageCode,
                         ranked.ReportBeginDate,
                         ranked.Commodity,
                         ranked.Variety,
                         ranked.Grade,
                         ranked.Package,
                         ranked.ItemSize,
                         ranked.Organic,
                         ranked.Origin,
                         ranked.MarketLocationName,
                         ranked.MarketLocationState,
                         ranked.LowPrice,
                         ranked.HighPrice,
                         ranked.MostlyLowPrice,
                         ranked.MostlyHighPrice,
                         ranked.WeightedAveragePrice,
                         ranked.StoreCount,
                         ranked.CurrencyCode,
                         ranked.OriginalUnit
                     FROM (
                         SELECT
                             deduplicated.*,
                             ROW_NUMBER() OVER (
                                 PARTITION BY
                                     deduplicated.Commodity,
                                     deduplicated.MarketStageCode
                                 ORDER BY
                                     deduplicated.ReportBeginDate DESC,
                                     deduplicated.OriginalUnit,
                                     deduplicated.Variety,
                                     deduplicated.Grade,
                                     deduplicated.Origin,
                                     deduplicated.MarketLocationName,
                                     deduplicated.RecordKey
                             ) AS CommodityStageRowNumber
                         FROM (
                             SELECT
                                 current_observation.*,
                                 ROW_NUMBER() OVER (
                                     PARTITION BY
                                         current_observation.Commodity,
                                         current_observation.MarketStageCode,
                                         current_observation.OriginalUnit,
                                         current_observation.Variety,
                                         current_observation.Grade,
                                         current_observation.Organic,
                                         current_observation.Origin,
                                         current_observation.MarketLocationName
                                     ORDER BY
                                         current_observation.ReportBeginDate DESC,
                                         current_observation.RecordKey
                                 ) AS DisplayRowNumber
                             FROM (
                                 SELECT
                                     observation.RecordKey,
                                     observation.SourceKey,
                                     observation.MarketStageCode,
                                     observation.ReportBeginDate,
                                     observation.Commodity,
                                     observation.Variety,
                                     observation.Grade,
                                     observation.Package,
                                     observation.ItemSize,
                                     observation.Organic,
                                     observation.Origin,
                                     observation.MarketLocationName,
                                     observation.MarketLocationState,
                                     observation.LowPrice,
                                     observation.HighPrice,
                                     observation.MostlyLowPrice,
                                     observation.MostlyHighPrice,
                                     observation.WeightedAveragePrice,
                                     observation.StoreCount,
                                     observation.CurrencyCode,
                                     observation.OriginalUnit
                                 FROM agri_usda_ams_market_price_observations AS observation
                                 INNER JOIN (
                                     SELECT
                                         Commodity,
                                         MarketStageCode,
                                         MAX(ReportBeginDate) AS LatestReferenceDate
                                     FROM agri_usda_ams_market_price_observations
                                     WHERE Commodity IN (__COMMODITY_PARAMETERS__)
                                       AND ReportBeginDate >= __YEAR_FROM_PARAMETER__
                                       AND ReportBeginDate < __YEAR_TO_PARAMETER__
                                     GROUP BY Commodity, MarketStageCode
                                 ) AS latest
                                     ON latest.Commodity = observation.Commodity
                                     AND latest.MarketStageCode = observation.MarketStageCode
                                     AND latest.LatestReferenceDate = observation.ReportBeginDate
                                 WHERE observation.Commodity IN (__COMMODITY_PARAMETERS__)
                                   AND (
                                       observation.LowPrice IS NOT NULL
                                       OR observation.HighPrice IS NOT NULL
                                       OR observation.WeightedAveragePrice IS NOT NULL
                                   )
                             ) AS current_observation
                         ) AS deduplicated
                         WHERE deduplicated.DisplayRowNumber = 1
                     ) AS ranked
                     WHERE ranked.CommodityStageRowNumber <= __TAKE_PARAMETER__
                     """
                .Replace(
                    "__COMMODITY_PARAMETERS__",
                    commodityPlaceholders,
                    StringComparison.Ordinal)
                .Replace(
                    "__YEAR_FROM_PARAMETER__",
                    $"{{{yearFromParameterIndex}}}",
                    StringComparison.Ordinal)
                .Replace(
                    "__YEAR_TO_PARAMETER__",
                    $"{{{yearToParameterIndex}}}",
                    StringComparison.Ordinal)
                .Replace(
                    "__TAKE_PARAMETER__",
                    $"{{{takeParameterIndex}}}",
                    StringComparison.Ordinal);
            var parameters = commodityNames
                .Cast<object>()
                .Append(yearFrom)
                .Append(yearToExclusive)
                .Append(takePerCommodityStage)
                .ToArray();
            return await db.Database.SqlQueryRaw<AmsLatestObservation>(
                    sql,
                    parameters)
                .ToArrayAsync(cancellationToken);
        }

        var latestDates = db.UsdaAmsMarketPriceObservations
            .Where(item =>
                commodityNames.Contains(item.Commodity)
                && item.ReportBeginDate >= yearFrom
                && item.ReportBeginDate < yearToExclusive)
            .GroupBy(item => new
            {
                item.Commodity,
                item.MarketStageCode
            })
            .Select(group => new
            {
                group.Key.Commodity,
                group.Key.MarketStageCode,
                LatestReferenceDate = group.Max(item => item.ReportBeginDate)
            });
        var latestObservations = await (
                from observation in db.UsdaAmsMarketPriceObservations.AsNoTracking()
                join latest in latestDates
                    on new
                    {
                        observation.Commodity,
                        observation.MarketStageCode,
                        observation.ReportBeginDate
                    }
                    equals new
                    {
                        latest.Commodity,
                        latest.MarketStageCode,
                        ReportBeginDate = latest.LatestReferenceDate
                    }
                where observation.LowPrice.HasValue
                      || observation.HighPrice.HasValue
                      || observation.WeightedAveragePrice.HasValue
                select new AmsLatestObservation
                {
                    RecordKey = observation.RecordKey,
                    SourceKey = observation.SourceKey,
                    MarketStageCode = observation.MarketStageCode,
                    ReportBeginDate = observation.ReportBeginDate,
                    Commodity = observation.Commodity,
                    Variety = observation.Variety,
                    Grade = observation.Grade,
                    Package = observation.Package,
                    ItemSize = observation.ItemSize,
                    Organic = observation.Organic,
                    Origin = observation.Origin,
                    MarketLocationName = observation.MarketLocationName,
                    MarketLocationState = observation.MarketLocationState,
                    LowPrice = observation.LowPrice,
                    HighPrice = observation.HighPrice,
                    MostlyLowPrice = observation.MostlyLowPrice,
                    MostlyHighPrice = observation.MostlyHighPrice,
                    WeightedAveragePrice = observation.WeightedAveragePrice,
                    StoreCount = observation.StoreCount,
                    CurrencyCode = observation.CurrencyCode,
                    OriginalUnit = observation.OriginalUnit
                })
            .ToArrayAsync(cancellationToken);
        return OrderAms(latestObservations)
            .DistinctBy(item => BuildAmsPointIdentity(
                item,
                includeMarketStage: true))
            .GroupBy(
                BuildAmsCommodityStageKey,
                StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => group.Take(takePerCommodityStage))
            .ToArray();
    }

}
