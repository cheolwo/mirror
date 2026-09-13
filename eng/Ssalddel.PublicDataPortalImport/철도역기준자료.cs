using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

// 공식 역사정보에서 첫 역세권 디오라마 후보 세 곳만 비공개 검토 원장에 축적한다.
// 역 중심 1km 표현 정책, 행정동 타일 결속, Unity 배치는 이 원천 수집과 분리한다.
internal static class 철도역기준자료
{
    internal const string Relative = "artifacts/local/public-data/station-reference-20260913-r1";
    internal const string SourceId = "kric-urban-rail-stations";
    internal const string DatasetId = "data-go-kr-15093755";
    internal const string DataRevision = "jungnang-line7-station-reference.r1";
    private const string MetadataUrl = "https://www.data.go.kr/catalog/15093755/fileData.json";
    private const string OfficialPageUrl = "https://www.data.go.kr/data/15093755/fileData.do";
    private const string DownloadUrl = "https://data.kric.go.kr/rips/dataset/download.file?type=filedata&id=32&operation=1";
    private const string WorkbookFile = "전체_도시철도역사정보_20260630.xlsx";
    private const string MetadataFile = "data-go-kr-15093755-metadata.json";
    private const string Quality = "OfficialSourcePendingHumanReview";
    private const string Limitation =
        "PrivateReviewOnly;NoPublicDistribution;NoOperationalAuthority;" +
        "DatasetLicenseObserved;CurrentFileVersionAlignmentPending;" +
        "SourceReportedPointNotPlatformSurvey;NoAreaCoverageInference";

    private static readonly JsonSerializerOptions PrettyJson = new() { WriteIndented = true };
    private static readonly string[] ExpectedHeaders =
    [
        "역번호", "역사명", "노선번호", "노선명", "영문역사명", "한자역사명", "환승역구분",
        "환승노선번호", "환승노선명", "역위도", "역경도", "운영기관명", "역사도로명주소",
        "역사전화번호", "데이터기준일자"
    ];

    private sealed record FileReceipt(
        string File,
        string Url,
        string Sha256,
        long Bytes,
        string ContentType,
        DateTimeOffset CollectedAtUtc);

    private sealed record Receipt(
        string SchemaVersion,
        string OfficialPage,
        string LicenseObserved,
        string ReviewStatus,
        bool PublicationAllowed,
        bool RuntimeAuthorized,
        FileReceipt[] Files);

    private sealed record StationRow(
        string StationStableId,
        string StationNumber,
        string StationName,
        string LineNumber,
        string LineName,
        string EnglishName,
        string TransferCategory,
        string TransferLineNumber,
        string TransferLineName,
        double Latitude,
        double Longitude,
        string OperatorName,
        string RoadAddress,
        DateTimeOffset DataReferenceDate);

    public static async Task RunAsync(string mode, string root, Dictionary<string, object?> result)
    {
        Require(mode is "acquire" or "self-test" or "preview" or "apply" or "verify",
            "StationReferenceModeInvalid");
        var folder = Path.Combine(root, Relative);
        if (mode == "acquire")
        {
            await AcquireAsync(folder);
            result["acquired"] = true;
            result["folder"] = Relative;
            return;
        }

        var receipt = JsonSerializer.Deserialize<Receipt>(
            await File.ReadAllTextAsync(Path.Combine(folder, "acquisition.json")))
            ?? throw new InvalidDataException("StationReferenceReceiptInvalid");
        Require(receipt.SchemaVersion == "station-reference-acquisition.r1"
                && receipt.ReviewStatus == "PendingHumanReview"
                && !receipt.PublicationAllowed
                && !receipt.RuntimeAuthorized,
            "StationReferenceReceiptBoundaryChanged");
        foreach (var file in receipt.Files)
        {
            var path = Path.Combine(folder, file.File);
            Require(File.Exists(path)
                    && new FileInfo(path).Length == file.Bytes
                    && Hash(path) == file.Sha256,
                "StationReferenceInputHashChanged");
        }

        using (var metadata = JsonDocument.Parse(
                   await File.ReadAllTextAsync(Path.Combine(folder, MetadataFile))))
        {
            Require(metadata.RootElement.GetProperty("name").GetString()
                        == "국가철도공단_도시광역철도_역사정보"
                    && metadata.RootElement.GetProperty("license").GetString()
                        == "이용허락범위 제한 없음",
                "StationReferenceMetadataChanged");
        }

        var workbookReceipt = receipt.Files.Single(item => item.File == WorkbookFile);
        var parsed = ParseWorkbook(Path.Combine(folder, WorkbookFile));
        var records = parsed.Stations
            .Select(item => ToRecord(item, workbookReceipt))
            .ToArray();
        Require(records.Length == 3
                && records.Select(item => item.RecordKey).Distinct(StringComparer.Ordinal).Count() == 3,
            "StationReferenceSelectionCountChanged");
        Require(records.All(item => item.TextValue.Length <= 2000
                                    && item.QualityCode == Quality
                                    && item.LimitationCode == Limitation),
            "StationReferenceReviewBoundaryChanged");

        result["sourceRows"] = parsed.SourceRowCount;
        result["selectedRows"] = records.Length;
        result["sourceHashSha256"] = workbookReceipt.Sha256;
        result["stationStableIds"] = records.Select(item => item.StableId).ToArray();
        await SaveAsync(Path.Combine(folder, "selected.json"), new
        {
            schemaVersion = "station-reference-selection.r1",
            sourceHashSha256 = workbookReceipt.Sha256,
            reviewStatus = "PendingHumanReview",
            publicationAllowed = false,
            runtimeAuthorized = false,
            rows = parsed.Stations
        });

        if (mode == "self-test")
        {
            var reparsed = ParseWorkbook(Path.Combine(folder, WorkbookFile));
            Require(parsed.SourceRowCount == 1099, "StationReferenceSourceCountChanged");
            Require(parsed.Stations.Select(item => item.StationStableId)
                    .SequenceEqual(
                    [
                        "station:kr:kric:s1107:0721",
                        "station:kr:kric:s1107:0722",
                        "station:kr:kric:s1107:0723"
                    ]),
                "StationReferenceStableIdChanged");
            Require(parsed.Stations.Select(item => item.StationName)
                    .SequenceEqual(["면목", "사가정", "용마산(용마폭포공원)"]),
                "StationReferenceOfficialNameChanged");
            Require(parsed.Stations.All(item => item.LineNumber == "S1107"
                                               && item.OperatorName == "서울교통공사"
                                               && item.TransferCategory == "일반역"
                                               && item.DataReferenceDate ==
                                               new DateTimeOffset(2024, 12, 31, 0, 0, 0, TimeSpan.Zero)),
                "StationReferenceSourceFieldsChanged");
            Require(parsed.Stations.Select(item => item)
                    .SequenceEqual(reparsed.Stations),
                "StationReferenceNondeterministic");
            Require(records.All(item => !item.TextValue.Contains("6311", StringComparison.Ordinal)),
                "StationReferencePhoneLeakedToProjection");
            result["selfTestsPassed"] = 8;
            await SaveAsync(Path.Combine(folder, "self-test.json"), result);
            return;
        }

        var options = await 로컬공공자료Db.OptionsAsync(root);
        var keys = records.Select(item => item.RecordKey).ToList();
        await using (var db = new PublicDataIngestionDbContext(options))
        {
            var before = await db.NormalizedRecords.AsNoTracking()
                .Where(item => keys.Contains(item.RecordKey))
                .ToListAsync();
            Require(before.All(stored => records.Any(incoming => Equivalent(stored, incoming))),
                "StationReferenceExistingConflict");
            result["beforeCount"] = before.Count;

            if (mode == "apply")
            {
                await db.Database.OpenConnectionAsync();
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT GET_LOCK('mirror:public-data:station-reference-r1',0)";
                Require(Convert.ToInt32(await command.ExecuteScalarAsync()) == 1,
                    "StationReferenceImportBusy");
                await using var transaction = await db.Database.BeginTransactionAsync();
                result["databaseWriteAttempted"] = true;
                var sourceVersion = SourceVersion(workbookReceipt);
                var registration = await new 평창군공공공간원본등록Service(db).RegisterFileAsync(
                    Path.Combine(folder, WorkbookFile),
                    new 공공공간원본등록Request(
                        SourceId,
                        DatasetId,
                        sourceVersion,
                        DataRevision,
                        new DateTimeOffset(2024, 12, 31, 0, 0, 0, TimeSpan.Zero),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        Relative + "/" + WorkbookFile));
                foreach (var record in records) record.RawSnapshotId = registration.RawSnapshotId;
                var saved = await new EfExternalDataIngestionStore(db).UpsertNormalizedAsync(records);
                Require(saved.UpdatedCount == 0, "StationReferenceUnexpectedUpdate");
                if (registration.Inserted)
                {
                    var raw = await db.RawSnapshots.SingleAsync(item => item.Id == registration.RawSnapshotId);
                    raw.CollectedAtUtc = workbookReceipt.CollectedAtUtc;
                    var run = await db.IngestionRuns.SingleAsync(item => item.Id == raw.FirstCollectionRunId);
                    run.StatusCode = 외부데이터수집StatusCodes.Partial;
                    run.FetchedCount = parsed.SourceRowCount;
                    run.NormalizedCount = records.Length;
                    run.InsertedCount = saved.InsertedCount;
                    run.ExistingCount = saved.ExistingCount;
                    run.ErrorCode = "PendingHumanReview";
                    run.ErrorSummary =
                        "Selected Jungnang Line 7 station references only. Dataset-level license observed; exact current file version alignment, spatial coverage, publication, operational authority and Unity application remain unapproved.";
                    await db.SaveChangesAsync();
                }
                await transaction.CommitAsync();
                result["committed"] = true;
                result["inserted"] = saved.InsertedCount;
                result["existing"] = saved.ExistingCount;
            }
        }

        await using var verify = new PublicDataIngestionDbContext(options);
        var storedRows = await verify.NormalizedRecords.AsNoTracking()
            .Include(item => item.RawSnapshot)
            .Where(item => keys.Contains(item.RecordKey))
            .OrderBy(item => item.StableId)
            .ToListAsync();
        if (mode != "preview") Require(storedRows.Count == 3, "StationReferenceReadbackCountMismatch");
        Require(storedRows.All(stored => records.Any(incoming => Equivalent(stored, incoming))
                                               && stored.RawSnapshot?.ContentHashSha256 == workbookReceipt.Sha256),
            "StationReferenceReadbackMismatch");
        result["target"] = "hongdal-mysql-1 / hongdal_dev";
        result["verifiedRows"] = storedRows.Count;
        result["rows"] = storedRows.Select(item => new
        {
            item.Id,
            item.RawSnapshotId,
            item.StableId,
            item.QualityCode,
            item.EvidenceAsOfUtc
        });
        await SaveAsync(Path.Combine(folder,
            mode + "-" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff") + ".json"), result);
    }

    private static async Task AcquireAsync(string folder)
    {
        Require(!Directory.Exists(folder), "StationReferenceAcquisitionAlreadyExists");
        Directory.CreateDirectory(folder);
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MirrorPublicDataResearch/1.0");
        var files = new List<FileReceipt>();

        async Task FetchAsync(string name, string url, long byteBudget)
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            Require(response.IsSuccessStatusCode,
                "StationReferenceHttpStatus:" + (int)response.StatusCode);
            await using var source = await response.Content.ReadAsStreamAsync();
            using var buffer = new MemoryStream();
            var bytes = new byte[8192];
            int count;
            while ((count = await source.ReadAsync(bytes)) > 0)
            {
                Require(buffer.Length + count <= byteBudget, "StationReferenceResponseBudgetExceeded");
                buffer.Write(bytes, 0, count);
            }
            var path = Path.Combine(folder, name);
            await File.WriteAllBytesAsync(path, buffer.ToArray());
            var collected = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            files.Add(new FileReceipt(
                name,
                url,
                Hash(path),
                buffer.Length,
                response.Content.Headers.ContentType?.ToString() ?? string.Empty,
                collected));
        }

        try
        {
            await FetchAsync(MetadataFile, MetadataUrl, 128 * 1024);
            using (var metadata = JsonDocument.Parse(
                       await File.ReadAllTextAsync(Path.Combine(folder, MetadataFile))))
            {
                Require(metadata.RootElement.GetProperty("name").GetString()
                            == "국가철도공단_도시광역철도_역사정보"
                        && metadata.RootElement.GetProperty("license").GetString()
                            == "이용허락범위 제한 없음",
                    "StationReferenceMetadataChanged");
            }
            await FetchAsync(WorkbookFile, DownloadUrl, 16 * 1024 * 1024);
            var workbookPath = Path.Combine(folder, WorkbookFile);
            var signature = await File.ReadAllBytesAsync(workbookPath);
            Require(signature.Length > 4 && signature[0] == (byte)'P' && signature[1] == (byte)'K',
                "StationReferenceWorkbookSignatureInvalid");
            _ = ParseWorkbook(workbookPath);
            var receipt = new Receipt(
                "station-reference-acquisition.r1",
                OfficialPageUrl,
                "이용허락범위 제한 없음 (공공데이터포털 dataset metadata)",
                "PendingHumanReview",
                false,
                false,
                files.ToArray());
            await SaveAsync(Path.Combine(folder, "acquisition.json"), receipt);
        }
        catch
        {
            await SaveAsync(Path.Combine(folder, "acquisition-failed.json"), new
            {
                schemaVersion = "station-reference-acquisition-failure.r1",
                files
            });
            throw;
        }
    }

    private static (int SourceRowCount, StationRow[] Stations) ParseWorkbook(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace officeRelationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace packageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";

        var workbook = ReadXml(RequiredEntry(archive, "xl/workbook.xml"));
        var firstSheet = workbook.Descendants(spreadsheet + "sheet").FirstOrDefault()
                         ?? throw new InvalidDataException("StationReferenceWorkbookSheetMissing");
        var relationshipId = (string?)firstSheet.Attribute(officeRelationships + "id")
                             ?? throw new InvalidDataException("StationReferenceWorkbookRelationshipMissing");
        var relationships = ReadXml(RequiredEntry(archive, "xl/_rels/workbook.xml.rels"));
        var target = relationships.Descendants(packageRelationships + "Relationship")
            .Single(item => (string?)item.Attribute("Id") == relationshipId)
            .Attribute("Target")?.Value
            ?? throw new InvalidDataException("StationReferenceWorksheetTargetMissing");
        var worksheetPath = target.StartsWith("/", StringComparison.Ordinal)
            ? target.TrimStart('/')
            : "xl/" + target.TrimStart('/');
        worksheetPath = worksheetPath.Replace("\\", "/", StringComparison.Ordinal);
        var worksheet = ReadXml(RequiredEntry(archive, worksheetPath));

        var sharedStringsEntry = archive.GetEntry("xl/sharedStrings.xml");
        var sharedStrings = sharedStringsEntry is null
            ? Array.Empty<string>()
            : ReadXml(sharedStringsEntry)
                .Descendants(spreadsheet + "si")
                .Select(item => string.Concat(item.Descendants(spreadsheet + "t").Select(text => text.Value)))
                .ToArray();

        var rows = new List<string[]>();
        foreach (var row in worksheet.Descendants(spreadsheet + "row"))
        {
            var values = new string[ExpectedHeaders.Length];
            foreach (var cell in row.Elements(spreadsheet + "c"))
            {
                var reference = (string?)cell.Attribute("r") ?? string.Empty;
                var index = ColumnIndex(reference);
                if (index < 0 || index >= values.Length) continue;
                var type = (string?)cell.Attribute("t") ?? string.Empty;
                var raw = cell.Element(spreadsheet + "v")?.Value ?? string.Empty;
                values[index] = type switch
                {
                    "s" => int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var sharedIndex)
                           && sharedIndex >= 0 && sharedIndex < sharedStrings.Length
                        ? sharedStrings[sharedIndex]
                        : throw new InvalidDataException("StationReferenceSharedStringInvalid"),
                    "inlineStr" => string.Concat(cell.Descendants(spreadsheet + "t").Select(text => text.Value)),
                    "e" => throw new InvalidDataException("StationReferenceCellError"),
                    _ => raw
                };
            }
            rows.Add(values.Select(value => value?.Trim() ?? string.Empty).ToArray());
        }

        Require(rows.Count > 1 && rows[0].SequenceEqual(ExpectedHeaders),
            "StationReferenceWorkbookSchemaChanged");
        var body = rows.Skip(1).Where(row => row.Any(value => value.Length > 0)).ToArray();
        var selected = body
            .Where(row => row[1] is "면목" or "사가정"
                          || row[1].StartsWith("용마산(", StringComparison.Ordinal))
            .Select(ParseStation)
            .OrderBy(item => item.StationNumber, StringComparer.Ordinal)
            .ToArray();
        Require(selected.Length == 3
                && selected.Select(item => item.StationStableId).Distinct(StringComparer.Ordinal).Count() == 3,
            "StationReferenceTargetRowsChanged");
        return (body.Length, selected);
    }

    private static StationRow ParseStation(string[] row)
    {
        Require(row.Length == ExpectedHeaders.Length
                && row[2] == "S1107"
                && row[3] == "7호선"
                && row[11] == "서울교통공사",
            "StationReferenceTargetIdentityChanged");
        var latitudeParsed = double.TryParse(
            row[9], NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude);
        var longitudeParsed = double.TryParse(
            row[10], NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude);
        Require(latitudeParsed
                && longitudeParsed
                && latitude is >= -90d and <= 90d
                && longitude is >= -180d and <= 180d,
            "StationReferenceCoordinateInvalid");
        Require(DateTime.TryParseExact(
                    row[14],
                    ["yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss"],
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var referenceDate),
            "StationReferenceDateInvalid");
        var stableId = "station:kr:kric:" + row[2].ToLowerInvariant() + ":" + row[0];
        return new StationRow(
            stableId,
            row[0],
            row[1],
            row[2],
            row[3],
            row[4],
            row[6],
            row[7],
            row[8],
            latitude,
            longitude,
            row[11],
            row[12],
            new DateTimeOffset(DateTime.SpecifyKind(referenceDate.Date, DateTimeKind.Utc)));
    }

    private static 외부데이터정규화Record ToRecord(StationRow station, FileReceipt source)
    {
        var collected = DateTimeOffset.FromUnixTimeMilliseconds(source.CollectedAtUtc.ToUnixTimeMilliseconds());
        var pointStableId = "point:kr:rail:" + station.LineNumber.ToLowerInvariant() + ":" + station.StationNumber;
        var dimension = "line=" + station.LineNumber + ";station=" + station.StationNumber;
        var text = JsonSerializer.Serialize(new
        {
            station.StationStableId,
            station.StationNumber,
            station.StationName,
            station.LineNumber,
            station.LineName,
            station.EnglishName,
            station.TransferCategory,
            station.TransferLineNumber,
            station.TransferLineName,
            station.Latitude,
            station.Longitude,
            station.OperatorName,
            station.RoadAddress,
            dataReferenceDate = station.DataReferenceDate.ToString("yyyy-MM-dd")
        });
        return new 외부데이터정규화Record
        {
            RecordKey = 외부데이터RecordKey.Create(
                SourceId,
                DatasetId,
                pointStableId,
                "station-reference",
                station.DataReferenceDate,
                dimension),
            StableId = station.StationStableId,
            SourceId = SourceId,
            DatasetId = DatasetId,
            RegionStableId = pointStableId,
            MetricCode = "station-reference",
            NumericValue = null,
            TextValue = text,
            UnitCode = "station-reference-json",
            EvidenceAsOfUtc = station.DataReferenceDate,
            CollectedAtUtc = collected,
            SpatialPrecisionCode = "source-reported-wgs84-point",
            TemporalPrecisionCode = "source-date",
            QualityCode = Quality,
            LimitationCode = Limitation,
            DimensionKey = dimension,
            SourceVersion = SourceVersion(source),
            DataRevision = DataRevision,
            FirstSeenAtUtc = collected,
            LastSeenAtUtc = collected
        };
    }

    private static string SourceVersion(FileReceipt source)
        => "file:20260630;target-row-reference:2024-12-31;sha256:" + source.Sha256;

    private static bool Equivalent(외부데이터정규화Record stored, 외부데이터정규화Record incoming)
        => stored.RecordKey == incoming.RecordKey
           && stored.StableId == incoming.StableId
           && stored.SourceId == incoming.SourceId
           && stored.DatasetId == incoming.DatasetId
           && stored.RegionStableId == incoming.RegionStableId
           && stored.MetricCode == incoming.MetricCode
           && stored.NumericValue == incoming.NumericValue
           && stored.TextValue == incoming.TextValue
           && stored.UnitCode == incoming.UnitCode
           && stored.EvidenceAsOfUtc == incoming.EvidenceAsOfUtc
           && stored.CollectedAtUtc == incoming.CollectedAtUtc
           && stored.SpatialPrecisionCode == incoming.SpatialPrecisionCode
           && stored.TemporalPrecisionCode == incoming.TemporalPrecisionCode
           && stored.QualityCode == incoming.QualityCode
           && stored.LimitationCode == incoming.LimitationCode
           && stored.DimensionKey == incoming.DimensionKey
           && stored.SourceVersion == incoming.SourceVersion
           && stored.DataRevision == incoming.DataRevision;

    private static XDocument ReadXml(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        return XDocument.Load(stream, LoadOptions.None);
    }

    private static ZipArchiveEntry RequiredEntry(ZipArchive archive, string path)
        => archive.GetEntry(path)
           ?? throw new InvalidDataException("StationReferenceWorkbookPartMissing:" + path);

    private static int ColumnIndex(string reference)
    {
        var index = 0;
        var found = false;
        foreach (var character in reference)
        {
            if (character is < 'A' or > 'Z') break;
            found = true;
            index = index * 26 + character - 'A' + 1;
        }
        return found ? index - 1 : -1;
    }

    private static string Hash(string path)
        => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static Task SaveAsync(string path, object value)
        => File.WriteAllTextAsync(path,
            JsonSerializer.Serialize(value, PrettyJson),
            new UTF8Encoding(false));

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}
