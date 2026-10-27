using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.AgriculturalFisheries;
using Ssalddel.Domain.AgriculturalFisheries;
using Ssalddel.Infrastructure.Persistence.AgriculturalFisheries;
using Ssalddel.Services.AgriculturalFisheries.Information;

namespace Ssalddel.Startup;

/// <summary>저장된 농수산물 품목코드와 연도별 관측 범위의 일회성 읽기 보고를 담당합니다.</summary>
internal static class 농수산물품목코드InspectCommandLine
{
    internal const string Command = "--inspect-agricultural-fisheries-product-codes";

    public static async Task<bool> TryRunAsync(IReadOnlyList<string> arguments, IServiceProvider services)
    {
        if (!arguments.Any(argument => string.Equals(argument, Command, StringComparison.OrdinalIgnoreCase)))
            return false;

        var inspectionYear = ParseInspectionYear(arguments);
        var inspectionStartDate = new DateOnly(inspectionYear, 1, 1);
        var inspectionEndDateExclusive = inspectionStartDate.AddYears(1);
        await using var scope = services.CreateAsyncScope();
        var archiveDb = scope.ServiceProvider.GetRequiredService<AgriculturalFisheriesDbContext>();
        var latestKamisSurveyDate = await archiveDb.KamisPriceObservations
            .AsNoTracking()
            .MaxAsync(item => (DateOnly?)item.SurveyDate);
        var kamisItemCodes = await archiveDb.KamisPriceObservations
            .AsNoTracking()
            .Where(item => item.ItemCode != string.Empty)
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
                LatestSurveyDate = group.Max(item => item.SurveyDate),
                KindCodeCount = group.Select(item => item.KindCode)
                    .Where(code => code != string.Empty)
                    .Distinct()
                    .Count()
            })
            .OrderBy(item => item.CategoryCode)
            .ThenBy(item => item.ItemCode)
            .ToListAsync();
        var focusNames = new[] { "사과", "감자", "양파", "쌀", "콩", "대두", "고등어" };
        var focusProducts = kamisItemCodes
            .Where(item => focusNames.Contains(item.ItemName, StringComparer.Ordinal))
            .ToArray();
        var kamisCategories = kamisItemCodes
            .GroupBy(item => new { item.CategoryCode, item.CategoryName })
            .Select(group => new
            {
                group.Key.CategoryCode,
                group.Key.CategoryName,
                ItemCodeCount = group.Count()
            })
            .OrderBy(item => item.CategoryCode)
            .ToArray();
        var kamisConflictingItemCodes = kamisItemCodes
            .GroupBy(item => item.ItemCode)
            .Where(group => group.Select(item => item.ItemName).Distinct().Count() > 1)
            .Select(group => group.Key)
            .ToList();
        var kamisCodeNameConflictDetails = await archiveDb.KamisPriceObservations
            .AsNoTracking()
            .Where(item => kamisConflictingItemCodes.Contains(item.ItemCode))
            .GroupBy(item => new
            {
                item.ItemCode,
                item.ItemName,
                item.KindCode,
                item.KindName,
                item.FrequencyCode,
                item.FirstCollectionRunId
            })
            .Select(group => new
            {
                group.Key.ItemCode,
                group.Key.ItemName,
                group.Key.KindCode,
                group.Key.KindName,
                group.Key.FrequencyCode,
                group.Key.FirstCollectionRunId,
                ObservationCount = group.Count(),
                FirstSurveyDate = group.Min(item => item.SurveyDate),
                LatestSurveyDate = group.Max(item => item.SurveyDate),
                LatestSeenAtUtc = group.Max(item => item.LastSeenAtUtc)
            })
            .OrderBy(item => item.ItemCode)
            .ThenBy(item => item.ItemName)
            .ThenBy(item => item.KindCode)
            .ToListAsync();
        var kamisCodeNameConflictSampleCandidates = await archiveDb.KamisPriceObservations
            .AsNoTracking()
            .Where(item => kamisConflictingItemCodes.Contains(item.ItemCode))
            .OrderBy(item => item.ItemCode)
            .ThenBy(item => item.ItemName)
            .ThenBy(item => item.SurveyDate)
            .Select(item => new
            {
                item.Id,
                item.FirstCollectionRunId,
                item.ItemCode,
                item.ItemName,
                item.KindCode,
                item.KindName,
                item.SurveyDate,
                item.RawJson
            })
            .ToListAsync();
        var kamisCodeNameConflictSamples = kamisCodeNameConflictSampleCandidates
            .GroupBy(item => new { item.ItemCode, item.ItemName })
            .SelectMany(group => group.Take(3))
            .ToArray();
        var kamisYearQuery = archiveDb.KamisPriceObservations
            .AsNoTracking()
            .Where(item => item.SurveyDate >= inspectionStartDate
                           && item.SurveyDate < inspectionEndDateExclusive);
        var kamisYearObservationCount = await kamisYearQuery.CountAsync();
        var kamisYearPricedObservationCount = await kamisYearQuery
            .CountAsync(item => item.PriceKrw.HasValue);
        var kamisYearSurveyDateCount = await kamisYearQuery
            .Select(item => item.SurveyDate)
            .Distinct()
            .CountAsync();
        var kamisYearFrequencyBreakdown = await kamisYearQuery
            .GroupBy(item => item.FrequencyCode)
            .Select(group => new
            {
                FrequencyCode = group.Key,
                ObservationCount = group.Count()
            })
            .OrderBy(item => item.FrequencyCode)
            .ToListAsync();
        var kamisYearCoverage = await kamisYearQuery
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
                ObservationCount = group.Count(),
                PricedObservationCount = group.Count(item => item.PriceKrw.HasValue),
                MissingPriceObservationCount = group.Count(item => !item.PriceKrw.HasValue),
                FirstSurveyDate = group.Min(item => item.SurveyDate),
                LatestSurveyDate = group.Max(item => item.SurveyDate),
                SurveyDateCount = group.Select(item => item.SurveyDate).Distinct().Count(),
                KindCodeCount = group.Select(item => item.KindCode).Distinct().Count(),
                RetailObservationCount = group.Count(item => item.ProductClassCode == "01"),
                WholesaleObservationCount = group.Count(item => item.ProductClassCode == "02")
            })
            .OrderBy(item => item.CategoryCode)
            .ThenBy(item => item.ItemCode)
            .ToListAsync();
        var kamisYearCoverageKeys = kamisYearCoverage
            .Select(item => (item.CategoryCode, item.ItemCode))
            .ToHashSet();
        var kamisCodesWithoutYearObservation = kamisItemCodes
            .Where(item => !kamisYearCoverageKeys.Contains((item.CategoryCode, item.ItemCode)))
            .Select(item => new
            {
                item.CategoryCode,
                item.CategoryName,
                item.ItemCode,
                item.ItemName
            })
            .ToArray();

        var latestAuctionSettlementDate = await archiveDb.DomesticAuctionPriceObservations
            .AsNoTracking()
            .MaxAsync(item => (DateOnly?)item.SettlementDate);
        var auctionObservationCount = await archiveDb.DomesticAuctionPriceObservations
            .AsNoTracking()
            .CountAsync();
        var auctionCorporationItemCodeCount = await archiveDb.DomesticAuctionPriceObservations
            .AsNoTracking()
            .Where(item => item.CorporationItemCode != string.Empty)
            .Select(item => new { item.CorporationCode, item.CorporationItemCode })
            .Distinct()
            .CountAsync();
        var auctionItemNameCount = await archiveDb.DomesticAuctionPriceObservations
            .AsNoTracking()
            .Where(item => item.ItemName != string.Empty)
            .Select(item => item.ItemName)
            .Distinct()
            .CountAsync();

        var hsUsdaMappings = await archiveDb.HsCommodityMappings
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.HsCode6)
            .Select(item => new
            {
                item.HsCode6,
                item.ProductNameKo,
                item.UsdaCommodityDesc,
                item.MatchQualityCode,
                item.ReviewStatusCode
            })
            .ToListAsync();
        var usdaYearQuery = archiveDb.PriceObservations
            .AsNoTracking()
            .Where(item => item.Year == inspectionYear);
        var usdaYearObservationCount = await usdaYearQuery.CountAsync();
        var usdaYearNumericObservationCount = await usdaYearQuery
            .CountAsync(item => item.NumericValue.HasValue);
        var usdaYearSuppressedObservationCount = await usdaYearQuery
            .CountAsync(item => item.IsSuppressed);
        var usdaYearCommodityCoverage = await usdaYearQuery
            .GroupBy(item => item.CommodityDesc)
            .Select(group => new
            {
                CommodityDesc = group.Key,
                ObservationCount = group.Count(),
                NumericObservationCount = group.Count(item => item.NumericValue.HasValue),
                SuppressedObservationCount = group.Count(item => item.IsSuppressed),
                UnitCount = group.Select(item => item.UnitDesc).Distinct().Count(),
                ReferencePeriodCount = group.Select(item => item.ReferencePeriodDesc).Distinct().Count(),
                LatestSourceLoadTimeUtc = group.Max(item => item.SourceLoadTimeUtc)
            })
            .OrderBy(item => item.CommodityDesc)
            .ToListAsync();
        var usdaYearReferencePeriods = await usdaYearQuery
            .GroupBy(item => new
            {
                item.CommodityDesc,
                item.ReferencePeriodDesc
            })
            .Select(group => new
            {
                group.Key.CommodityDesc,
                group.Key.ReferencePeriodDesc,
                ObservationCount = group.Count(),
                NumericObservationCount = group.Count(item => item.NumericValue.HasValue),
                SuppressedObservationCount = group.Count(item => item.IsSuppressed)
            })
            .OrderBy(item => item.CommodityDesc)
            .ThenBy(item => item.ReferencePeriodDesc)
            .ToListAsync();
        var mappedUsdaCommodityNames = hsUsdaMappings
            .Select(item => item.UsdaCommodityDesc)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var mappedUsdaCommodityCoverage = usdaYearCommodityCoverage
            .Where(item => mappedUsdaCommodityNames.Contains(item.CommodityDesc))
            .ToArray();
        var usdaYearCoverageByCommodity = usdaYearCommodityCoverage
            .ToDictionary(item => item.CommodityDesc, StringComparer.OrdinalIgnoreCase);
        var kamisToUsdaCommodityAliases =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["쌀"] = ["RICE"],
                ["콩"] = ["SOYBEANS"],
                ["감자"] = ["POTATOES"],
                ["양파"] = ["ONIONS"],
                ["토마토"] = ["TOMATOES"],
                ["딸기"] = ["STRAWBERRIES"],
                ["땅콩"] = ["PEANUTS"],
                ["브로콜리"] = ["BROCCOLI"],
                ["오이"] = ["CUCUMBERS"],
                ["상추"] = ["LETTUCE"],
                ["멜론"] = ["MELONS"],
                ["사과"] = ["APPLES"],
                ["배"] = ["PEARS"],
                ["복숭아"] = ["PEACHES"],
                ["포도"] = ["GRAPES"],
                ["오렌지"] = ["ORANGES"],
                ["레몬"] = ["LEMONS"],
                ["소"] = ["CATTLE"],
                ["돼지"] = ["HOGS"],
                ["닭"] = ["CHICKENS"],
                ["계란"] = ["EGGS"],
                ["우유"] = ["MILK"]
            };
        var kamisUsdaCrosswalk = kamisItemCodes
            .Select(kamisItem =>
            {
                var commodityAliases = kamisToUsdaCommodityAliases.TryGetValue(
                        kamisItem.ItemName,
                        out var aliases)
                    ? aliases.ToHashSet(StringComparer.OrdinalIgnoreCase)
                    : [];

                var candidateMappings = hsUsdaMappings
                    .Where(mapping =>
                        string.Equals(
                            mapping.ProductNameKo,
                            kamisItem.ItemName,
                            StringComparison.Ordinal)
                        || commodityAliases.Contains(mapping.UsdaCommodityDesc))
                    .Select(mapping =>
                    {
                        usdaYearCoverageByCommodity.TryGetValue(
                            mapping.UsdaCommodityDesc,
                            out var commodityCoverage);
                        return new
                        {
                            mapping.HsCode6,
                            mapping.ProductNameKo,
                            mapping.UsdaCommodityDesc,
                            mapping.MatchQualityCode,
                            mapping.ReviewStatusCode,
                            UsdaYearObservationCount =
                                commodityCoverage?.ObservationCount ?? 0,
                            UsdaYearNumericObservationCount =
                                commodityCoverage?.NumericObservationCount ?? 0,
                            ReferencePeriods = usdaYearReferencePeriods
                                .Where(period => string.Equals(
                                    period.CommodityDesc,
                                    mapping.UsdaCommodityDesc,
                                    StringComparison.OrdinalIgnoreCase))
                                .ToArray()
                        };
                    })
                    .ToArray();
                var usdaCommodityCandidates = commodityAliases
                    .OrderBy(commodityDesc => commodityDesc)
                    .Select(commodityDesc =>
                    {
                        usdaYearCoverageByCommodity.TryGetValue(
                            commodityDesc,
                            out var commodityCoverage);
                        return new
                        {
                            CommodityDesc = commodityDesc,
                            UsdaYearObservationCount =
                                commodityCoverage?.ObservationCount ?? 0,
                            UsdaYearNumericObservationCount =
                                commodityCoverage?.NumericObservationCount ?? 0,
                            UsdaYearSuppressedObservationCount =
                                commodityCoverage?.SuppressedObservationCount ?? 0,
                            ReferencePeriods = usdaYearReferencePeriods
                                .Where(period => string.Equals(
                                    period.CommodityDesc,
                                    commodityDesc,
                                    StringComparison.OrdinalIgnoreCase))
                                .ToArray()
                        };
                    })
                    .ToArray();

                return new
                {
                    kamisItem.CategoryCode,
                    kamisItem.CategoryName,
                    kamisItem.ItemCode,
                    kamisItem.ItemName,
                    CandidateMappingCount = candidateMappings.Length,
                    UsdaCommodityCandidateCount = usdaCommodityCandidates.Length,
                    HasUsdaYearObservation = usdaCommodityCandidates.Any(candidate =>
                        candidate.UsdaYearObservationCount > 0),
                    UsdaCommodityCandidates = usdaCommodityCandidates,
                    CandidateMappings = candidateMappings
                };
            })
            .ToArray();

        var blsArchiveService =
            scope.ServiceProvider.GetRequiredService<IBls평균소매가격ArchiveService>();
        var blsKamisComparison = blsArchiveService.GetKamisComparisonCatalog();
        var blsYearQuery = archiveDb.BlsAverageRetailPriceObservations
            .AsNoTracking()
            .Where(item =>
                item.ReferenceMonth >= inspectionStartDate
                && item.ReferenceMonth < inspectionEndDateExclusive);
        var blsYearObservationCount = await blsYearQuery.CountAsync();
        var blsYearNumericObservationCount =
            await blsYearQuery.CountAsync(item => item.PriceUsd.HasValue);
        var blsYearReferenceMonthCount = await blsYearQuery
            .Select(item => item.ReferenceMonth)
            .Distinct()
            .CountAsync();
        var blsYearLatestReferenceMonth = await blsYearQuery
            .MaxAsync(item => (DateOnly?)item.ReferenceMonth);
        var blsYearCoverage = await blsYearQuery
            .GroupBy(item => new
            {
                item.SeriesId,
                item.ItemCode,
                item.CanonicalProductKey,
                item.ProductNameKo,
                item.OriginalUnit
            })
            .Select(group => new
            {
                group.Key.SeriesId,
                group.Key.ItemCode,
                group.Key.CanonicalProductKey,
                group.Key.ProductNameKo,
                group.Key.OriginalUnit,
                ObservationCount = group.Count(),
                NumericObservationCount = group.Count(item => item.PriceUsd.HasValue),
                MissingObservationCount = group.Count(item => !item.PriceUsd.HasValue),
                FirstReferenceMonth = group.Min(item => item.ReferenceMonth),
                LatestReferenceMonth = group.Max(item => item.ReferenceMonth)
            })
            .OrderBy(item => item.SeriesId)
            .ToListAsync();
        var blsYearSeriesIds = blsYearCoverage
            .Select(item => item.SeriesId)
            .ToHashSet(StringComparer.Ordinal);
        var blsSeriesWithoutYearObservation = blsKamisComparison.Items
            .Where(item => !blsYearSeriesIds.Contains(item.SeriesId))
            .Select(item => new
            {
                item.SeriesId,
                item.BlsItemCode,
                item.CanonicalProductKey,
                item.BlsProductNameKo,
                item.MappingStatusCode
            })
            .ToArray();
        var blsKamisCandidateRows = blsKamisComparison.Items
            .SelectMany(series => series.KamisCandidates.Select(candidate => new
            {
                series.SeriesId,
                series.BlsItemCode,
                series.CanonicalProductKey,
                series.BlsProductNameKo,
                series.BlsOriginalUnit,
                candidate.KamisCategoryCode,
                candidate.KamisItemCode,
                candidate.MatchQualityCode,
                candidate.ReviewStatusCode,
                candidate.AllowsDirectPriceComparison,
                candidate.ReviewNote
            }))
            .ToArray();
        var kamisBlsCrosswalk = kamisItemCodes
            .Select(kamisItem => new
            {
                kamisItem.CategoryCode,
                kamisItem.CategoryName,
                kamisItem.ItemCode,
                kamisItem.ItemName,
                BlsCandidates = blsKamisCandidateRows
                    .Where(candidate =>
                        candidate.KamisCategoryCode == kamisItem.CategoryCode
                        && candidate.KamisItemCode == kamisItem.ItemCode)
                    .OrderBy(candidate => candidate.SeriesId)
                    .ToArray()
            })
            .Select(item => new
            {
                item.CategoryCode,
                item.CategoryName,
                item.ItemCode,
                item.ItemName,
                CandidateSeriesCount = item.BlsCandidates.Length,
                HasDirectComparableCandidate = item.BlsCandidates.Any(candidate =>
                    candidate.AllowsDirectPriceComparison),
                item.BlsCandidates
            })
            .ToArray();

        Console.WriteLine(JsonSerializer.Serialize(
            new
            {
                Kamis = new
                {
                    LatestSurveyDate = latestKamisSurveyDate,
                    ItemCodeCount = kamisItemCodes.Count,
                    Categories = kamisCategories,
                    WeeklyComparisonProducts = focusProducts,
                    AllItemCodes = kamisItemCodes,
                    CodeNameIntegrity = new
                    {
                        ConflictingItemCodeCount = kamisConflictingItemCodes.Count,
                        ConflictingItemCodes = kamisConflictingItemCodes,
                        ConflictDetails = kamisCodeNameConflictDetails,
                        ConflictSamples = kamisCodeNameConflictSamples
                    },
                    YearCoverage = new
                    {
                        Year = inspectionYear,
                        ObservationCount = kamisYearObservationCount,
                        PricedObservationCount = kamisYearPricedObservationCount,
                        MissingPriceObservationCount =
                            kamisYearObservationCount - kamisYearPricedObservationCount,
                        SurveyDateCount = kamisYearSurveyDateCount,
                        ItemCodeWithObservationCount = kamisYearCoverage.Count,
                        ItemCodeWithoutObservationCount = kamisCodesWithoutYearObservation.Length,
                        FrequencyBreakdown = kamisYearFrequencyBreakdown,
                        ItemCoverage = kamisYearCoverage,
                        ItemCodesWithoutObservation = kamisCodesWithoutYearObservation,
                        UnitBoundary =
                            "Period 관측은 KAMIS p_convert_kg_yn=Y에 따른 1kg 환산값이며 원 거래단위 관측과 FrequencyCode·Unit으로 구분합니다."
                    }
                },
                DomesticAuction = new
                {
                    LatestSettlementDate = latestAuctionSettlementDate,
                    ObservationCount = auctionObservationCount,
                    CorporationItemCodeCount = auctionCorporationItemCodeCount,
                    ItemNameCount = auctionItemNameCount,
                    CodeBoundary =
                        "법인품목코드는 도매시장 법인별 코드이며 KAMIS·HS의 전국 공통 품목코드가 아닙니다."
                },
                HsUsda = new
                {
                    MappingCount = hsUsdaMappings.Count,
                    ReviewedCount = hsUsdaMappings.Count(item =>
                        item.ReviewStatusCode == HsUsdaMappingReviewStatusCodes.Reviewed),
                    Mappings = hsUsdaMappings,
                    YearCoverage = new
                    {
                        Year = inspectionYear,
                        ObservationCount = usdaYearObservationCount,
                        NumericObservationCount = usdaYearNumericObservationCount,
                        SuppressedObservationCount = usdaYearSuppressedObservationCount,
                        CommodityCount = usdaYearCommodityCoverage.Count,
                        CommodityCoverage = usdaYearCommodityCoverage,
                        ReferencePeriodCoverage = usdaYearReferencePeriods,
                        CandidateMappedCommodityCoverage = mappedUsdaCommodityCoverage,
                        KamisItemCodeCrosswalk = new
                        {
                            KamisItemCodeCount = kamisUsdaCrosswalk.Length,
                            CandidateHsMappedKamisItemCodeCount = kamisUsdaCrosswalk.Count(item =>
                                item.CandidateMappingCount > 0),
                            DirectCommodityMappedKamisItemCodeCount = kamisUsdaCrosswalk.Count(item =>
                                item.UsdaCommodityCandidateCount > 0),
                            KamisItemCodeWithUsdaObservationCount = kamisUsdaCrosswalk.Count(item =>
                                item.HasUsdaYearObservation),
                            UnmappedKamisItemCodeCount = kamisUsdaCrosswalk.Count(item =>
                                item.UsdaCommodityCandidateCount == 0),
                            Items = kamisUsdaCrosswalk
                        },
                        MappingBoundary =
                            "HS-USDA 매핑은 검토 후보이며 KAMIS 품목코드와 동일한 코드 체계가 아닙니다. KAMIS는 도·소매 관측이고 USDA PRICE RECEIVED는 생산자 수취가격이므로 가격 수준을 직접 비교하지 않습니다."
                    }
                },
                BlsAverageRetail = new
                {
                    blsKamisComparison.BlsCatalogObservedAt,
                    blsKamisComparison.BlsSeriesCount,
                    blsKamisComparison.SeriesWithCandidateCount,
                    blsKamisComparison.DirectComparableCandidateSeriesCount,
                    blsKamisComparison.UniqueKamisItemCodeCount,
                    Crosswalk = blsKamisComparison.Items,
                    blsKamisComparison.ComparisonBoundaries,
                    KamisItemCodeCoverage = new
                    {
                        KamisItemCodeCount = kamisBlsCrosswalk.Length,
                        KamisItemCodeWithBlsCandidateCount = kamisBlsCrosswalk.Count(item =>
                            item.CandidateSeriesCount > 0),
                        KamisItemCodeWithoutBlsCandidateCount = kamisBlsCrosswalk.Count(item =>
                            item.CandidateSeriesCount == 0),
                        Items = kamisBlsCrosswalk
                    },
                    YearCoverage = new
                    {
                        Year = inspectionYear,
                        ObservationCount = blsYearObservationCount,
                        NumericObservationCount = blsYearNumericObservationCount,
                        ReferenceMonthCount = blsYearReferenceMonthCount,
                        LatestReferenceMonth = blsYearLatestReferenceMonth,
                        SeriesWithObservationCount = blsYearCoverage.Count,
                        SeriesWithoutObservationCount =
                            blsSeriesWithoutYearObservation.Length,
                        Items = blsYearCoverage,
                        SeriesWithoutObservation = blsSeriesWithoutYearObservation,
                        CollectionBoundary =
                            "2026년은 진행 중이므로 현재 BLS 공개 월까지만 수집하며 이후 월은 같은 RecordKey 규칙으로 누적합니다."
                    }
                }
            },
            new JsonSerializerOptions { WriteIndented = true }));
        return true;
    }

    internal static int ParseInspectionYear(IReadOnlyList<string> arguments)
    {
        var yearArgument = arguments.FirstOrDefault(argument =>
            argument.StartsWith("--year=", StringComparison.OrdinalIgnoreCase));
        var inspectionYear = int.TryParse(
            yearArgument?["--year=".Length..],
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var parsedInspectionYear)
            ? parsedInspectionYear
            : DateTime.Now.Year;
        if (inspectionYear is < 1990 or > 2100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(inspectionYear),
                "조사 연도는 1990년부터 2100년 사이여야 합니다.");
        }
        return inspectionYear;
    }
}
