using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

// 도로명 원문의 작은 후보 집합이다. 법정동 참조를 행정동·도로 형상·건물 위치로 승격하지 않는다.
internal static class 도로명공공자료
{
    private const string Folder = "artifacts/local/public-data/diorama-spatial-20260926-r1";
    private const string FileName = "JUSUZR_DB_ALL_2608.zip";
    private const string Source = "mois-juso-road-name";
    private const string Dataset = "juso-rtlDtaDtlSn-6";
    private const string Revision = "jungnang-four-road-names.202608.r1";
    private const string ExpectedHash = "2c159366a15f0354daa705488a62ca1427e6b4cdbc31af4aa724b8c60ca2b80b";
    private const long ExpectedBytes = 12_129_067;
    private const int ExpectedRows = 372_063;
    private const string Quality = "PendingHumanReview";
    private const string Limits = "PrivateReviewOnly;NoGeometry;NoBuildingOrEntranceBinding;NoPublication;NoRuntime;LegalDongNotAdministrativeDong;BaseNumbersNotAddressExistence";
    private const string CatalogUrl = "https://business.juso.go.kr/jst/jstAddressDetailsSearch";
    private static readonly HashSet<string> Names = new(["사가정로", "면목로", "용마산로", "면목천로"], StringComparer.Ordinal);
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private sealed record Row(int SourceLine, string[] Fields)
    {
        public string RoadCode => Fields[0] + Fields[1];
        public string Sequence => Fields[2];
    }
    private sealed record Frozen(string Path, DateTimeOffset CollectedAt, string LicenseStatus, string LicenseEvidenceUrl,
        DateTimeOffset? LicenseCheckedAt, bool PrivateReviewOnly, List<Row> Rows);

    internal static async Task RunAsync(string mode, string root, Dictionary<string, object?> result)
    {
        Require(mode is "preview" or "self-test" or "apply" or "verify", "ModeInvalid");
        var frozen = Load(root);
        result["sourceRows"] = ExpectedRows;
        result["selectedRows"] = frozen.Rows.Count;
        result["distinctRoadNames"] = frozen.Rows.Select(x => x.Fields[3]).Distinct().Count();
        result["sourceHash"] = ExpectedHash;
        result["catalogMonth"] = "202608";
        result["reviewStatus"] = Quality;
        result["distributionApproved"] = false;
        result["currentBoundaryGatePassed"] = false;
        result["geometryAvailable"] = false;
        result["unityApplied"] = false;
        result["rightsReadyForPrivateStorage"] = RightsReady(frozen);
        result["selection"] = frozen.Rows.Select(x => new { x.SourceLine, x.RoadCode, x.Sequence,
            roadName = x.Fields[3], legalDongCodeFragment = x.Fields[8], legalDongName = x.Fields[9], unusedCode = x.Fields[10] }).ToArray();
        if (mode == "preview") return; // 원본 내용 검토에는 DB 연결이나 파일 쓰기가 필요하지 않다.
        if (mode == "self-test") { result["selfTestsPassed"] = SelfTest(frozen); return; }
        Require(RightsReady(frozen), "PrivateStorageRightsUnverified");
        var options = await 로컬공공자료Db.OptionsAsync(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)));
        if (mode == "apply") await Apply(options, frozen, result);
        await Verify(options, frozen, result);
    }

    private static Frozen Load(string root)
    {
        var folder = Path.Combine(root, Folder);
        var path = Path.Combine(folder, FileName);
        Require(File.Exists(path), "SourceMissing");
        using var receipt = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "road-name-acquisition.json")));
        var r = receipt.RootElement;
        Require(r.GetProperty("sourceId").GetString() == Source && r.GetProperty("datasetId").GetString() == Dataset
            && r.GetProperty("catalogMonth").GetString() == "202608" && r.GetProperty("fileName").GetString() == FileName
            && r.GetProperty("catalogUrl").GetString() == CatalogUrl, "ReceiptScopeChanged");
        Require(r.GetProperty("sha256").GetString() == ExpectedHash && r.GetProperty("byteLength").GetInt64() == ExpectedBytes,
            "ReceiptFingerprintChanged");
        using (var input = File.OpenRead(path))
            ValidateFingerprint(input.Length, Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant());
        var collected = r.GetProperty("collectedAtUtc").GetDateTimeOffset();
        Require(collected.Offset == TimeSpan.Zero && collected.Year == 2026, "CollectionTimeInvalid");
        var selected = new List<Row>();
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var archive = ZipFile.OpenRead(path);
        Require(archive.Entries.Count == 1 && archive.Entries[0].FullName == "TN_SPRD_RDNM.txt"
            && archive.Entries[0].Length <= 256L * 1024 * 1024, "ArchiveContractChanged");
        using var reader = new StreamReader(archive.Entries[0].Open(), Encoding.GetEncoding(949, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback), false);
        var count = 0;
        while (reader.ReadLine() is { } line)
        {
            count++;
            Require(count <= ExpectedRows && line.Length <= 16_384, "SourceBudgetExceeded");
            var fields = line.Split('|', StringSplitOptions.None);
            Require(fields.Length == 21, "ColumnCountChanged");
            if (Selected(fields)) selected.Add(ParseSelected(fields, count));
        }
        Require(count == ExpectedRows, "SourceRowCountChanged");
        ValidateSelected(selected);
        selected.Sort((a, b) => StringComparer.Ordinal.Compare(a.RoadCode + a.Sequence, b.RoadCode + b.Sequence));
        var license = r.TryGetProperty("licenseStatus", out var ls) ? ls.GetString() ?? "" : "";
        var url = r.TryGetProperty("licenseEvidenceUrl", out var lu) ? lu.GetString() ?? "" : "";
        DateTimeOffset? checkedAt = r.TryGetProperty("licenseCheckedAtUtc", out var lc) && lc.TryGetDateTimeOffset(out var value) ? value : null;
        var privateOnly = r.TryGetProperty("privateReviewOnly", out var pr) && pr.ValueKind == JsonValueKind.True;
        return new(path, Microseconds(collected), license, url, checkedAt, privateOnly, selected);
    }

    // 내부 비공개 검토 저장 범위의 확인값이다. 개별 KOGL 유형이나 외부 재배포 허용을 뜻하지 않는다.
    private static bool RightsReady(Frozen f) => f.LicenseStatus == "OfficialPublicDownloadPrivateReview" && f.PrivateReviewOnly
        && f.LicenseCheckedAt is { Offset: var offset } && offset == TimeSpan.Zero
        && Uri.TryCreate(f.LicenseEvidenceUrl, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && uri.Host == "business.juso.go.kr";
    private static void ValidateFingerprint(long length, string hash)
        => Require(length == ExpectedBytes && hash == ExpectedHash, "SourceFingerprintChanged");
    private static bool Selected(string[] fields) => fields[0] == "11260" && Names.Contains(fields[3]);
    private static Row ParseSelected(string[] fields, int sourceLine)
    {
        Require(fields.Length == 21, "ColumnCountChanged");
        Require(Digits(fields[0], 5) && Digits(fields[1], 7) && Digits(fields[2], 2), "RoadIdentifierInvalid");
        Require(fields[10] is "0" or "1", "UnusedCodeInvalid");
        Require(fields[7] is "1" or "2", "DongAssignmentTypeInvalid");
        Require(fields[8].Length == 0 || Digits(fields[8], 3), "LegalDongFragmentInvalid");
        return new(sourceLine, fields.ToArray()); // 빈 원필드도 그대로 보존한다.
    }
    private static void ValidateSelected(List<Row> rows)
    {
        Require(rows.Select(x => x.RoadCode + ":" + x.Sequence).Distinct(StringComparer.Ordinal).Count() == rows.Count,
            "DuplicateRoadSequence");
        Require(rows.Count == 11 && rows.Select(x => x.Fields[3]).Distinct(StringComparer.Ordinal).Count() == 4,
            "SelectedScopeChanged");
        Require(rows.Count(x => x.Sequence == "00") == 4, "AggregateRowsChanged");
    }
    private static string Key(Row row) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        string.Join("|", Source, Dataset, ExpectedHash, row.RoadCode, row.Sequence)))).ToLowerInvariant();
    private static List<외부데이터정규화Record> Records(Frozen f, long rawId = 0) => f.Rows.Select(row =>
    {
        var text = JsonSerializer.Serialize(new { row.SourceLine, fields = row.Fields, catalogMonth = "202608",
            catalogUrl = CatalogUrl, f.LicenseStatus, f.LicenseEvidenceUrl }, Json);
        Require(text.Length <= 2000, "RecordTextTooLong");
        return new 외부데이터정규화Record
        {
            RawSnapshotId = rawId, RecordKey = Key(row), StableId = "juso:road-name:" + row.RoadCode + ":" + row.Sequence,
            SourceId = Source, DatasetId = Dataset, RegionStableId = "region:kr:sig:11260", MetricCode = "road-name-catalog-entry",
            NumericValue = null, TextValue = text, UnitCode = "source-row",
            // 월 자료의 색인 기준점이며 도로 실측일 또는 변경 효력일이 아니다.
            EvidenceAsOfUtc = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero), CollectedAtUtc = f.CollectedAt,
            SpatialPrecisionCode = "road-name-without-geometry", TemporalPrecisionCode = "catalog-month",
            QualityCode = Quality, LimitationCode = Limits, DimensionKey = "roadCode=" + row.RoadCode + ";dongSequence=" + row.Sequence,
            SourceVersion = "202608;sha256:" + ExpectedHash, DataRevision = Revision,
            FirstSeenAtUtc = DateTimeOffset.UtcNow, LastSeenAtUtc = DateTimeOffset.UtcNow
        };
    }).ToList();

    private static async Task Apply(DbContextOptions<PublicDataIngestionDbContext> options, Frozen frozen, Dictionary<string, object?> result)
    {
        await using var db = new PublicDataIngestionDbContext(options);
        await db.Database.OpenConnectionAsync();
        await using var gate = db.Database.GetDbConnection().CreateCommand();
        gate.CommandText = "SELECT GET_LOCK('mirror:public-data:juso-four-roads-202608',0)";
        Require(Convert.ToInt32(await gate.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 1, "ImportBusy");
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var before = await Read(db);
            var expected = Records(frozen);
            Require(before.All(x => expected.Any(y => Equivalent(x, y)) && x.RawSnapshot?.ContentHashSha256 == ExpectedHash), "ExistingContentConflict");
            result["databaseWriteAttempted"] = true;
            var registration = new 평창군공공공간원본등록Service(db);
            var request = new 공공공간원본등록Request(Source, Dataset, "202608;sha256:" + ExpectedHash, Revision,
                new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero), "application/zip", Folder + "/" + FileName);
            var saved = await registration.RegisterFileAsync(frozen.Path, request);
            Require(saved.SourceHashSha256 == ExpectedHash && saved.ContentLength == ExpectedBytes, "SourceChangedDuringImport");
            var store = new EfExternalDataIngestionStore(db);
            var stored = await store.UpsertNormalizedAsync(Records(frozen, saved.RawSnapshotId));
            Require(stored.UpdatedCount == 0 && stored.InsertedCount + stored.ExistingCount == 11, "UnexpectedUpdate");
            if (saved.Inserted)
            {
                var raw = await db.RawSnapshots.SingleAsync(x => x.Id == saved.RawSnapshotId);
                raw.CollectedAtUtc = frozen.CollectedAt;
                var run = await db.IngestionRuns.SingleAsync(x => x.Id == raw.FirstCollectionRunId);
                run.StatusCode = 외부데이터수집StatusCodes.Partial; run.FetchedCount = ExpectedRows; run.NormalizedCount = 11;
                run.InsertedCount = stored.InsertedCount; run.ExistingCount = stored.ExistingCount; run.ErrorCode = Quality;
                run.ErrorSummary = Limits;
                await db.SaveChangesAsync();
            }
            var repeatRaw = await registration.RegisterFileAsync(frozen.Path, request);
            var repeated = await store.UpsertNormalizedAsync(Records(frozen, saved.RawSnapshotId));
            Require(repeatRaw.SourceHashSha256 == ExpectedHash && repeatRaw.ContentLength == ExpectedBytes
                && !repeatRaw.Inserted && repeatRaw.RawSnapshotId == saved.RawSnapshotId && repeated.InsertedCount == 0
                && repeated.UpdatedCount == 0 && repeated.ExistingCount == 11, "RepeatInputNotIdempotent");
            await transaction.CommitAsync();
            result["committed"] = true; result["insertedRows"] = stored.InsertedCount; result["existingRows"] = stored.ExistingCount;
            result["rawInsertedCount"] = saved.Inserted ? 1 : 0; result["repeatInsertedRows"] = repeated.InsertedCount;
            result["rawSnapshotId"] = saved.RawSnapshotId;
        }
        finally { gate.CommandText = "SELECT RELEASE_LOCK('mirror:public-data:juso-four-roads-202608')"; _ = await gate.ExecuteScalarAsync(); }
    }
    private static Task<List<외부데이터정규화Record>> Read(PublicDataIngestionDbContext db) => db.NormalizedRecords.AsNoTracking()
        .Include(x => x.RawSnapshot).Where(x => x.SourceId == Source && x.DatasetId == Dataset && x.DataRevision == Revision).ToListAsync();
    private static async Task Verify(DbContextOptions<PublicDataIngestionDbContext> options, Frozen frozen, Dictionary<string, object?> result)
    {
        await using var db = new PublicDataIngestionDbContext(options);
        var stored = await Read(db); var expected = Records(frozen);
        Require(stored.Count == 11 && stored.All(x => expected.Any(y => Equivalent(x, y))
            && x.RawSnapshot?.ContentHashSha256 == ExpectedHash), "IndependentReadbackMismatch");
        var raw = await db.RawSnapshots.AsNoTracking().Include(x => x.FirstCollectionRun)
            .SingleOrDefaultAsync(x => x.SourceId == Source && x.DatasetId == Dataset && x.ContentHashSha256 == ExpectedHash);
        Require(raw is not null && raw.ContentLength == ExpectedBytes && raw.ContentType == "application/zip"
            && raw.StorageLocation == "private-file://" + Folder + "/" + FileName && raw.CollectedAtUtc == frozen.CollectedAt
            && raw.FirstCollectionRun?.ErrorCode == Quality && raw.FirstCollectionRun.ErrorSummary == Limits
            && raw.FirstCollectionRun.NormalizedCount == 11, "RawReadbackMismatch");
        result["verifiedRows"] = stored.Count; result["verifiedRawSnapshots"] = 1; result["independentReadback"] = true;
        result["rawSnapshotId"] = raw!.Id; result["target"] = "hongdal-mysql-1 / hongdal_dev";
    }
    private static bool Equivalent(외부데이터정규화Record a, 외부데이터정규화Record b) => a.RecordKey == b.RecordKey
        && a.StableId == b.StableId && a.SourceId == b.SourceId && a.DatasetId == b.DatasetId && a.RegionStableId == b.RegionStableId
        && a.MetricCode == b.MetricCode && a.NumericValue == b.NumericValue && a.TextValue == b.TextValue && a.UnitCode == b.UnitCode
        && a.EvidenceAsOfUtc == b.EvidenceAsOfUtc && a.CollectedAtUtc == b.CollectedAtUtc && a.SpatialPrecisionCode == b.SpatialPrecisionCode
        && a.TemporalPrecisionCode == b.TemporalPrecisionCode && a.QualityCode == b.QualityCode && a.LimitationCode == b.LimitationCode
        && a.DimensionKey == b.DimensionKey && a.SourceVersion == b.SourceVersion && a.DataRevision == b.DataRevision;

    private static int SelfTest(Frozen frozen)
    {
        var tests = 0;
        void Check(bool ok, string name) { Require(ok, "SelfTest:" + name); tests++; }
        void Reject(Action action, string code) { try { action(); } catch (InvalidDataException e) when (e.Message == "RoadNamePublicData:" + code) { tests++; return; } throw new InvalidDataException("RoadNamePublicData:SelfTestExpected:" + code); }
        var row = frozen.Rows[0];
        Check(frozen.Rows.Count == 11 && frozen.Rows.Count(x => x.Sequence == "00") == 4, "TotalAndAggregateRows");
        Reject(() => ParseSelected(row.Fields[..20], 1), "ColumnCountChanged");
        var bad = row.Fields.ToArray(); bad[1] = "bad"; Reject(() => ParseSelected(bad, 1), "RoadIdentifierInvalid");
        bad = row.Fields.ToArray(); bad[10] = "9"; Reject(() => ParseSelected(bad, 1), "UnusedCodeInvalid");
        var inactive = row.Fields.ToArray(); inactive[10] = "1";
        Check(ParseSelected(inactive, 1).Fields[10] == "1" && Selected(inactive), "InactivePreserved");
        var aggregate = frozen.Rows.First(x => x.Sequence == "00");
        Check(aggregate.Fields[8] == "" && aggregate.Fields[9] == "" && aggregate.Fields[20] == "", "EmptySourceFieldsPreserved");
        Reject(() => ValidateSelected(frozen.Rows.Concat([row]).ToList()), "DuplicateRoadSequence");
        Reject(() => ValidateFingerprint(ExpectedBytes, new string('0', 64)), "SourceFingerprintChanged");
        Reject(() => ValidateFingerprint(ExpectedBytes - 1, ExpectedHash), "SourceFingerprintChanged");
        var sideStreet = row.Fields.ToArray(); sideStreet[3] += "1길";
        Check(!Selected(sideStreet), "ExactRoadNameScope");
        var records = Records(frozen);
        Check(records.Select(x => x.RecordKey).Distinct().Count() == 11 && records.Select(x => x.RecordKey).SequenceEqual(Records(frozen).Select(x => x.RecordKey)), "DeterministicKeys");
        foreach (var item in frozen.Rows)
        {
            using var text = JsonDocument.Parse(records.Single(x => x.RecordKey == Key(item)).TextValue);
            Check(text.RootElement.GetProperty("sourceLine").GetInt32() == item.SourceLine
                && text.RootElement.GetProperty("fields").EnumerateArray().Select(x => x.GetString()).SequenceEqual(item.Fields), "SourceFieldsRoundTrip");
        }
        Check(!RightsReady(frozen with { LicenseStatus = "" }) && !RightsReady(frozen with { PrivateReviewOnly = false }), "RightsFailClosed");
        Check(records.All(x => x.NumericValue is null && x.LimitationCode == Limits && x.QualityCode == Quality), "NoGeometryOrPublicationPromotion");
        Check(Microseconds(frozen.CollectedAt).Ticks % 10 == 0, "MySqlTimePrecision");
        return tests;
    }
    private static DateTimeOffset Microseconds(DateTimeOffset value) => new(value.UtcTicks - value.UtcTicks % 10, TimeSpan.Zero);
    private static bool Digits(string value, int length) => value.Length == length && value.All(c => c is >= '0' and <= '9');
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidDataException("RoadNamePublicData:" + code); }
}
