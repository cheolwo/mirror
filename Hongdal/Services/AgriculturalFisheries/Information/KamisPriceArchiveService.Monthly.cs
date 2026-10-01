using System.Globalization;
using System.Text.Json;
using Hongdal.Domain.AgriculturalFisheries;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using static Hongdal.Services.AgriculturalFisheries.Information.KamisJsonReader;
using static Hongdal.Services.AgriculturalFisheries.Information.KamisPriceRequestRules;
using static Hongdal.Services.AgriculturalFisheries.Information.KamisPriceValueParser;

namespace Hongdal.Services.AgriculturalFisheries.Information;

public sealed partial class KamisPriceArchiveService : IKamisPriceArchiveService
{
    public async Task<KamisPriceArchiveResult> CollectMonthlyPricesAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        ValidatePeriod(startDate, endDate);
        EnsureKamisConfigured();

        var run = new KamisPriceCollectionRun
        {
            RequestedDate = endDate,
            QuerySummary =
                $"KAMIS 전국 월평균 가격 / 도매·소매 / 품목·품종·상품·중품 / {startDate:yyyy-MM-dd}~{endDate:yyyy-MM-dd} / kg 환산",
            SourceUrl = SourceUrl,
            StartedAtUtc = DateTime.UtcNow
        };
        _db.KamisCollectionRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);

        var fetchedCount = 0;
        var insertedCount = 0;
        var updatedCount = 0;
        var existingCount = 0;
        DateOnly? latestSurveyDate = null;

        try
        {
            var queries = await FetchMonthlyProductQueriesAsync(cancellationToken);
            using var concurrency = new SemaphoreSlim(MonthlyQueryConcurrency);
            var completedQueryCount = 0;

            foreach (var batch in queries.Chunk(MonthlyQueryBatchSize))
            {
                var tasks = batch.Select(async query =>
                {
                    await concurrency.WaitAsync(cancellationToken);
                    try
                    {
                        return await FetchMonthlyPricesAsync(
                            query,
                            startDate,
                            endDate,
                            DateTime.UtcNow,
                            cancellationToken);
                    }
                    finally
                    {
                        concurrency.Release();
                    }
                });

                var batchResults = await Task.WhenAll(tasks);
                var deduplicated = batchResults
                    .SelectMany(result => result)
                    .GroupBy(item => item.RecordKey, StringComparer.Ordinal)
                    .Select(group => group.Last())
                    .ToArray();
                var batchCounts = await UpsertArchiveBatchAsync(
                    run.Id,
                    deduplicated,
                    cancellationToken);

                fetchedCount += deduplicated.Length;
                insertedCount += batchCounts.Inserted;
                updatedCount += batchCounts.Updated;
                existingCount += batchCounts.Existing;
                var batchLatestDate = deduplicated
                    .Select(item => item.SurveyDate)
                    .Cast<DateOnly?>()
                    .DefaultIfEmpty()
                    .Max();
                if (batchLatestDate is not null
                    && (latestSurveyDate is null || batchLatestDate > latestSurveyDate))
                {
                    latestSurveyDate = batchLatestDate;
                }

                completedQueryCount += batch.Length;
                _logger.LogInformation(
                    "KAMIS 월평균 가격 수집 진행. RunId={RunId}, Queries={Completed}/{Total}, Fetched={Fetched}, Inserted={Inserted}",
                    run.Id,
                    completedQueryCount,
                    queries.Count,
                    fetchedCount,
                    insertedCount);
            }

            _db.ChangeTracker.Clear();
            var completedRun = await _db.KamisCollectionRuns
                .SingleAsync(item => item.Id == run.Id, cancellationToken);
            completedRun.StatusCode = KamisArchiveStatusCodes.Completed;
            completedRun.CompletedAtUtc = DateTime.UtcNow;
            completedRun.LatestSurveyDate = latestSurveyDate;
            completedRun.FetchedCount = fetchedCount;
            completedRun.InsertedCount = insertedCount;
            completedRun.UpdatedCount = updatedCount;
            completedRun.ExistingCount = existingCount;
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "KAMIS 국내 농수산물 월평균 가격 수집 완료. RunId={RunId}, Queries={Queries}, Fetched={Fetched}, Inserted={Inserted}, Updated={Updated}, Existing={Existing}",
                run.Id,
                queries.Count,
                fetchedCount,
                insertedCount,
                updatedCount,
                existingCount);

            return new KamisPriceArchiveResult(
                run.Id,
                fetchedCount,
                insertedCount,
                updatedCount,
                existingCount,
                latestSurveyDate);
        }
        catch (Exception ex)
        {
            _db.ChangeTracker.Clear();
            var failedRun = await _db.KamisCollectionRuns
                .SingleAsync(item => item.Id == run.Id, CancellationToken.None);
            failedRun.StatusCode = KamisArchiveStatusCodes.Failed;
            failedRun.CompletedAtUtc = DateTime.UtcNow;
            failedRun.LatestSurveyDate = latestSurveyDate;
            failedRun.FetchedCount = fetchedCount;
            failedRun.InsertedCount = insertedCount;
            failedRun.UpdatedCount = updatedCount;
            failedRun.ExistingCount = existingCount;
            failedRun.ErrorMessage = ex.Message.Length <= 2000 ? ex.Message : ex.Message[..2000];
            await _db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<IReadOnlyList<MonthlyProductQuery>> FetchMonthlyProductQueriesAsync(
        CancellationToken cancellationToken)
    {
        var kamis = _options.Kamis;
        var requestPath = QueryHelpers.AddQueryString(
            kamis.DailyCategoryPricePath.TrimStart('/'),
            new Dictionary<string, string?>
            {
                ["action"] = "productInfo",
                ["p_returntype"] = "json"
            });

        using var document = await _kamisClient.GetDocumentAsync(requestPath, cancellationToken);
        var root = document.RootElement;
        var resultCode = ReadString(root, "error_code");
        if (!string.Equals(resultCode, "000", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"KAMIS 품목 목록 요청이 거부되었습니다. 코드={resultCode}");
        }

        if (!TryGetProperty(root, "info", out var info)
            || info.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("KAMIS 품목 목록 응답의 info 항목이 올바르지 않습니다.");
        }

        var queries = new List<MonthlyProductQuery>();
        foreach (var product in info.EnumerateArray())
        {
            var rankCodes = new[]
                {
                    ReadString(product, "whole_productrankcode"),
                    ReadString(product, "retail_productrankcode")
                }
                .SelectMany(value => value.Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Where(rankCode => rankCode is "04" or "05")
                .Distinct(StringComparer.Ordinal);

            foreach (var rankCode in rankCodes)
            {
                queries.Add(new MonthlyProductQuery(
                    ReadString(product, "itemcategorycode"),
                    ReadString(product, "itemcategoryname"),
                    ReadString(product, "itemcode"),
                    ReadString(product, "itemname"),
                    ReadString(product, "kindcode"),
                    ReadString(product, "kindname"),
                    rankCode,
                    GetRankName(rankCode),
                    rankCode == "04" ? "1" : "2"));
            }
        }

        return queries
            .Where(query => query.CategoryCode.Length > 0
                            && query.ItemCode.Length > 0
                            && query.KindCode.Length > 0)
            .DistinctBy(query => string.Join(
                '\u001f',
                query.CategoryCode,
                query.ItemCode,
                query.KindCode,
                query.RankCode))
            .ToArray();
    }

    private async Task<IReadOnlyList<KamisPriceObservation>> FetchMonthlyPricesAsync(
        MonthlyProductQuery query,
        DateOnly startDate,
        DateOnly endDate,
        DateTime collectedAtUtc,
        CancellationToken cancellationToken)
    {
        var kamis = _options.Kamis;
        var periodYears = Math.Max(1, endDate.Year - startDate.Year + 1);
        var requestPath = QueryHelpers.AddQueryString(
            kamis.DailyCategoryPricePath.TrimStart('/'),
            new Dictionary<string, string?>
            {
                ["action"] = "monthlySalesList",
                ["p_yyyy"] = endDate.Year.ToString(CultureInfo.InvariantCulture),
                ["p_period"] = periodYears.ToString(CultureInfo.InvariantCulture),
                ["p_itemcategorycode"] = query.CategoryCode,
                ["p_itemcode"] = query.ItemCode,
                ["p_kindcode"] = query.KindCode,
                ["p_graderank"] = query.GradeRank,
                ["p_countycode"] = string.Empty,
                ["p_convert_kg_yn"] = "Y",
                ["p_cert_key"] = kamis.CertificationKey,
                ["p_cert_id"] = kamis.RequesterId,
                ["p_returntype"] = "json"
            });

        using var document = await _kamisClient.GetDocumentAsync(requestPath, cancellationToken);
        var root = document.RootElement;
        var resultCode = ReadString(root, "error_code");
        if (!string.Equals(resultCode, "000", StringComparison.Ordinal))
        {
            if (string.Equals(resultCode, "001", StringComparison.Ordinal))
            {
                return [];
            }

            throw new InvalidOperationException(
                $"KAMIS 월평균 가격 요청이 거부되었습니다. 부류={query.CategoryCode}, 품목={query.ItemCode}, 코드={resultCode}");
        }

        if (!TryGetProperty(root, "price", out var prices))
        {
            return [];
        }

        var priceGroups = prices.ValueKind switch
        {
            JsonValueKind.Array => prices.EnumerateArray().ToArray(),
            JsonValueKind.Object => [prices],
            _ => []
        };
        var observations = new List<KamisPriceObservation>();
        var startMonth = new DateOnly(startDate.Year, startDate.Month, 1);
        var endMonth = new DateOnly(endDate.Year, endDate.Month, 1);

        foreach (var priceGroup in priceGroups)
        {
            var productClassCode = ReadString(priceGroup, "productclscode");
            if (!ProductClasses.TryGetValue(productClassCode, out var productClassName)
                || !TryGetProperty(priceGroup, "item", out var yearlyItems))
            {
                continue;
            }

            var years = yearlyItems.ValueKind switch
            {
                JsonValueKind.Array => yearlyItems.EnumerateArray().ToArray(),
                JsonValueKind.Object => [yearlyItems],
                _ => []
            };
            foreach (var yearItem in years)
            {
                if (!int.TryParse(ReadString(yearItem, "yyyy"), out var year)
                    || year is < 1990 or > 2100)
                {
                    continue;
                }

                for (var month = 1; month <= 12; month++)
                {
                    var monthStart = new DateOnly(year, month, 1);
                    if (monthStart < startMonth || monthStart > endMonth)
                    {
                        continue;
                    }

                    observations.Add(MapMonthlyObservation(
                        yearItem,
                        query,
                        productClassCode,
                        productClassName,
                        year,
                        month,
                        endDate,
                        collectedAtUtc));
                }
            }
        }

        return observations;
    }

    private static KamisPriceObservation MapMonthlyObservation(
        JsonElement source,
        MonthlyProductQuery query,
        string productClassCode,
        string productClassName,
        int year,
        int month,
        DateOnly requestedDate,
        DateTime collectedAtUtc)
    {
        var surveyDate = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var priceRaw = ReadString(source, $"m{month}");
        var priceKrw = ParsePrice(priceRaw);
        var identity = string.Join(
            '\u001f',
            "Monthly",
            productClassCode,
            query.CategoryCode,
            NationwideCode,
            surveyDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            query.ItemCode,
            query.KindCode,
            query.RankCode,
            ConvertedKilogramUnit);

        return new KamisPriceObservation
        {
            RecordKey = UsdaNassPriceArchiveService.Sha256(identity),
            ProductClassCode = productClassCode,
            ProductClassName = productClassName,
            CategoryCode = query.CategoryCode,
            CategoryName = query.CategoryName,
            CountryCode = NationwideCode,
            CountryName = NationwideName,
            RequestedDate = requestedDate,
            SurveyDate = surveyDate,
            FrequencyCode = "Monthly",
            ItemName = query.ItemName,
            ItemCode = query.ItemCode,
            KindName = query.KindName,
            KindCode = query.KindCode,
            RankName = query.RankName,
            RankCode = query.RankCode,
            Unit = ConvertedKilogramUnit,
            PriceRaw = priceRaw,
            PriceKrw = priceKrw,
            IsPriceMissing = priceKrw is null,
            SourceUrl = SourceUrl,
            RawJson = source.GetRawText(),
            FirstCollectedAtUtc = collectedAtUtc,
            LastSeenAtUtc = collectedAtUtc,
            UpdatedAtUtc = collectedAtUtc
        };
    }

}
