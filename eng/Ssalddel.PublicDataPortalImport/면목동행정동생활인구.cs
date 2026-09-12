using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

// 서울시가 행정동 단위로 사전 집계한 생활인구를 비공개 검토 원장에 보존한다.
// 현행 경계에 재투영한 값이 아니므로 2016 행정동 구역 판본을 현재 경계 통계로 승격하지 않는다.
internal static class 면목동행정동생활인구
{
    private const string Relative = "artifacts/local/public-data/myeonmok-administrative-data-20260912-r1";
    private const string FileName = "LOCAL_PEOPLE_DONG_202607.zip";
    private const string CsvName = "LOCAL_PEOPLE_DONG_202607.csv";
    private const string Source = "seoul-open-data";
    private const string Dataset = "oa-14991-administrative-dong-local-population";
    private const string Revision = "myeonmok-admin-dong-living-population-20260912.r1";
    private const string SourceHash = "c2dbc97bb2d1018d27cede7d47a7b49eedd3a8f6abdd268616fffecbeae3f7bc";
    private const long SourceBytes = 45_045_885;
    private const int ExpectedRowsPerArea = 31 * 24;
    private const string Metric = "living-population.total";
    private const string Quality = "OfficialStatisticalEstimatePendingReview";
    private const string Limitation = "PrivateReviewOnly;StatisticalEstimate;2016AdministrativeAreaVintage;NotCurrentBoundaryReaggregation;SmallCellsMayBeMasked;NotGameplayAuthority";
    private const string SourcePage = "https://data.seoul.go.kr/dataList/OA-14991/S/1/datasetView.do";
    private const string DownloadUrl = "https://datafile.seoul.go.kr/bigfile/iot/inf/nio_download.do?useCache=false";
    private static readonly DateTimeOffset MonthStart = new(2026, 7, 1, 0, 0, 0, TimeSpan.FromHours(9));
    private static readonly DateTimeOffset MonthEnd = new(2026, 7, 31, 23, 0, 0, TimeSpan.FromHours(9));
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly IReadOnlyDictionary<string, Area> Areas = new Dictionary<string, Area>(StringComparer.Ordinal)
    {
        ["11260520"] = new("1126052000", "면목제2동"),
        ["11260540"] = new("1126054000", "면목제4동"),
        ["11260550"] = new("1126055000", "면목제5동"),
        ["11260565"] = new("1126056500", "면목본동"),
        ["11260570"] = new("1126057000", "면목제7동"),
        ["11260575"] = new("1126057500", "면목제3·8동"),
    };

    private sealed record Area(string Code10, string Name)
    {
        public string StableId => "region:kr:hjd:" + Code10;
    }

    private sealed record Observation(Area Area, string SourceCode, DateOnly Date, int Hour, decimal Value)
    {
        public DateTimeOffset EvidenceAt => new(Date.Year, Date.Month, Date.Day, Hour, 0, 0, TimeSpan.FromHours(9));
    }

    private sealed record AreaAnalysis(
        string AdministrativeAreaStableId,
        string DisplayName,
        int ObservationCount,
        decimal MonthlyHourlyAverage,
        decimal NightHourlyAverage,
        decimal DaytimeHourlyAverage,
        decimal WeekdayHourlyAverage,
        decimal WeekendHourlyAverage,
        int PeakHour,
        decimal PeakHourAverage,
        int TroughHour,
        decimal TroughHourAverage);

    internal static async Task RunAsync(string mode, string root, Dictionary<string, object?> result)
    {
        Require(mode is "acquire" or "preview" or "apply" or "verify" or "self-test", "AdministrativeDongDataModeInvalid");
        var folder = Path.Combine(root, Relative);
        var zipPath = Path.Combine(folder, FileName);
        result["sourcePage"] = SourcePage;
        result["sourceDataset"] = "OA-14991";
        result["sourceFile"] = FileName;
        result["sourceVintage"] = "2026-07";
        result["sourceSpatialVintage"] = "2016-administrative-dong-code-area";
        result["license"] = "KOGL-Type1";
        result["publicationApproved"] = false;
        result["gameplayAuthority"] = false;

        if (mode == "self-test")
        {
            result["selfTestsPassed"] = SelfTest();
            return;
        }
        if (mode == "acquire")
        {
            await AcquireAsync(folder, zipPath);
            result["acquisitionStatus"] = "ExactOfficialFilePresent";
            result["bytes"] = new FileInfo(zipPath).Length;
            result["sha256"] = HashFile(zipPath);
            return;
        }

        ValidateSource(zipPath, root);
        var collectedAt = DateTimeOffset.UtcNow;
        var observations = Read(zipPath).ToArray();
        ValidateCoverage(observations);
        var records = observations.Select(item => ToRecord(item, collectedAt)).ToArray();
        var analyses = Analyze(observations);
        result["selectedRows"] = records.Length;
        result["administrativeAreaCount"] = analyses.Length;
        result["coverage"] = analyses;
        result["missingAreas"] = Array.Empty<string>();
        result["limitations"] = Limitation.Split(';');

        var options = await 로컬공공자료Db.OptionsAsync(root);
        await using (var db = new PublicDataIngestionDbContext(options))
        {
            result["stage"] = "ValidateAdministrativeCodeLedger";
            await ValidateAdministrativeCodeLedgerAsync(db);
            result["stage"] = "ReadExistingLivingPopulation";
            var before = await ReadStoredAsync(db);
            result["beforeCount"] = before.Length;
            Require(before.All(stored => records.Any(candidate => Equivalent(stored, candidate))), "ExistingLivingPopulationConflict");
            if (mode == "apply")
            {
                await db.Database.OpenConnectionAsync();
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT GET_LOCK('mirror:public-data:myeonmok-admin-dong-living-population',0)";
                Require(Convert.ToInt32(await command.ExecuteScalarAsync()) == 1, "ImportBusy");
                await using var transaction = await db.Database.BeginTransactionAsync();
                result["databaseWriteAttempted"] = true;
                before = await ReadStoredAsync(db);
                Require(before.All(stored => records.Any(candidate => Equivalent(stored, candidate))), "ExistingLivingPopulationConflictAfterLock");
                var registration = await new 평창군공공공간원본등록Service(db).RegisterFileAsync(
                    zipPath,
                    new(Source, Dataset, "OA-14991:2026-07:sha256:" + SourceHash, Revision, MonthEnd, "application/zip", Relative + "/" + FileName));
                foreach (var record in records) record.RawSnapshotId = registration.RawSnapshotId;
                var inserted = 0;
                var existing = 0;
                for (var start = 0; start < records.Length; start += 250)
                {
                    var saved = await new EfExternalDataIngestionStore(db).UpsertNormalizedAsync(records.Skip(start).Take(250).ToArray());
                    Require(saved.UpdatedCount == 0, "UnexpectedLivingPopulationUpdate");
                    inserted += saved.InsertedCount;
                    existing += saved.ExistingCount;
                }
                if (registration.Inserted)
                {
                    var raw = await db.RawSnapshots.SingleAsync(item => item.Id == registration.RawSnapshotId);
                    var run = await db.IngestionRuns.SingleAsync(item => item.Id == raw.FirstCollectionRunId);
                    run.StatusCode = 외부데이터수집StatusCodes.Partial;
                    run.FetchedCount = observations.Length;
                    run.NormalizedCount = records.Length;
                    run.InsertedCount = inserted;
                    run.ExistingCount = existing;
                    run.ErrorCode = "PendingHumanReview";
                    run.ErrorSummary = "Official statistical estimate using 2016 administrative-dong code areas. Private review only; not current-boundary reaggregation or gameplay authority.";
                    await db.SaveChangesAsync();
                }
                await transaction.CommitAsync();
                result["committed"] = true;
                result["inserted"] = inserted;
                result["existing"] = existing;
                result["rawSnapshotInserted"] = registration.Inserted;
                result["rawSnapshotId"] = registration.RawSnapshotId;
            }
        }

        result["stage"] = "IndependentReadback";
        await using var verify = new PublicDataIngestionDbContext(options);
        var storedRows = await ReadStoredAsync(verify);
        if (mode != "preview") Require(storedRows.Length == records.Length, "LivingPopulationReadbackCountMismatch");
        Require(storedRows.All(stored => records.Any(candidate => Equivalent(stored, candidate))), "LivingPopulationReadbackMismatch");
        if (storedRows.Length > 0)
        {
            var rawIds = storedRows.Select(item => item.RawSnapshotId).Distinct().ToArray();
            Require(rawIds.Length == 1, "LivingPopulationRawSnapshotMismatch");
            var raw = await verify.RawSnapshots.AsNoTracking().SingleAsync(item => item.Id == rawIds[0]);
            Require(raw.ContentHashSha256 == SourceHash && raw.ContentLength == SourceBytes, "LivingPopulationRawEvidenceMismatch");
            var storedAnalyses = Analyze(storedRows.Select(item => FromRecord(item)).ToArray());
            Require(JsonSerializer.Serialize(storedAnalyses) == JsonSerializer.Serialize(analyses), "LivingPopulationAnalysisReadbackMismatch");
        }
        result["verifiedRows"] = storedRows.Length;
        result["target"] = "hongdal-mysql-1 / hongdal_dev";
        await SaveResultAsync(folder, mode, result);
    }

    private static async Task AcquireAsync(string folder, string zipPath)
    {
        Directory.CreateDirectory(folder);
        if (File.Exists(zipPath))
        {
            ValidateSource(zipPath, Path.GetFullPath(Path.Combine(folder, "..", "..", "..", "..")));
            return;
        }
        var temporary = zipPath + ".download";
        Require(!File.Exists(temporary), "IncompleteDownloadAlreadyExists");
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["infId"] = "OA-14991",
            ["seq"] = "2607",
            ["infSeq"] = "3",
        });
        using var response = await client.PostAsync(DownloadUrl, form);
        response.EnsureSuccessStatusCode();
        await using (var source = await response.Content.ReadAsStreamAsync())
        await using (var destination = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            var buffer = new byte[128 * 1024];
            long total = 0;
            int read;
            while ((read = await source.ReadAsync(buffer)) > 0)
            {
                total += read;
                Require(total <= 50L * 1024 * 1024, "LivingPopulationDownloadBudgetExceeded");
                await destination.WriteAsync(buffer.AsMemory(0, read));
            }
        }
        Require(new FileInfo(temporary).Length == SourceBytes && HashFile(temporary) == SourceHash, "LivingPopulationDownloadChanged");
        File.Move(temporary, zipPath);
    }

    private static IEnumerable<Observation> Read(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        Require(zip.Entries.Count == 1 && zip.Entries[0].FullName == CsvName && zip.Entries[0].Length is > 100_000_000 and < 120_000_000, "LivingPopulationArchiveLayoutChanged");
        using var stream = zip.Entries[0].Open();
        using var parser = new TextFieldParser(stream, Encoding.UTF8, true) { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false };
        parser.SetDelimiters(",");
        var headers = parser.ReadFields() ?? throw new InvalidDataException("LivingPopulationCsvHeadersMissing");
        Require(headers.Length >= 4 && headers[0] == "기준일ID" && headers[1] == "시간대구분" && headers[2] == "행정동코드" && headers[3] == "총생활인구수", "LivingPopulationCsvHeadersChanged");
        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields() ?? throw new InvalidDataException("LivingPopulationCsvRowMissing");
            // 2026-07 원본은 문서화된 32개 열 뒤에 값 없는 열 하나를 모든 행에 붙인다.
            // 값이 생기거나 열 수가 달라지면 공급자 스키마 변화로 중단한다.
            Require(fields.Length == headers.Length + 1 && fields[^1].Length == 0, "LivingPopulationCsvColumnCountChanged");
            if (!Areas.TryGetValue(fields[2], out var area)) continue;
            Require(DateOnly.TryParseExact(fields[0], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) && date.Year == 2026 && date.Month == 7, "LivingPopulationDateInvalid");
            Require(int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var hour) && hour is >= 0 and <= 23, "LivingPopulationHourInvalid");
            Require(decimal.TryParse(fields[3], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) && value >= 0, "LivingPopulationValueInvalid");
            yield return new Observation(area, fields[2], date, hour, value);
        }
    }

    private static 외부데이터정규화Record ToRecord(Observation item, DateTimeOffset collectedAt)
    {
        var dimension = "source-hjd-code=" + item.SourceCode + ";statistic=total";
        var evidence = item.EvidenceAt;
        return new 외부데이터정규화Record
        {
            RecordKey = 외부데이터RecordKey.Create(Source, Dataset, item.Area.StableId, Metric, evidence, dimension),
            StableId = $"metric:kr:hjd:{item.Area.Code10}:living-population:{item.Date:yyyyMMdd}:{item.Hour:00}",
            SourceId = Source,
            DatasetId = Dataset,
            RegionStableId = item.Area.StableId,
            MetricCode = Metric,
            NumericValue = item.Value,
            TextValue = string.Empty,
            UnitCode = "estimated-person",
            EvidenceAsOfUtc = evidence,
            CollectedAtUtc = collectedAt,
            SpatialPrecisionCode = "administrative-dong-2016-code-area",
            TemporalPrecisionCode = "hour",
            QualityCode = Quality,
            LimitationCode = Limitation,
            DimensionKey = dimension,
            SourceVersion = "OA-14991:2026-07:sha256:" + SourceHash,
            DataRevision = Revision,
            FirstSeenAtUtc = collectedAt,
            LastSeenAtUtc = collectedAt,
        };
    }

    private static Observation FromRecord(외부데이터정규화Record item)
    {
        var area = Areas.Values.Single(area => area.StableId == item.RegionStableId);
        var sourceCode = item.DimensionKey.Split(';', StringSplitOptions.RemoveEmptyEntries)[0]["source-hjd-code=".Length..];
        var local = item.EvidenceAsOfUtc.ToOffset(TimeSpan.FromHours(9));
        return new Observation(area, sourceCode, DateOnly.FromDateTime(local.DateTime), local.Hour, item.NumericValue ?? throw new InvalidDataException("LivingPopulationStoredValueMissing"));
    }

    private static AreaAnalysis[] Analyze(IReadOnlyCollection<Observation> observations)
        => observations.GroupBy(item => item.Area.StableId, StringComparer.Ordinal).Select(group =>
        {
            var ordered = group.OrderBy(item => item.Date).ThenBy(item => item.Hour).ToArray();
            var hourAverages = ordered.GroupBy(item => item.Hour).ToDictionary(hour => hour.Key, hour => Average(hour.Select(item => item.Value)));
            var peak = hourAverages.OrderByDescending(item => item.Value).ThenBy(item => item.Key).First();
            var trough = hourAverages.OrderBy(item => item.Value).ThenBy(item => item.Key).First();
            return new AreaAnalysis(
                group.Key,
                ordered[0].Area.Name,
                ordered.Length,
                Average(ordered.Select(item => item.Value)),
                Average(ordered.Where(item => item.Hour <= 5).Select(item => item.Value)),
                Average(ordered.Where(item => item.Hour is >= 9 and <= 17).Select(item => item.Value)),
                Average(ordered.Where(item => item.Date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).Select(item => item.Value)),
                Average(ordered.Where(item => item.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday).Select(item => item.Value)),
                peak.Key,
                peak.Value,
                trough.Key,
                trough.Value);
        }).OrderBy(item => item.AdministrativeAreaStableId, StringComparer.Ordinal).ToArray();

    private static decimal Average(IEnumerable<decimal> values)
        => decimal.Round(values.Average(), 4, MidpointRounding.AwayFromZero);

    private static void ValidateCoverage(IReadOnlyCollection<Observation> observations)
    {
        Require(observations.Count == Areas.Count * ExpectedRowsPerArea, "LivingPopulationCoverageCountMismatch");
        foreach (var area in Areas.Values)
        {
            var rows = observations.Where(item => item.Area == area).ToArray();
            Require(rows.Length == ExpectedRowsPerArea, "LivingPopulationAreaIncomplete:" + area.Code10);
            Require(rows.Select(item => (item.Date, item.Hour)).Distinct().Count() == ExpectedRowsPerArea, "LivingPopulationAreaDuplicateHour:" + area.Code10);
            Require(rows.Min(item => item.EvidenceAt) == MonthStart && rows.Max(item => item.EvidenceAt) == MonthEnd, "LivingPopulationAreaDateRangeMismatch:" + area.Code10);
        }
    }

    private static async Task ValidateAdministrativeCodeLedgerAsync(PublicDataIngestionDbContext db)
    {
        var stableIds = Areas.Values.Select(item => item.StableId).ToList();
        var rows = await db.NormalizedRecords.AsNoTracking()
            .Where(item => stableIds.Contains(item.RegionStableId) && item.MetricCode == "geography.administrative-agency.name")
            .ToArrayAsync();
        foreach (var area in Areas.Values)
        {
            var areaRows = rows.Where(item => item.RegionStableId == area.StableId).ToArray();
            Require(areaRows.Length > 0
                    && areaRows.All(item => item.TextValue.Replace("서울특별시 중랑구 ", string.Empty, StringComparison.Ordinal).Replace('.', '·') == area.Name)
                    && areaRows.Any(item => item.SourceVersion.StartsWith("mois-jscode:20260301:", StringComparison.Ordinal)),
                "AdministrativeCodeLedgerMismatch:" + area.Code10);
        }
    }

    private static Task<외부데이터정규화Record[]> ReadStoredAsync(PublicDataIngestionDbContext db)
    {
        var stableIds = Areas.Values.Select(item => item.StableId).ToList();
        return db.NormalizedRecords.AsNoTracking()
            .Where(item => item.SourceId == Source && item.DatasetId == Dataset && item.MetricCode == Metric
                           && stableIds.Contains(item.RegionStableId)
                           && item.EvidenceAsOfUtc >= MonthStart && item.EvidenceAsOfUtc <= MonthEnd)
            .OrderBy(item => item.RegionStableId).ThenBy(item => item.EvidenceAsOfUtc)
            .ToArrayAsync();
    }

    private static bool Equivalent(외부데이터정규화Record stored, 외부데이터정규화Record incoming)
        => stored.RecordKey == incoming.RecordKey && stored.StableId == incoming.StableId
           && stored.SourceId == incoming.SourceId && stored.DatasetId == incoming.DatasetId
           && stored.RegionStableId == incoming.RegionStableId && stored.MetricCode == incoming.MetricCode
           && stored.NumericValue == incoming.NumericValue && stored.TextValue == incoming.TextValue
           && stored.UnitCode == incoming.UnitCode && stored.EvidenceAsOfUtc == incoming.EvidenceAsOfUtc
           && stored.SpatialPrecisionCode == incoming.SpatialPrecisionCode && stored.TemporalPrecisionCode == incoming.TemporalPrecisionCode
           && stored.QualityCode == incoming.QualityCode && stored.LimitationCode == incoming.LimitationCode
           && stored.DimensionKey == incoming.DimensionKey && stored.SourceVersion == incoming.SourceVersion
           && stored.DataRevision == incoming.DataRevision;

    private static void ValidateSource(string path, string root)
    {
        var full = Path.GetFullPath(path);
        var safeRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Require(full.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(full), "LivingPopulationSourcePathInvalid");
        Require(new FileInfo(full).Length == SourceBytes && HashFile(full) == SourceHash, "LivingPopulationSourceChanged");
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static async Task SaveResultAsync(string folder, string mode, object value)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"{mode}-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffffff}.json");
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, value, Json);
    }

    private static int SelfTest()
    {
        var area = Areas["11260575"];
        var observations = Enumerable.Range(0, 7).SelectMany(day => Enumerable.Range(0, 24)
            .Select(hour => new Observation(area, "11260575", new DateOnly(2026, 7, 1).AddDays(day), hour, 100 + hour))).ToArray();
        var analysis = Analyze(observations).Single();
        var now = new DateTimeOffset(2026, 9, 12, 0, 0, 0, TimeSpan.Zero);
        var record = ToRecord(observations[0], now);
        var tests = 0;
        void Check(bool condition) { Require(condition, "LivingPopulationSelfTest:" + (tests + 1)); tests++; }
        Check(record.RegionStableId == area.StableId);
        Check(record.EvidenceAsOfUtc.Offset == TimeSpan.FromHours(9));
        Check(record.NumericValue == 100);
        Check(record.RecordKey == ToRecord(observations[0], now.AddDays(1)).RecordKey);
        Check(record.QualityCode == Quality && record.LimitationCode.Contains("NotCurrentBoundaryReaggregation", StringComparison.Ordinal));
        Check(analysis.PeakHour == 23 && analysis.TroughHour == 0);
        Check(analysis.NightHourlyAverage == 102.5m);
        Check(FromRecord(record) == observations[0]);
        Check(Areas.Count == 6 && Areas.Values.Select(item => item.StableId).Distinct().Count() == 6);
        return tests;
    }

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}
