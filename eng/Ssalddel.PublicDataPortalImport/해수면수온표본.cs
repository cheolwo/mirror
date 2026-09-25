using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

/// <summary>한정된 NOAA 분석 수온 표본. 관측 어군·실시간 예측·공개 Unity 자원이 아니다.</summary>
internal static class 해수면수온표본
{
    private const string Source = "noaa-coastwatch";
    private const string Dataset = "noaacwBLENDEDsstDNDaily";
    private const string Version = "Geo_Polar_Blended-OSPO-L4-GLOB-v1.0";
    private const string Revision = "marine-sst-pilot.20260901-07.r1";
    private const string Region = "area:marine:pilot:37.025:130.075";
    private const string Metric = "sea-surface-foundation-temperature";
    private const string Limitations = "PrivateReviewOnly;NoPublication;NoRuntime;L4Analysis;NotFishLocation;NoDepthProfile";
    public const string Relative = "artifacts/local/public-data/marine/sst-20260901-07-r1";
    private const string MetadataUrl = "https://coastwatch.noaa.gov/erddap/info/" + Dataset + "/index.json";
    private const string Slice = "[(2026-09-01T12:00:00Z):1:(2026-09-07T12:00:00Z)][(36.975):1:(37.075)][(130.025):1:(130.125)]";
    private static readonly string DataUrl = "https://coastwatch.noaa.gov/erddap/griddap/" + Dataset + ".json?"
        + Uri.EscapeDataString("analysed_sst" + Slice + ",analysis_error" + Slice + ",mask" + Slice);
    private static readonly DateTimeOffset First = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    internal sealed record Cell(DateTimeOffset Time, decimal Latitude, decimal Longitude, decimal? Celsius,
        decimal? AnalysisErrorCelsius, int Mask);
    private sealed record Receipt(string DataUrl, string MetadataUrl, string DataHash, string MetadataHash,
        string ResultHash, DateTimeOffset CollectedAtUtc);

    public static async Task RunAsync(string mode, string root, Dictionary<string, object?> result)
    {
        Require(mode is "acquire" or "self-test" or "preview" or "apply" or "verify" or "export", "MarineModeInvalid");
        result["scope"] = "L4 analysis; 9 cells; 2026-09-01..07; private review; no fish simulation";
        if (mode == "self-test") { result["selfTestsPassed"] = SelfTest(); return; }
        var folder = Path.Combine(root, Relative);
        if (mode == "acquire")
        {
            Require(!Directory.Exists(folder) || !Directory.EnumerateFileSystemEntries(folder).Any(), "MarineAcquisitionAlreadyExists");
            using var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(55), MaxResponseContentBufferSize = 2 * 1024 * 1024 };
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(110));
            result["acquisitionStage"] = "Metadata";
            var metadata = await FetchAsync(http, MetadataUrl, "Metadata", deadline.Token);
            ValidateMetadata(Encoding.UTF8.GetString(metadata));
            result["acquisitionStage"] = "TemperatureData";
            var data = await FetchAsync(http, DataUrl, "TemperatureData", deadline.Token);
            var cells = Parse(Encoding.UTF8.GetString(data));
            Require(cells.Count == 63, "MarinePilotIncomplete");
            var receipt = new Receipt(DataUrl, MetadataUrl, Hash(data), Hash(metadata), ResultHash(cells), DateTimeOffset.UtcNow);
            Directory.CreateDirectory(folder);
            await WriteNewAsync(Path.Combine(folder, "metadata.json"), metadata);
            await WriteNewAsync(Path.Combine(folder, "sst.json"), data);
            await WriteNewAsync(Path.Combine(folder, "receipt.json"), JsonSerializer.SerializeToUtf8Bytes(receipt, Pretty));
            result["acquiredRows"] = cells.Count;
            result["resultHash"] = receipt.ResultHash;
            result["privateFolder"] = Relative;
            result["acquisitionStage"] = "Complete";
            return;
        }

        var receiptInput = JsonSerializer.Deserialize<Receipt>(await File.ReadAllTextAsync(Path.Combine(folder, "receipt.json")))!;
        Require(receiptInput.DataUrl == DataUrl && receiptInput.MetadataUrl == MetadataUrl, "MarineSourceChanged");
        var raw = await File.ReadAllBytesAsync(Path.Combine(folder, "sst.json"));
        var info = await File.ReadAllBytesAsync(Path.Combine(folder, "metadata.json"));
        Require(Hash(raw) == receiptInput.DataHash && Hash(info) == receiptInput.MetadataHash, "MarineInputHashChanged");
        ValidateMetadata(Encoding.UTF8.GetString(info));
        var samples = Parse(Encoding.UTF8.GetString(raw));
        Require(samples.Count == 63 && ResultHash(samples) == receiptInput.ResultHash, "MarineResultChanged");
        var expected = samples.Select(x => Normalize(x, receiptInput.CollectedAtUtc)).ToList();
        var keys = expected.Select(x => x.RecordKey).ToList();
        var options = await 로컬공공자료Db.OptionsAsync(root);
        if (mode == "apply")
        {
            await using var db = new PublicDataIngestionDbContext(options);
            await db.Database.OpenConnectionAsync();
            await using var gate = db.Database.GetDbConnection().CreateCommand();
            gate.CommandText = "SELECT GET_LOCK('mirror:public-data:marine-sst-pilot-r1',0)";
            Require(Convert.ToInt32(await gate.ExecuteScalarAsync()) == 1, "MarineImportBusy");
            try
            {
                await using var tx = await db.Database.BeginTransactionAsync();
                var existing = await db.NormalizedRecords.AsNoTracking().Where(x => keys.Contains(x.RecordKey)).ToListAsync();
                Require(existing.All(x => expected.Any(y => Same(x, y))), "MarineExistingConflict");
                result["databaseWriteAttempted"] = true;
                var registered = await new 평창군공공공간원본등록Service(db).RegisterFileAsync(Path.Combine(folder, "sst.json"),
                    new 공공공간원본등록Request(Source, Dataset, Version, Revision, First.AddDays(6), "application/json", Relative + "/sst.json"));
                foreach (var item in expected) item.RawSnapshotId = registered.RawSnapshotId;
                var saved = await new EfExternalDataIngestionStore(db).UpsertNormalizedAsync(expected);
                Require(saved.UpdatedCount == 0, "MarineUnexpectedUpdate");
                if (registered.Inserted)
                {
                    var snapshot = await db.RawSnapshots.SingleAsync(x => x.Id == registered.RawSnapshotId);
                    snapshot.CollectedAtUtc = receiptInput.CollectedAtUtc;
                    var run = await db.IngestionRuns.SingleAsync(x => x.Id == snapshot.FirstCollectionRunId);
                    run.FetchedCount = samples.Count; run.NormalizedCount = expected.Count; run.InsertedCount = saved.InsertedCount;
                    run.StatusCode = 외부데이터수집StatusCodes.Partial;
                    run.ErrorCode = "PendingHumanReview";
                    run.ErrorSummary = "Bounded private NOAA L4 SST analysis; no publication/fish inference. " + Relative + "/receipt.json";
                    await db.SaveChangesAsync();
                }
                await tx.CommitAsync();
                result["committed"] = true; // DB transaction only, never a Git commit.
                result["inserted"] = saved.InsertedCount; result["existing"] = saved.ExistingCount; result["updated"] = saved.UpdatedCount;
            }
            finally
            {
                gate.CommandText = "SELECT RELEASE_LOCK('mirror:public-data:marine-sst-pilot-r1')";
                await gate.ExecuteScalarAsync();
            }
        }

        // 별도 DbContext에서 재조회. 프레임은 원본 파일이 아닌 저장된 값에서 만든다.
        await using var readback = new PublicDataIngestionDbContext(options);
        var stored = await readback.NormalizedRecords.AsNoTracking().Where(x => keys.Contains(x.RecordKey)).Include(x => x.RawSnapshot).ToListAsync();
        if (mode != "preview") Require(stored.Count == 63, "MarineReadbackCountMismatch");
        Require(stored.All(x => expected.Any(y => Same(x, y)) && x.RawSnapshot?.ContentHashSha256 == receiptInput.DataHash), "MarineReadbackMismatch");
        var replay = stored.Select(x => JsonSerializer.Deserialize<Cell>(x.TextValue)!).ToList();
        if (mode != "preview") Require(ResultHash(replay) == receiptInput.ResultHash, "MarineReadbackHashMismatch");
        if (mode == "export")
        {
            // 검증된 DB 조회 결과만 Editor 로컬 검토로 인계한다. 원본의 비공개 상태는 변경하지 않는다.
            var payload = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = "marine-sst-preview.v1", sourceId = Source, datasetId = Dataset,
                sourceUrl = MetadataUrl, sourceHash = receiptInput.DataHash, resultHash = receiptInput.ResultHash,
                collectedAtUtc = receiptInput.CollectedAtUtc.ToString("O"),
                localReviewOnly = true, distributionApproved = false, unit = "degree_C", gridDegrees = 0.05m,
                sourceKind = "L4Analysis", cells = replay.OrderBy(x => x.Time).ThenBy(x => x.Latitude).ThenBy(x => x.Longitude)
                    .Select(x => new { time = x.Time.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                        latitude = x.Latitude, longitude = x.Longitude, hasValue = x.Celsius.HasValue,
                        celsius = x.Celsius ?? 0m, hasError = x.AnalysisErrorCelsius.HasValue,
                        errorCelsius = x.AnalysisErrorCelsius ?? 0m, mask = x.Mask }).ToArray()
            }, Pretty);
            var path = Path.Combine(folder, "sst-preview.json");
            async Task SaveSame(string target, byte[] bytes)
            {
                if (File.Exists(target)) Require((await File.ReadAllBytesAsync(target)).SequenceEqual(bytes), "MarineExportConflict");
                else await WriteNewAsync(target, bytes);
            }
            await SaveSame(path, payload);
            await SaveSame(path + ".sha256", Encoding.ASCII.GetBytes(Hash(payload)));
            result["exportPath"] = path; result["exportHash"] = Hash(payload);
        }
        result["target"] = "hongdal-mysql-1 / hongdal_dev";
        result["verifiedRows"] = stored.Count;
        result["resultHash"] = ResultHash(replay);
        result["frames"] = Enumerable.Range(0, 7).Select(day =>
        {
            var date = First.AddDays(day);
            var frame = Frame(replay, date);
            var values = frame.Where(x => x.Celsius.HasValue).Select(x => x.Celsius!.Value).ToArray();
            return new { date, cells = frame.Count, missingCells = 9 - values.Length,
                minCelsius = values.Length == 0 ? (decimal?)null : values.Min(), maxCelsius = values.Length == 0 ? (decimal?)null : values.Max() };
        }).ToArray();
    }

    internal static List<Cell> Parse(string json)
    {
        Require(Encoding.UTF8.GetByteCount(json) <= 2 * 1024 * 1024, "MarinePayloadTooLarge");
        using var doc = JsonDocument.Parse(json);
        var table = doc.RootElement.GetProperty("table");
        Require(table.GetProperty("columnNames").EnumerateArray().Select(x => x.GetString()).SequenceEqual(
            new[] { "time", "latitude", "longitude", "analysed_sst", "analysis_error", "mask" }), "MarineColumnsChanged");
        Require(table.GetProperty("columnUnits").EnumerateArray().Select(x => x.GetString() ?? "").SequenceEqual(
            new[] { "UTC", "degrees_north", "degrees_east", "degree_C", "degree_C", "" }), "MarineUnitsChanged");
        var rows = table.GetProperty("rows");
        Require(rows.GetArrayLength() <= 63, "MarineScopeExceeded");
        var cells = new List<Cell>();
        var identities = new HashSet<(DateTimeOffset, decimal, decimal)>();
        foreach (var row in rows.EnumerateArray())
        {
            Require(row.GetArrayLength() == 6, "MarineRowShapeChanged");
            Require(DateTimeOffset.TryParseExact(row[0].GetString(), "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var time), "MarineTimeInvalid");
            Require(time >= First && time <= First.AddDays(6) && (time - First).TotalDays % 1 == 0, "MarineDateOutOfScope");
            var lat = row[1].GetDecimal(); var lon = row[2].GetDecimal();
            Require(new[] { 36.975m, 37.025m, 37.075m }.Contains(lat)
                && new[] { 130.025m, 130.075m, 130.125m }.Contains(lon), "MarineCoordinateOutOfScope");
            Require(row[5].GetInt32() == 1, "MarineNonWaterCellRejected");
            decimal? ReadValue(JsonElement value, decimal min, decimal max)
            {
                if (value.ValueKind == JsonValueKind.Null) return null;
                var number = value.GetDecimal();
                if (number == -327.68m) return null;
                Require(number >= min && number <= max, "MarineValueOutOfRange");
                return number;
            }
            var sst = ReadValue(row[3], -2.00001m, 40m);
            var error = ReadValue(row[4], 0m, 5m);
            Require(identities.Add((time, lat, lon)), "MarineDuplicateCell");
            cells.Add(new(time, lat, lon, sst, error, 1));
        }
        return cells.OrderBy(x => x.Time).ThenBy(x => x.Latitude).ThenBy(x => x.Longitude).ToList();
    }

    // 정확한 기록 시각만 반환. 누락일을 전날 값이나 가짜 보간값으로 대체하지 않는다.
    internal static IReadOnlyList<Cell> Frame(IEnumerable<Cell> cells, DateTimeOffset date)
        => cells.Where(x => x.Time == date).OrderBy(x => x.Latitude).ThenBy(x => x.Longitude).ToArray();

    private static void ValidateMetadata(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var attributes = doc.RootElement.GetProperty("table").GetProperty("rows").EnumerateArray()
            .Where(x => x[0].GetString() == "attribute").ToDictionary(x => (x[1].GetString()!, x[2].GetString()!), x => x[4].GetString());
        Require(attributes[("NC_GLOBAL", "id")] == Version && attributes[("NC_GLOBAL", "processing_level")] == "L4", "MarineProductChanged");
        Require(attributes[("NC_GLOBAL", "license")] == "GHRSST protocol describes data use as free and open", "MarineLicenseChanged");
        Require(attributes[("analysed_sst", "units")] == "degree_C" && attributes[("analysis_error", "units")] == "degree_C", "MarineMetadataUnitsChanged");
        Require(attributes[("analysed_sst", "standard_name")] == "sea_surface_foundation_temperature"
            && attributes[("mask", "flag_values")] == "1, 2, 4" && attributes[("mask", "flag_meanings")] == "water land ice", "MarineSemanticsChanged");
    }

    private static 외부데이터정규화Record Normalize(Cell cell, DateTimeOffset collected)
    {
        var dimension = FormattableString.Invariant($"lat={cell.Latitude:F3};lon={cell.Longitude:F3};grid=0.05deg;level=L4");
        return new 외부데이터정규화Record
        {
            RecordKey = 외부데이터RecordKey.Create(Source, Dataset, Region, Metric, cell.Time, dimension),
            StableId = FormattableString.Invariant($"noaa:sst:{cell.Time:yyyyMMdd}:{cell.Latitude:F3}:{cell.Longitude:F3}"),
            SourceId = Source, DatasetId = Dataset, RegionStableId = Region, MetricCode = Metric,
            NumericValue = cell.Celsius, TextValue = JsonSerializer.Serialize(cell), UnitCode = "degree_C",
            EvidenceAsOfUtc = cell.Time, CollectedAtUtc = collected, SpatialPrecisionCode = "grid-0.05-degree",
            TemporalPrecisionCode = "daily-analysis-reference-12Z", QualityCode = "PendingHumanReview", LimitationCode = Limitations,
            DimensionKey = dimension, SourceVersion = Version, DataRevision = Revision, FirstSeenAtUtc = collected, LastSeenAtUtc = collected
        };
    }

    private static bool Same(외부데이터정규화Record x, 외부데이터정규화Record y)
        => x.RecordKey == y.RecordKey && x.StableId == y.StableId && x.SourceId == y.SourceId && x.DatasetId == y.DatasetId
        && x.RegionStableId == y.RegionStableId && x.MetricCode == y.MetricCode && x.NumericValue == y.NumericValue
        && x.TextValue == y.TextValue && x.UnitCode == y.UnitCode && x.EvidenceAsOfUtc == y.EvidenceAsOfUtc
        && x.QualityCode == y.QualityCode && x.LimitationCode == y.LimitationCode && x.DimensionKey == y.DimensionKey
        && x.SourceVersion == y.SourceVersion && x.DataRevision == y.DataRevision
        && x.SpatialPrecisionCode == y.SpatialPrecisionCode && x.TemporalPrecisionCode == y.TemporalPrecisionCode;

    private static string ResultHash(IEnumerable<Cell> cells) => Hash(JsonSerializer.SerializeToUtf8Bytes(
        cells.OrderBy(x => x.Time).ThenBy(x => x.Latitude).ThenBy(x => x.Longitude).ToArray()));
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static async Task WriteNewAsync(string path, byte[] bytes)
    {
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await output.WriteAsync(bytes);
    }
    private static async Task<byte[]> FetchAsync(HttpClient http, string url, string stage, CancellationToken cancellationToken)
    {
        try { return await http.GetByteArrayAsync(url, cancellationToken); }
        catch (OperationCanceledException) { throw new InvalidDataException("Marine" + stage + "Timeout"); }
        catch (HttpRequestException ex) { throw new InvalidDataException("Marine" + stage + "HttpFailure:" + (ex.StatusCode is null ? "Network" : ((int)ex.StatusCode).ToString(CultureInfo.InvariantCulture))); }
    }
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidDataException(code); }

    private static int SelfTest()
    {
        string Input(object?[][] rows, string unit = "degree_C") => JsonSerializer.Serialize(new { table = new {
            columnNames = new[] { "time", "latitude", "longitude", "analysed_sst", "analysis_error", "mask" },
            columnUnits = new[] { "UTC", "degrees_north", "degrees_east", unit, "degree_C", "" }, rows } });
        object?[] Row(string date = "2026-09-01T12:00:00Z", decimal lat = 36.975m, object? value = null, int mask = 1)
            => new object?[] { date, lat, 130.025m, value, 0.2m, mask };
        var row = Row(value: 27m);
        var cells = Parse(Input(new[] { row }));
        Require(cells.Count == 1 && cells[0].Celsius == 27m, "MarineParseTest");
        Require(Frame(cells, First).Count == 1 && Frame(cells, First.AddDays(1)).Count == 0, "MarineMissingFrameTest");
        Require(Parse(Input(new[] { Row() }))[0].Celsius == null && Parse(Input(new[] { Row(value: -327.68m) }))[0].Celsius == null, "MarineMissingValueTest");
        var reversed = Parse(Input(new[] { Row("2026-09-02T12:00:00Z", value: 28m), row }));
        Require(ResultHash(reversed) == ResultHash(reversed.AsEnumerable().Reverse()), "MarineDeterminismTest");
        Require(Normalize(cells[0], First).RecordKey == Normalize(cells[0], First.AddDays(10)).RecordKey, "MarineIdempotencyKeyTest");
        var bad = new[] { Input(new[] { row }, "K"), Input(new[] { row, row }), Input(new[] { Row(lat: 37.5m) }),
            Input(new[] { Row("2026-09-08T12:00:00Z") }), Input(new[] { Row("2026-09-01T00:00:00Z") }),
            Input(new[] { Row(mask: 2) }), Input(new[] { Row(mask: 5) }), Input(new[] { Row(value: 99m) }),
            Input(Enumerable.Repeat(row, 64).ToArray()) };
        foreach (var input in bad)
        {
            var rejected = false;
            try { Parse(input); } catch (InvalidDataException) { rejected = true; }
            Require(rejected, "MarineNegativeTest");
        }
        return 5 + bad.Length;
    }
}
