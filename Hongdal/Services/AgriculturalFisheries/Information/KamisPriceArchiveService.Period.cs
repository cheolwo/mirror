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
    public async Task<KamisPriceArchiveResult> CollectPeriodPricesAsync(
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
                $"KAMIS 전국 기간 가격 / 도매·소매 / 품목·품종·등급별 / {startDate:yyyy-MM-dd}~{endDate:yyyy-MM-dd} / kg 환산",
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
            var queries = await FetchPeriodProductQueriesAsync(cancellationToken);
            using var concurrency = new SemaphoreSlim(PeriodQueryConcurrency);
            var completedQueryCount = 0;

            foreach (var batch in queries.Chunk(PeriodQueryBatchSize))
            {
                var tasks = batch.Select(async query =>
                {
                    await concurrency.WaitAsync(cancellationToken);
                    try
                    {
                        return await FetchPeriodPricesAsync(
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
                    "KAMIS 기간 가격 수집 진행. RunId={RunId}, Queries={Completed}/{Total}, Fetched={Fetched}, Inserted={Inserted}",
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
                "KAMIS 국내 농수산물 1년 가격 수집 완료. RunId={RunId}, Queries={Queries}, Fetched={Fetched}, Inserted={Inserted}, Updated={Updated}, Existing={Existing}",
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

    private async Task<IReadOnlyList<PeriodProductQuery>> FetchPeriodProductQueriesAsync(
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

        var queries = new List<PeriodProductQuery>();
        foreach (var product in info.EnumerateArray())
        {
            AddPeriodProductQueries(
                queries,
                product,
                "periodWholesaleProductList",
                "02",
                ProductClasses["02"],
                "whole_productrankcode");
            AddPeriodProductQueries(
                queries,
                product,
                "periodRetailProductList",
                "01",
                ProductClasses["01"],
                "retail_productrankcode");
        }

        return queries
            .DistinctBy(query => string.Join(
                '\u001f',
                query.Action,
                query.CategoryCode,
                query.ItemCode,
                query.KindCode,
                query.RankCode))
            .ToArray();
    }

    private static void AddPeriodProductQueries(
        ICollection<PeriodProductQuery> target,
        JsonElement product,
        string action,
        string productClassCode,
        string productClassName,
        string rankPropertyName)
    {
        var categoryCode = ReadString(product, "itemcategorycode");
        var itemCode = ReadString(product, "itemcode");
        var kindCode = ReadString(product, "kindcode");
        var rankCodes = ReadString(product, rankPropertyName)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (categoryCode.Length == 0
            || itemCode.Length == 0
            || kindCode.Length == 0
            || rankCodes.Length == 0)
        {
            return;
        }

        var categoryName = ReadString(product, "itemcategoryname");
        var itemName = ReadString(product, "itemname");
        var kindName = ReadString(product, "kindname");
        foreach (var rankCode in rankCodes)
        {
            target.Add(new PeriodProductQuery(
                action,
                productClassCode,
                productClassName,
                categoryCode,
                categoryName,
                itemCode,
                itemName,
                kindCode,
                kindName,
                rankCode,
                GetRankName(rankCode)));
        }
    }

    private async Task<IReadOnlyList<KamisPriceObservation>> FetchPeriodPricesAsync(
        PeriodProductQuery query,
        DateOnly startDate,
        DateOnly endDate,
        DateTime collectedAtUtc,
        CancellationToken cancellationToken)
    {
        var kamis = _options.Kamis;
        var requestPath = QueryHelpers.AddQueryString(
            kamis.DailyCategoryPricePath.TrimStart('/'),
            new Dictionary<string, string?>
            {
                ["action"] = query.Action,
                ["p_startday"] = startDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["p_endday"] = endDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["p_product_cls_code"] = query.ProductClassCode,
                ["p_item_category_code"] = query.CategoryCode,
                ["p_item_code"] = query.ItemCode,
                ["p_kind_code"] = query.KindCode,
                ["p_product_rank_code"] = query.RankCode,
                ["p_county_code"] = string.Empty,
                ["p_convert_kg_yn"] = "Y",
                ["p_cert_key"] = kamis.CertificationKey,
                ["p_cert_id"] = kamis.RequesterId,
                ["p_returntype"] = "json"
            });

        using var document = await _kamisClient.GetDocumentAsync(requestPath, cancellationToken);
        var data = ReadDataObject(document.RootElement, query.ProductClassCode, query.CategoryCode);
        var resultCode = ReadString(data, "error_code");
        if (!string.Equals(resultCode, "000", StringComparison.Ordinal))
        {
            if (string.Equals(resultCode, "001", StringComparison.Ordinal))
            {
                return [];
            }

            throw new InvalidOperationException(
                $"KAMIS 기간 가격 요청이 거부되었습니다. 가격구분={query.ProductClassCode}, 부류={query.CategoryCode}, 품목={query.ItemCode}, 코드={resultCode}");
        }

        if (!TryGetProperty(data, "item", out var items))
        {
            return [];
        }

        var sourceItems = items.ValueKind switch
        {
            JsonValueKind.Array => items.EnumerateArray().ToArray(),
            JsonValueKind.Object => [items],
            _ => []
        };
        var resolvedItemName = sourceItems
            .Select(item => ReadString(item, "itemname"))
            .FirstOrDefault(value => value.Length > 0) ?? query.ItemName;
        var resolvedKindName = sourceItems
            .Select(item => ReadString(item, "kindname"))
            .FirstOrDefault(value => value.Length > 0) ?? query.KindName;

        return sourceItems
            .Where(item => string.Equals(
                ReadString(item, "countyname"),
                "평균",
                StringComparison.Ordinal))
            .Select(item => MapPeriodObservation(
                item,
                query,
                resolvedItemName,
                resolvedKindName,
                endDate,
                collectedAtUtc))
            .Where(item => item is not null)
            .Select(item => item!)
            .ToArray();
    }

    private static KamisPriceObservation? MapPeriodObservation(
        JsonElement source,
        PeriodProductQuery query,
        string itemName,
        string kindName,
        DateOnly requestedDate,
        DateTime collectedAtUtc)
    {
        var surveyDate = ParsePeriodSurveyDate(
            ReadString(source, "yyyy"),
            ReadString(source, "regday"));
        if (surveyDate is null)
        {
            return null;
        }

        var priceRaw = ReadString(source, "price");
        var priceKrw = ParsePrice(priceRaw);
        var identity = string.Join(
            '\u001f',
            query.ProductClassCode,
            query.CategoryCode,
            NationwideCode,
            surveyDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            query.ItemCode,
            query.KindCode,
            query.RankCode,
            ConvertedKilogramUnit);

        return new KamisPriceObservation
        {
            RecordKey = UsdaNassPriceArchiveService.Sha256(identity),
            ProductClassCode = query.ProductClassCode,
            ProductClassName = query.ProductClassName,
            CategoryCode = query.CategoryCode,
            CategoryName = query.CategoryName,
            CountryCode = NationwideCode,
            CountryName = NationwideName,
            RequestedDate = requestedDate,
            SurveyDate = surveyDate.Value,
            FrequencyCode = "Daily",
            ItemName = itemName,
            ItemCode = query.ItemCode,
            KindName = kindName,
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
