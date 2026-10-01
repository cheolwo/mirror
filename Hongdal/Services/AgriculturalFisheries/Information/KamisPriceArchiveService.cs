using Hongdal.Domain.AgriculturalFisheries;
using Hongdal.Infrastructure.Persistence.AgriculturalFisheries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Services.AgriculturalFisheries.Information;

public sealed partial class KamisPriceArchiveService : IKamisPriceArchiveService
{
    private const string SourceUrl = "https://www.kamis.or.kr/service/price/xml.do";
    private const string NationwideCode = "ALL";
    private const string NationwideName = "전국";
    private const string ConvertedKilogramUnit = "1kg";
    private const int PeriodQueryBatchSize = 12;
    private const int PeriodQueryConcurrency = 2;
    private const int MonthlyQueryBatchSize = 20;
    private const int MonthlyQueryConcurrency = 4;

    private sealed record PeriodProductQuery(
        string Action,
        string ProductClassCode,
        string ProductClassName,
        string CategoryCode,
        string CategoryName,
        string ItemCode,
        string ItemName,
        string KindCode,
        string KindName,
        string RankCode,
        string RankName);

    private sealed record MonthlyProductQuery(
        string CategoryCode,
        string CategoryName,
        string ItemCode,
        string ItemName,
        string KindCode,
        string KindName,
        string RankCode,
        string RankName,
        string GradeRank);

    private static readonly IReadOnlyDictionary<string, string> ProductClasses =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["01"] = "소매",
            ["02"] = "도매"
        };

    private static readonly IReadOnlyDictionary<string, string> Categories =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["100"] = "식량작물",
            ["200"] = "채소류",
            ["300"] = "특용작물",
            ["400"] = "과일류",
            ["500"] = "축산물",
            ["600"] = "수산물"
        };

    private readonly IKamisJsonClient _kamisClient;
    private readonly AgriculturalFisheriesDbContext _db;
    private readonly PublicDataOptions _options;
    private readonly ILogger<KamisPriceArchiveService> _logger;

    public KamisPriceArchiveService(
        IKamisJsonClient kamisClient,
        AgriculturalFisheriesDbContext db,
        IOptions<PublicDataOptions> options,
        ILogger<KamisPriceArchiveService> logger)
    {
        _kamisClient = kamisClient;
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    private async Task<(int Inserted, int Updated, int Existing)> UpsertArchiveBatchAsync(
        long collectionRunId,
        IReadOnlyCollection<KamisPriceObservation> incoming,
        CancellationToken cancellationToken)
    {
        if (incoming.Count == 0)
        {
            return (0, 0, 0);
        }

        var recordKeys = incoming
            .Select(item => item.RecordKey)
            .ToHashSet(StringComparer.Ordinal);
        var existing = await _db.KamisPriceObservations
            .Where(item => recordKeys.Contains(item.RecordKey))
            .ToDictionaryAsync(item => item.RecordKey, StringComparer.Ordinal, cancellationToken);
        var updatedCount = 0;
        var seenAtUtc = DateTime.UtcNow;

        foreach (var item in incoming)
        {
            if (existing.TryGetValue(item.RecordKey, out var stored))
            {
                if (HasPeriodMaterialChanges(stored, item))
                {
                    CopyPeriodMutableValues(stored, item);
                    stored.UpdatedAtUtc = seenAtUtc;
                    updatedCount++;
                }

                stored.LastSeenAtUtc = seenAtUtc;
                continue;
            }

            item.FirstCollectionRunId = collectionRunId;
            _db.KamisPriceObservations.Add(item);
        }

        await _db.SaveChangesAsync(cancellationToken);
        _db.ChangeTracker.Clear();
        return (incoming.Count - existing.Count, updatedCount, existing.Count);
    }

    private void EnsureKamisConfigured()
    {
        var kamis = _options.Kamis;
        if (string.IsNullOrWhiteSpace(kamis.CertificationKey)
            || string.IsNullOrWhiteSpace(kamis.RequesterId))
        {
            throw new InvalidOperationException(
                "KAMIS 인증값이 설정되지 않았습니다. PublicData:Kamis 설정을 확인해 주세요.");
        }
    }

    private static string GetRankName(string rankCode)
        => rankCode switch
        {
            "04" => "상품",
            "05" => "중품",
            _ => $"등급 {rankCode}"
        };

    private static bool HasPeriodMaterialChanges(
        KamisPriceObservation stored,
        KamisPriceObservation incoming)
        => stored.FrequencyCode != incoming.FrequencyCode
           || stored.ItemName != incoming.ItemName
           || stored.KindName != incoming.KindName
           || stored.RankName != incoming.RankName
           || stored.Unit != incoming.Unit
           || stored.PriceRaw != incoming.PriceRaw
           || stored.PriceKrw != incoming.PriceKrw
           || stored.IsPriceMissing != incoming.IsPriceMissing;

    private static void CopyPeriodMutableValues(
        KamisPriceObservation stored,
        KamisPriceObservation incoming)
    {
        stored.RequestedDate = incoming.RequestedDate;
        stored.FrequencyCode = incoming.FrequencyCode;
        stored.ItemName = incoming.ItemName;
        stored.KindName = incoming.KindName;
        stored.RankName = incoming.RankName;
        stored.Unit = incoming.Unit;
        stored.PriceRaw = incoming.PriceRaw;
        stored.PriceKrw = incoming.PriceKrw;
        stored.IsPriceMissing = incoming.IsPriceMissing;
        stored.RawJson = incoming.RawJson;
    }

    private static bool HasMaterialChanges(
        KamisPriceObservation stored,
        KamisPriceObservation incoming)
        => stored.FrequencyCode != incoming.FrequencyCode
           || stored.ItemName != incoming.ItemName
           || stored.KindName != incoming.KindName
           || stored.RankName != incoming.RankName
           || stored.Unit != incoming.Unit
           || stored.PriceRaw != incoming.PriceRaw
           || stored.PriceKrw != incoming.PriceKrw
           || stored.PreviousDayLabel != incoming.PreviousDayLabel
           || stored.PreviousDayPriceKrw != incoming.PreviousDayPriceKrw
           || stored.OneWeekAgoLabel != incoming.OneWeekAgoLabel
           || stored.OneWeekAgoPriceKrw != incoming.OneWeekAgoPriceKrw
           || stored.TwoWeeksAgoLabel != incoming.TwoWeeksAgoLabel
           || stored.TwoWeeksAgoPriceKrw != incoming.TwoWeeksAgoPriceKrw
           || stored.OneMonthAgoLabel != incoming.OneMonthAgoLabel
           || stored.OneMonthAgoPriceKrw != incoming.OneMonthAgoPriceKrw
           || stored.OneYearAgoLabel != incoming.OneYearAgoLabel
           || stored.OneYearAgoPriceKrw != incoming.OneYearAgoPriceKrw
           || stored.NormalYearLabel != incoming.NormalYearLabel
           || stored.NormalYearPriceKrw != incoming.NormalYearPriceKrw;

    private static void CopyMutableValues(
        KamisPriceObservation stored,
        KamisPriceObservation incoming)
    {
        stored.RequestedDate = incoming.RequestedDate;
        stored.FrequencyCode = incoming.FrequencyCode;
        stored.ItemName = incoming.ItemName;
        stored.KindName = incoming.KindName;
        stored.RankName = incoming.RankName;
        stored.Unit = incoming.Unit;
        stored.PriceRaw = incoming.PriceRaw;
        stored.PriceKrw = incoming.PriceKrw;
        stored.PreviousDayLabel = incoming.PreviousDayLabel;
        stored.PreviousDayPriceKrw = incoming.PreviousDayPriceKrw;
        stored.OneWeekAgoLabel = incoming.OneWeekAgoLabel;
        stored.OneWeekAgoPriceKrw = incoming.OneWeekAgoPriceKrw;
        stored.TwoWeeksAgoLabel = incoming.TwoWeeksAgoLabel;
        stored.TwoWeeksAgoPriceKrw = incoming.TwoWeeksAgoPriceKrw;
        stored.OneMonthAgoLabel = incoming.OneMonthAgoLabel;
        stored.OneMonthAgoPriceKrw = incoming.OneMonthAgoPriceKrw;
        stored.OneYearAgoLabel = incoming.OneYearAgoLabel;
        stored.OneYearAgoPriceKrw = incoming.OneYearAgoPriceKrw;
        stored.NormalYearLabel = incoming.NormalYearLabel;
        stored.NormalYearPriceKrw = incoming.NormalYearPriceKrw;
        stored.IsPriceMissing = incoming.IsPriceMissing;
        stored.RawJson = incoming.RawJson;
    }
}
