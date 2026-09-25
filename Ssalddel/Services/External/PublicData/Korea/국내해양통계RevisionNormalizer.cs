using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Ssalddel.Domain.PublicData;

namespace 살뜰.Services.External.PublicData.Korea;

/// <summary>관세청 수산물 표본의 같은 통계 식별자와 불변 값 판본을 분리한다.</summary>
public static class 국내해양통계RevisionNormalizer
{
    public const string SourceId = "kcs";
    public const string DatasetId = "marine-monthly-trade-history";
    public const string ParserVersion = "marine-monthly-trade.r1";

    public static IReadOnlyList<외부데이터정규화Record> Parse(string xml, string month, string hsCode,
        string partner, DateTimeOffset collectedAt)
    {
        Require(Regex.IsMatch(month, "^[0-9]{6}$") && DateTime.TryParseExact(month, "yyyyMM", CultureInfo.InvariantCulture, DateTimeStyles.None, out _), "MarineMonthInvalid");
        Require(new[] { "030354", "030214", "030363" }.Contains(hsCode) && new[] { "NO", "RU" }.Contains(partner), "MarineTradeScopeInvalid");
        Require(Encoding.UTF8.GetByteCount(xml) <= 2 * 1024 * 1024, "MarinePayloadTooLarge");
        using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var doc = XDocument.Load(reader);
        Require(doc.Root?.Name.LocalName == "response" && doc.Root.Element("header")?.Element("resultCode")?.Value == "00", "MarineSourceNotSuccess");
        var items = doc.Root!.Element("body")?.Element("items")?.Elements("item").ToList()
            ?? throw new InvalidDataException("MarineItemsMissing");
        Require(items.Count <= 50, "MarineTradeResponseTooLarge");
        var records = new List<외부데이터정규화Record>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            string Field(string name) => item.Element(name)?.Value.Trim() ?? throw new InvalidDataException("MarineFieldMissing:" + name);
            var sourceMonth = Field("year");
            // 합계 행을 월별 실적으로 중복 합산하지 않는다.
            if (sourceMonth is "총계" or "합계" or "총 합계") continue;
            Require(sourceMonth.Replace(".", "").Replace("-", "") == month, "MarineResponseMonthMismatch");
            var sourceHsCode = Field("hsCd");
            // HS6 요청에 HSK10 상세 행이 반환될 수 있다. 접두어를 검증하고 원래 코드로 구분 보존한다.
            Require(Regex.IsMatch(sourceHsCode, "^[0-9]{6}([0-9]{4})?$") && sourceHsCode.StartsWith(hsCode, StringComparison.Ordinal)
                && Field("statCd") == partner, "MarineResponseIdentityMismatch");
            var evidence = new DateTimeOffset(int.Parse(month[..4]), int.Parse(month[4..]), 1, 0, 0, 0, TimeSpan.Zero);
            foreach (var metric in new[] { "impWgt", "impDlr", "expWgt", "expDlr" })
            {
                Require(decimal.TryParse(Field(metric), NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands,
                    CultureInfo.InvariantCulture, out var value) && value >= 0, "MarineNumericInvalid");
                var unit = metric.EndsWith("Wgt", StringComparison.Ordinal) ? "kg" : "USD";
                var basis = metric.StartsWith("imp", StringComparison.Ordinal) ? "CIF" : "FOB";
                var identity = $"kcs:marine:{month}:{sourceHsCode}:{partner}:{metric}";
                Require(seen.Add(identity), "MarineDuplicateObservation");
                var text = JsonSerializer.Serialize(new { hsCode = sourceHsCode, queryHs6 = hsCode, partner, month, metric, unit, basis, name = Field("statKor"), value = value.ToString("G29", CultureInfo.InvariantCulture) });
                var revision = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ParserVersion + "|" + text))).ToLowerInvariant();
                var dimension = $"hs={sourceHsCode};partner={partner};metric={metric};content={revision}";
                records.Add(new 외부데이터정규화Record {
                    SourceId = SourceId, DatasetId = DatasetId, StableId = identity,
                    RecordKey = 외부데이터RecordKey.Create(SourceId, DatasetId, "country:kr", metric, evidence, dimension),
                    RegionStableId = "country:kr", MetricCode = metric, NumericValue = value, TextValue = text, UnitCode = unit,
                    EvidenceAsOfUtc = evidence, CollectedAtUtc = collectedAt, FirstSeenAtUtc = collectedAt, LastSeenAtUtc = collectedAt,
                    SourceVersion = ParserVersion, DataRevision = revision, DimensionKey = dimension,
                    TemporalPrecisionCode = "calendar-month", SpatialPrecisionCode = "country-trade-not-habitat",
                    QualityCode = "PendingHumanReview", LimitationCode = "PrivateReviewOnly;NoRuntime;NoDeletionInference;NotFishPopulation"
                });
            }
        }
        // 빈 응답은 0으로 간주하지 않는다. 마지막 정상 판본도 삭제하지 않는다.
        Require(records.Count > 0, "MarineTradeNoMonthlyData");
        return records.OrderBy(x => x.StableId, StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<외부데이터정규화Record> Latest(IEnumerable<외부데이터정규화Record> versions)
        => versions.GroupBy(x => x.StableId, StringComparer.Ordinal)
            .Select(x => x.OrderByDescending(y => y.LastSeenAtUtc).ThenByDescending(y => y.Id).First())
            .OrderBy(x => x.StableId, StringComparer.Ordinal).ToArray();

    public static bool SameVersion(외부데이터정규화Record x, 외부데이터정규화Record y)
        => x.RecordKey == y.RecordKey && x.StableId == y.StableId && x.DataRevision == y.DataRevision
        && x.NumericValue == y.NumericValue && x.TextValue == y.TextValue && x.UnitCode == y.UnitCode
        && x.SourceId == y.SourceId && x.DatasetId == y.DatasetId && x.RegionStableId == y.RegionStableId
        && x.MetricCode == y.MetricCode && x.EvidenceAsOfUtc == y.EvidenceAsOfUtc && x.SourceVersion == y.SourceVersion
        && x.QualityCode == y.QualityCode && x.LimitationCode == y.LimitationCode && x.DimensionKey == y.DimensionKey
        && x.TemporalPrecisionCode == y.TemporalPrecisionCode && x.SpatialPrecisionCode == y.SpatialPrecisionCode;

    private static void Require(bool condition, string code) { if (!condition) throw new InvalidDataException(code); }
}
