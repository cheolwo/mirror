using System.Globalization;
using System.Text.Json;
using Hongdal.Domain.AgriculturalFisheries;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using static Hongdal.Services.AgriculturalFisheries.Information.KamisJsonReader;
using static Hongdal.Services.AgriculturalFisheries.Information.KamisPriceValueParser;

namespace Hongdal.Services.AgriculturalFisheries.Information;

public sealed partial class KamisPriceArchiveService : IKamisPriceArchiveService
{
    public async Task<KamisPriceArchiveResult> CollectDailyPricesAsync(
        DateOnly requestedDate,
        CancellationToken cancellationToken = default)
    {
        if (requestedDate.Year is < 1990 or > 2100)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedDate));
        }

        EnsureKamisConfigured();

        var run = new KamisPriceCollectionRun
        {
            RequestedDate = requestedDate,
            QuerySummary =
                $"KAMIS 전국 일별 부류별 가격 / 도매·소매 / 6개 부류 / 요청일 {requestedDate:yyyy-MM-dd} / kg 환산",
            SourceUrl = SourceUrl,
            StartedAtUtc = DateTime.UtcNow
        };
        _db.KamisCollectionRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var collectedAtUtc = DateTime.UtcNow;
            var incoming = new List<KamisPriceObservation>();
            foreach (var productClass in ProductClasses)
            {
                foreach (var category in Categories)
                {
                    incoming.AddRange(await FetchCategoryAsync(
                        requestedDate,
                        productClass.Key,
                        productClass.Value,
                        category.Key,
                        category.Value,
                        collectedAtUtc,
                        cancellationToken));
                }
            }

            var deduplicated = incoming
                .GroupBy(item => item.RecordKey, StringComparer.Ordinal)
                .Select(group => group.Last())
                .ToArray();
            var recordKeys = deduplicated
                .Select(item => item.RecordKey)
                .ToHashSet(StringComparer.Ordinal);
            var existing = await _db.KamisPriceObservations
                .Where(item => recordKeys.Contains(item.RecordKey))
                .ToDictionaryAsync(item => item.RecordKey, StringComparer.Ordinal, cancellationToken);

            var updatedCount = 0;
            foreach (var item in deduplicated)
            {
                if (existing.TryGetValue(item.RecordKey, out var stored))
                {
                    if (HasMaterialChanges(stored, item))
                    {
                        CopyMutableValues(stored, item);
                        stored.UpdatedAtUtc = collectedAtUtc;
                        updatedCount++;
                    }

                    stored.LastSeenAtUtc = collectedAtUtc;
                    continue;
                }

                item.FirstCollectionRunId = run.Id;
                _db.KamisPriceObservations.Add(item);
            }

            var latestSurveyDate = deduplicated
                .Select(item => item.SurveyDate)
                .Cast<DateOnly?>()
                .DefaultIfEmpty()
                .Max();
            run.StatusCode = KamisArchiveStatusCodes.Completed;
            run.CompletedAtUtc = DateTime.UtcNow;
            run.LatestSurveyDate = latestSurveyDate;
            run.FetchedCount = deduplicated.Length;
            run.InsertedCount = deduplicated.Length - existing.Count;
            run.UpdatedCount = updatedCount;
            run.ExistingCount = existing.Count;
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "KAMIS 국내 농수산물 가격 수집 완료. RunId={RunId}, Fetched={Fetched}, Inserted={Inserted}, Updated={Updated}, Existing={Existing}",
                run.Id,
                run.FetchedCount,
                run.InsertedCount,
                run.UpdatedCount,
                run.ExistingCount);

            return new KamisPriceArchiveResult(
                run.Id,
                run.FetchedCount,
                run.InsertedCount,
                run.UpdatedCount,
                run.ExistingCount,
                latestSurveyDate);
        }
        catch (Exception ex)
        {
            _db.ChangeTracker.Clear();
            var failedRun = await _db.KamisCollectionRuns
                .SingleAsync(item => item.Id == run.Id, CancellationToken.None);
            failedRun.StatusCode = KamisArchiveStatusCodes.Failed;
            failedRun.CompletedAtUtc = DateTime.UtcNow;
            failedRun.ErrorMessage = ex.Message.Length <= 2000 ? ex.Message : ex.Message[..2000];
            await _db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<IReadOnlyList<KamisPriceObservation>> FetchCategoryAsync(
        DateOnly requestedDate,
        string productClassCode,
        string productClassName,
        string categoryCode,
        string categoryName,
        DateTime collectedAtUtc,
        CancellationToken cancellationToken)
    {
        var kamis = _options.Kamis;
        var requestPath = QueryHelpers.AddQueryString(
            kamis.DailyCategoryPricePath.TrimStart('/'),
            new Dictionary<string, string?>
            {
                ["action"] = "dailyPriceByCategoryList",
                ["p_product_cls_code"] = productClassCode,
                ["p_country_code"] = string.Empty,
                ["p_regday"] = requestedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["p_convert_kg_yn"] = "Y",
                ["p_item_category_code"] = categoryCode,
                ["p_cert_key"] = kamis.CertificationKey,
                ["p_cert_id"] = kamis.RequesterId,
                ["p_returntype"] = "json"
            });

        using var document = await _kamisClient.GetDocumentAsync(requestPath, cancellationToken);
        var data = ReadDataObject(document.RootElement, productClassCode, categoryCode);
        var resultCode = ReadString(data, "error_code");
        if (!string.Equals(resultCode, "000", StringComparison.Ordinal))
        {
            if (string.Equals(resultCode, "001", StringComparison.Ordinal))
            {
                return [];
            }

            throw new InvalidOperationException(
                $"KAMIS 요청이 거부되었습니다. 가격구분={productClassCode}, 부류={categoryCode}, 코드={resultCode}");
        }

        if (!TryGetProperty(data, "item", out var items))
        {
            return [];
        }

        return items.ValueKind switch
        {
            JsonValueKind.Array => items.EnumerateArray()
                .Select(item => MapObservation(
                    item,
                    requestedDate,
                    productClassCode,
                    productClassName,
                    categoryCode,
                    categoryName,
                    collectedAtUtc))
                .ToArray(),
            JsonValueKind.Object =>
            [
                MapObservation(
                    items,
                    requestedDate,
                    productClassCode,
                    productClassName,
                    categoryCode,
                    categoryName,
                    collectedAtUtc)
            ],
            _ => []
        };
    }

    private static KamisPriceObservation MapObservation(
        JsonElement source,
        DateOnly requestedDate,
        string productClassCode,
        string productClassName,
        string categoryCode,
        string categoryName,
        DateTime collectedAtUtc)
    {
        var itemName = ReadString(source, "item_name", "itemname");
        var itemCode = ReadString(source, "item_code", "itemcode");
        var kindName = ReadString(source, "kind_name", "kindname");
        var kindCode = ReadString(source, "kind_code", "kindcode");
        var rankName = ReadString(source, "rank");
        var rankCode = ReadString(source, "rank_code", "rankcode");
        var unit = ConvertedKilogramUnit;
        var priceRaw = ReadString(source, "dpr1");
        var priceKrw = ParsePrice(priceRaw);
        var surveyDate = ParseSurveyDate(ReadString(source, "day1"), requestedDate);
        var identity = string.Join(
            '\u001f',
            productClassCode,
            categoryCode,
            NationwideCode,
            surveyDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            itemCode,
            kindCode,
            rankCode,
            unit);

        return new KamisPriceObservation
        {
            RecordKey = UsdaNassPriceArchiveService.Sha256(identity),
            ProductClassCode = productClassCode,
            ProductClassName = productClassName,
            CategoryCode = categoryCode,
            CategoryName = categoryName,
            CountryCode = NationwideCode,
            CountryName = NationwideName,
            RequestedDate = requestedDate,
            SurveyDate = surveyDate,
            FrequencyCode = "Daily",
            ItemName = itemName,
            ItemCode = itemCode,
            KindName = kindName,
            KindCode = kindCode,
            RankName = rankName,
            RankCode = rankCode,
            Unit = unit,
            PriceRaw = priceRaw,
            PriceKrw = priceKrw,
            PreviousDayLabel = ReadString(source, "day2"),
            PreviousDayPriceKrw = ParsePrice(ReadString(source, "dpr2")),
            OneWeekAgoLabel = ReadString(source, "day3"),
            OneWeekAgoPriceKrw = ParsePrice(ReadString(source, "dpr3")),
            TwoWeeksAgoLabel = ReadString(source, "day4"),
            TwoWeeksAgoPriceKrw = ParsePrice(ReadString(source, "dpr4")),
            OneMonthAgoLabel = ReadString(source, "day5"),
            OneMonthAgoPriceKrw = ParsePrice(ReadString(source, "dpr5")),
            OneYearAgoLabel = ReadString(source, "day6"),
            OneYearAgoPriceKrw = ParsePrice(ReadString(source, "dpr6")),
            NormalYearLabel = ReadString(source, "day7"),
            NormalYearPriceKrw = ParsePrice(ReadString(source, "dpr7")),
            IsPriceMissing = priceKrw is null,
            SourceUrl = SourceUrl,
            RawJson = source.GetRawText(),
            FirstCollectedAtUtc = collectedAtUtc,
            LastSeenAtUtc = collectedAtUtc,
            UpdatedAtUtc = collectedAtUtc
        };
    }

}
