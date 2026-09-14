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

internal static class 사가정횡단보도자료
{
    private const string RelativeFolder = "artifacts/local/public-data/sagajeong-static-traffic-20260913-r2-crosswalk";
    private const string ReceiptFileName = "acquisition.json";
    private const string FileName = "서울시 교차로 및 횡단보도 시설·위치정보_20260824.xlsx";
    private const string SourceId = "seoul-open-data-crosswalk";
    private const string DatasetId = "oa-23081-crosswalk-location";
    private const string InfId = "OA-23081";
    private const string DatasetTitle = "서울시 교차로 및 횡단보도 시설·위치정보";
    private const string OfficialPageUrl = "https://data.seoul.go.kr/dataList/OA-23081/F/1/datasetView.do";
    private const string WorksheetName = "교차로 및 횡단보도 시설 위치정보";
    private const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string DataRevision = "sagajeong-static-traffic-20260913.r2-crosswalk";
    private const string StationStableId = "station:kr:kric:s1107:0722";
    private const string WorldRegionStableId = "world-region:kr:seoul:jungnang:sagajeong.r1";
    private const string DbRegionStableId = "area:kr:seoul:jungnang:sagajeong-station-0722-one-kilometer";
    private const string Quality = "PendingHumanReview";
    private const string SelectionMethod = "OfficialXlsxThenEpsg5186PointWithinFrozenSagajeongEnvelope";
    private const string Limitation = "PrivateReviewOnly;NoPublication;NoRuntime;NoTraversalAuthority;NoGameplayAuthority;CrosswalkPointOnly;NoStopLineGeometry;NoSignalPhaseOrTiming";
    private const string ExpectedHash = "1A5DB9EA7A1CD58E2D7F2B4246BAF099A3E4D5278F2867A1B87F3D50D21541BE";
    private const long ExpectedLength = 1_735_662;
    private const int ExpectedTotalRows = 21_776;
    private const int ExpectedSelectedRows = 84;
    private static readonly DateTimeOffset FileModifiedDate = new(2026, 8, 24, 0, 0, 0, TimeSpan.Zero);
    private static readonly Bounds Scope = new(207286.783d, 552964.590d, 208321.206d, 553986.657d);

    private static readonly string[] ExpectedHeaders =
    [
        "연번", "자치구", "횡단보도관리번호", "교차로관리번호", "횡단보도종류", "보행등유무", "교차로명", "X좌표", "Y좌표"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static async Task RunAsync(string mode, string root, Dictionary<string, object?> result)
    {
        if (mode == "acquire")
        {
            await AcquireAsync(root, result);
            return;
        }

        Require(mode is "self-test" or "preview" or "apply" or "verify", "SagajeongCrosswalkModeInvalid");
        var frozen = LoadFrozen(root);
        var parsed = Parse(frozen);
        SetSummary(result, frozen, parsed);

        if (mode == "self-test")
        {
            result["selfTestsPassed"] = SelfTest(frozen, parsed);
            return;
        }

        var options = await 로컬공공자료Db.OptionsAsync(root);
        if (mode == "preview")
        {
            await using var db = new PublicDataIngestionDbContext(options);
            var existing = await ReadExistingAsync(db, parsed.Records.Select(item => item.RecordKey).ToArray());
            Require(existing.All(item => parsed.Records.Any(candidate => Equivalent(item, candidate))),
                "SagajeongCrosswalkExistingRecordConflict");
            result["beforeCount"] = existing.Count;
            return;
        }

        if (mode == "apply")
            await ApplyAsync(options, frozen, parsed, result);

        await VerifyReadbackAsync(options, frozen, parsed, result);
    }

    // 공식 포털에서 내려받은 원본 XLSX가 이미 있는 상태에서 hash·길이·계약을
    // 확인하고 비공개 동결 영수증만 만든다. 원본을 수정하거나 다른 위치로 복사하지 않는다.
    private static async Task AcquireAsync(string root, Dictionary<string, object?> result)
    {
        var folder = Path.Combine(root, RelativeFolder);
        var path = Path.Combine(folder, FileName);
        Require(Directory.Exists(folder) && File.Exists(path), "SagajeongCrosswalkOfficialXlsxMissing");
        ValidateRawFile(path);

        var receiptPath = Path.Combine(folder, ReceiptFileName);
        if (!File.Exists(receiptPath))
        {
            var acquiredAt = RoundToMilliseconds(new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero));
            var receipt = new AcquisitionReceipt(
                "sagajeong-crosswalk-acquisition.r2",
                acquiredAt,
                StationStableId,
                WorldRegionStableId,
                DbRegionStableId,
                new Wgs84Bounds(127.0825d, 37.5762d, 127.0942d, 37.5854d),
                Scope,
                SelectionMethod,
                false,
                false,
                false,
                false,
                Quality,
                new SourceReceipt(
                    InfId,
                    DatasetTitle,
                    OfficialPageUrl,
                    FileName,
                    FileModifiedDate,
                    "EPSG:5186",
                    ExpectedLength,
                    ExpectedHash,
                    "1",
                    "1",
                    "KOGL-Type1-Attribution",
                    WorksheetName,
                    ExpectedTotalRows,
                    ExpectedSelectedRows,
                    "CrosswalkPointAndPedestrianSignalPresenceOnly;NoStopLineGeometryOrSignalPhaseTiming"));
            await File.WriteAllTextAsync(
                receiptPath,
                JsonSerializer.Serialize(receipt, JsonOptions) + Environment.NewLine,
                new UTF8Encoding(false));
            result["receiptCreated"] = true;
        }
        else
        {
            _ = LoadFrozen(root);
            result["reusedFrozenAcquisition"] = true;
        }

        result["sourceFiles"] = 1;
        result["downloadedBytes"] = ExpectedLength;
        result["folder"] = RelativeFolder;
        result["reviewStatus"] = Quality;
    }

    private static FrozenAcquisition LoadFrozen(string root)
    {
        var folder = Path.Combine(root, RelativeFolder);
        var path = Path.Combine(folder, FileName);
        var receiptPath = Path.Combine(folder, ReceiptFileName);
        Require(File.Exists(receiptPath), "SagajeongCrosswalkReceiptMissing");
        var receipt = JsonSerializer.Deserialize<AcquisitionReceipt>(File.ReadAllText(receiptPath), JsonOptions)
            ?? throw new InvalidDataException("SagajeongCrosswalkReceiptInvalid");
        Require(receipt.SchemaVersion == "sagajeong-crosswalk-acquisition.r2"
                && receipt.StationStableId == StationStableId
                && receipt.WorldRegionStableId == WorldRegionStableId
                && receipt.DbRegionStableId == DbRegionStableId
                && receipt.Wgs84Bounds == new Wgs84Bounds(127.0825d, 37.5762d, 127.0942d, 37.5854d)
                && receipt.Epsg5186Bounds == Scope
                && receipt.SelectionMethod == SelectionMethod
                && !receipt.DistributionApproved
                && !receipt.RuntimeAuthorized
                && !receipt.TraversalAuthorized
                && !receipt.GameplayAuthorized
                && receipt.ReviewStatus == Quality,
            "SagajeongCrosswalkReceiptContractChanged");
        var source = receipt.Source;
        Require(source.InfId == InfId
                && source.DatasetTitle == DatasetTitle
                && source.OfficialPageUrl == OfficialPageUrl
                && source.FileName == FileName
                && source.FileModifiedDate == FileModifiedDate
                && source.CoordinateReferenceSystem == "EPSG:5186"
                && source.ContentLength == ExpectedLength
                && source.Sha256.Equals(ExpectedHash, StringComparison.OrdinalIgnoreCase)
                && source.DownloadSequence == "1"
                && source.InformationSequence == "1"
                && source.LicenseCode == "KOGL-Type1-Attribution"
                && source.WorksheetName == WorksheetName
                && source.TotalRows == ExpectedTotalRows
                && source.SelectedRows == ExpectedSelectedRows,
            "SagajeongCrosswalkReceiptSourceChanged");
        ValidateRawFile(path);
        return new FrozenAcquisition(folder, path, receipt);
    }

    private static void ValidateRawFile(string path)
    {
        Require(File.Exists(path) && new FileInfo(path).Length == ExpectedLength,
            "SagajeongCrosswalkRawFileMissingOrLengthChanged");
        Require(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))
                .Equals(ExpectedHash, StringComparison.OrdinalIgnoreCase),
            "SagajeongCrosswalkRawHashChanged");
    }

    private static ParsedData Parse(FrozenAcquisition frozen)
    {
        using var archive = ZipFile.OpenRead(frozen.Path);
        var workbook = LoadXml(RequiredEntry(archive, "xl/workbook.xml"));
        var relationships = LoadXml(RequiredEntry(archive, "xl/_rels/workbook.xml.rels"));
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace documentRelationship = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace packageRelationship = "http://schemas.openxmlformats.org/package/2006/relationships";
        var sheets = workbook.Root?.Element(spreadsheet + "sheets")?.Elements(spreadsheet + "sheet").ToArray()
            ?? throw new InvalidDataException("SagajeongCrosswalkWorkbookSheetsMissing");
        Require(sheets.Length == 1 && (string?)sheets[0].Attribute("name") == WorksheetName,
            "SagajeongCrosswalkWorksheetChanged");
        var relationshipId = (string?)sheets[0].Attribute(documentRelationship + "id");
        Require(!string.IsNullOrWhiteSpace(relationshipId), "SagajeongCrosswalkWorksheetRelationshipMissing");
        var target = relationships.Root?.Elements(packageRelationship + "Relationship")
            .SingleOrDefault(item => (string?)item.Attribute("Id") == relationshipId)?.Attribute("Target")?.Value
            ?? throw new InvalidDataException("SagajeongCrosswalkWorksheetTargetMissing");
        Require(!string.IsNullOrWhiteSpace(target), "SagajeongCrosswalkWorksheetTargetEmpty");
        var worksheetPath = "xl/" + target.Replace('\\', '/').TrimStart('/');
        var worksheet = LoadXml(RequiredEntry(archive, worksheetPath));
        var sharedStrings = ReadSharedStrings(archive, spreadsheet);
        var rows = worksheet.Descendants(spreadsheet + "sheetData").Elements(spreadsheet + "row").ToArray();
        Require(rows.Length == ExpectedTotalRows + 1, "SagajeongCrosswalkWorksheetRowCountChanged");

        var headers = ReadRow(rows[0], spreadsheet, sharedStrings);
        Require(headers.SequenceEqual(ExpectedHeaders, StringComparer.Ordinal),
            "SagajeongCrosswalkHeadersChanged");
        var candidates = new List<Candidate>();
        var coordinateMissingRows = 0;
        var previousSheetRow = 1;
        for (var index = 1; index < rows.Length; index++)
        {
            var sheetRow = RequiredRowNumber(rows[index]);
            Require(sheetRow > previousSheetRow, "SagajeongCrosswalkRowOrderInvalid");
            previousSheetRow = sheetRow;
            var values = ReadRow(rows[index], spreadsheet, sharedStrings);
            Require(values.Length == ExpectedHeaders.Length, "SagajeongCrosswalkRowWidthChanged");
            Require(!string.IsNullOrWhiteSpace(values[0])
                    && !string.IsNullOrWhiteSpace(values[1])
                    && !string.IsNullOrWhiteSpace(values[2])
                    && !string.IsNullOrWhiteSpace(values[4])
                    && values[5] is "유" or "무",
                "SagajeongCrosswalkRequiredValueMissing");
            if (values[7].Length == 0 && values[8].Length == 0)
            {
                coordinateMissingRows++;
                continue;
            }
            var x = RequiredDouble(values[7], "SagajeongCrosswalkXInvalid");
            var y = RequiredDouble(values[8], "SagajeongCrosswalkYInvalid");
            if (!Scope.Contains(x, y)) continue;
            candidates.Add(new Candidate(
                sheetRow,
                values[0],
                values[1],
                values[2],
                EmptyToNull(values[3]),
                values[4],
                values[5] == "유",
                EmptyToNull(values[6]),
                x,
                y));
        }

        Require(candidates.Count == ExpectedSelectedRows, "SagajeongCrosswalkSelectedRowCountChanged");
        var records = candidates.Select(item => ToRecord(item, frozen.Receipt))
            .OrderBy(item => item.StableId, StringComparer.Ordinal)
            .ThenBy(item => item.DimensionKey, StringComparer.Ordinal)
            .ToArray();
        Require(records.Select(item => item.RecordKey).Distinct(StringComparer.Ordinal).Count() == records.Length,
            "SagajeongCrosswalkRecordIdentityCollision");
        return new ParsedData(ExpectedTotalRows, coordinateMissingRows, candidates, records);
    }

    private static 외부데이터정규화Record ToRecord(Candidate candidate, AcquisitionReceipt receipt)
    {
        var intersection = candidate.IntersectionManagementNumber ?? "none";
        var dimension = $"crosswalk={candidate.CrosswalkManagementNumber};intersection={intersection}";
        var text = JsonSerializer.Serialize(new
        {
            sourceSheetRow = candidate.SourceSheetRow,
            sourceSerialNumber = candidate.SourceSerialNumber,
            district = candidate.District,
            crosswalkManagementNumber = candidate.CrosswalkManagementNumber,
            candidate.IntersectionManagementNumber,
            candidate.CrosswalkType,
            candidate.PedestrianSignalPresent,
            candidate.IntersectionName,
            pointEpsg5186 = new[] { candidate.X, candidate.Y }
        });
        Require(text.Length <= 2_000 && dimension.Length <= 500 && Limitation.Length <= 240,
            "SagajeongCrosswalkDatabaseFieldTooLong");
        const string metric = "crosswalk-point-observation";
        return new 외부데이터정규화Record
        {
            RecordKey = 외부데이터RecordKey.Create(
                SourceId, DatasetId, DbRegionStableId, metric, FileModifiedDate, dimension),
            StableId = "observation:seoul-oa23081:crosswalk:" + candidate.CrosswalkManagementNumber.ToLowerInvariant(),
            SourceId = SourceId,
            DatasetId = DatasetId,
            RegionStableId = DbRegionStableId,
            MetricCode = metric,
            NumericValue = null,
            TextValue = text,
            UnitCode = "crosswalk-observation-json",
            EvidenceAsOfUtc = FileModifiedDate,
            CollectedAtUtc = receipt.AcquiredAtUtc,
            SpatialPrecisionCode = "epsg5186-point-within-frozen-bbox-candidate",
            TemporalPrecisionCode = "portal-file-vintage-not-observation-date",
            QualityCode = Quality,
            LimitationCode = Limitation,
            DimensionKey = dimension,
            SourceVersion = $"portal-file:{FileModifiedDate:yyyy-MM-dd};sha256:{ExpectedHash.ToLowerInvariant()}",
            DataRevision = DataRevision,
            FirstSeenAtUtc = receipt.AcquiredAtUtc,
            LastSeenAtUtc = receipt.AcquiredAtUtc
        };
    }

    private static int SelfTest(FrozenAcquisition frozen, ParsedData parsed)
    {
        var tests = 0;
        void Test(bool condition, string code)
        {
            Require(condition, "SagajeongCrosswalkSelfTest:" + code);
            tests++;
        }

        Test(parsed.TotalRows == 21_776, "TotalRows");
        Test(parsed.CoordinateMissingRows == 1, "CoordinateMissingRows");
        Test(parsed.Candidates.Count == 84 && parsed.Records.Count == 84, "SelectedRows");
        Test(parsed.Candidates.All(item => item.District == "중랑구"), "JungnangDistrict");
        Test(parsed.Candidates.Count(item => item.PedestrianSignalPresent) == 40, "PedestrianSignalPresent");
        Test(parsed.Candidates.Count(item => !item.PedestrianSignalPresent) == 44, "PedestrianSignalAbsent");
        Test(parsed.Candidates.Where(item => item.IntersectionManagementNumber is not null)
            .Select(item => item.IntersectionManagementNumber).Distinct(StringComparer.Ordinal).Count() == 27,
            "DistinctIntersections");
        var station = parsed.Candidates.Where(item => item.IntersectionManagementNumber == "3205").ToArray();
        Test(station.Length == 7, "SagajeongIntersectionRows");
        Test(station.Count(item => item.PedestrianSignalPresent) == 4
             && station.Count(item => !item.PedestrianSignalPresent) == 3,
            "SagajeongPedestrianSignalSplit");
        Test(station.All(item => item.IntersectionName is null or "사가정역"), "SagajeongIntersectionName");
        Test(Math.Abs((Scope.MaxX - Scope.MinX) - 1_034.423d) < 0.001d
             && Math.Abs((Scope.MaxY - Scope.MinY) - 1_022.067d) < 0.001d,
            "NominalOneKilometerEnvelopeDimensions");
        Test(parsed.Candidates.All(item => Scope.Contains(item.X, item.Y)), "PointScope");
        Test(parsed.Candidates.Select(item => item.CrosswalkManagementNumber)
            .Distinct(StringComparer.Ordinal).Count() == 84, "CrosswalkIdentity");
        Test(parsed.Records.All(item => item.QualityCode == Quality
            && item.RegionStableId == DbRegionStableId
            && item.NumericValue == null
            && item.SpatialPrecisionCode == "epsg5186-point-within-frozen-bbox-candidate"
            && item.LimitationCode.Contains("NoPublication", StringComparison.Ordinal)
            && item.LimitationCode.Contains("NoRuntime", StringComparison.Ordinal)
            && item.LimitationCode.Contains("NoTraversalAuthority", StringComparison.Ordinal)
            && item.LimitationCode.Contains("NoGameplayAuthority", StringComparison.Ordinal)), "AuthorityBoundary");
        Test(parsed.Records.All(item => !item.TextValue.Contains("상세주소", StringComparison.Ordinal)
            && !item.TextValue.Contains("담당자", StringComparison.Ordinal)
            && item.TextValue.Length <= 2_000), "PrivateFieldBoundary");
        Test(parsed.Records.Select(item => item.RecordKey).SequenceEqual(
            parsed.Records.OrderBy(item => item.StableId, StringComparer.Ordinal)
                .ThenBy(item => item.DimensionKey, StringComparer.Ordinal)
                .Select(item => item.RecordKey)), "DeterministicOrder");
        Test(frozen.Receipt.Source.Sha256.Equals(ExpectedHash, StringComparison.OrdinalIgnoreCase)
             && frozen.Receipt.Source.ContentLength == ExpectedLength, "FrozenSourceIdentity");
        Test(!frozen.Receipt.DistributionApproved
             && !frozen.Receipt.RuntimeAuthorized
             && !frozen.Receipt.TraversalAuthorized
             && !frozen.Receipt.GameplayAuthorized, "ReceiptAuthorityBoundary");
        return tests;
    }

    private static async Task ApplyAsync(
        DbContextOptions<PublicDataIngestionDbContext> options,
        FrozenAcquisition frozen,
        ParsedData parsed,
        Dictionary<string, object?> result)
    {
        await using var db = new PublicDataIngestionDbContext(options);
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT GET_LOCK('mirror:public-data:sagajeong-crosswalk-r2',0)";
        Require(Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 1,
            "SagajeongCrosswalkImportBusy");
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            result["databaseWriteAttempted"] = true;
            var service = new 평창군공공공간원본등록Service(db);
            var registration = await service.RegisterFileAsync(
                frozen.Path,
                new 공공공간원본등록Request(
                    SourceId,
                    DatasetId,
                    $"portal-file:{FileModifiedDate:yyyy-MM-dd};sha256:{ExpectedHash.ToLowerInvariant()}",
                    DataRevision,
                    FileModifiedDate,
                    ContentType,
                    RelativeFolder + "/" + FileName));
            foreach (var record in parsed.Records)
                record.RawSnapshotId = registration.RawSnapshotId;
            var before = await ReadExistingAsync(db, parsed.Records.Select(item => item.RecordKey).ToArray());
            Require(before.All(item => parsed.Records.Any(candidate => Equivalent(item, candidate))),
                "SagajeongCrosswalkExistingRecordConflict");
            var stored = await new EfExternalDataIngestionStore(db).UpsertNormalizedAsync(parsed.Records);
            Require(stored.UpdatedCount == 0, "SagajeongCrosswalkUnexpectedUpdate");

            if (registration.Inserted)
            {
                var snapshot = await db.RawSnapshots.SingleAsync(item => item.Id == registration.RawSnapshotId);
                snapshot.CollectedAtUtc = frozen.Receipt.AcquiredAtUtc;
                var run = await db.IngestionRuns.SingleAsync(item => item.Id == snapshot.FirstCollectionRunId);
                run.StatusCode = 외부데이터수집StatusCodes.Partial;
                run.FetchedCount = ExpectedTotalRows;
                run.NormalizedCount = ExpectedSelectedRows;
                run.InsertedCount = stored.InsertedCount;
                run.ExistingCount = stored.ExistingCount;
                run.ErrorCode = Quality;
                run.ErrorSummary = "Official whole-file crosswalk points cropped to the frozen Sagajeong bbox; pedestrian-signal presence is observation only; no publication, runtime, traversal or gameplay authority.";
            }

            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            result["committed"] = true;
            result["insertedCount"] = stored.InsertedCount;
            result["existingCount"] = stored.ExistingCount;
            result["rawInsertedCount"] = registration.Inserted ? 1 : 0;
        }
        finally
        {
            command.CommandText = "SELECT RELEASE_LOCK('mirror:public-data:sagajeong-crosswalk-r2')";
            _ = await command.ExecuteScalarAsync();
        }
    }

    private static async Task VerifyReadbackAsync(
        DbContextOptions<PublicDataIngestionDbContext> options,
        FrozenAcquisition frozen,
        ParsedData parsed,
        Dictionary<string, object?> result)
    {
        await using var db = new PublicDataIngestionDbContext(options);
        var stored = await ReadExistingAsync(db, parsed.Records.Select(item => item.RecordKey).ToArray());
        var revisionCount = await db.NormalizedRecords.AsNoTracking().CountAsync(item =>
            item.SourceId == SourceId
            && item.DatasetId == DatasetId
            && item.RegionStableId == DbRegionStableId
            && item.DataRevision == DataRevision);
        Require(stored.Count == ExpectedSelectedRows
                && revisionCount == ExpectedSelectedRows
                && stored.All(item => parsed.Records.Any(candidate => Equivalent(item, candidate)))
                && stored.All(item => item.RawSnapshot is not null
                    && item.RawSnapshot.ContentHashSha256.Equals(ExpectedHash, StringComparison.OrdinalIgnoreCase)),
            "SagajeongCrosswalkReadbackMismatch");
        var snapshot = await db.RawSnapshots.AsNoTracking()
            .Include(item => item.FirstCollectionRun)
            .SingleOrDefaultAsync(item => item.SourceId == SourceId
                && item.DatasetId == DatasetId
                && item.ContentHashSha256 == ExpectedHash.ToLowerInvariant());
        Require(snapshot is not null
                && snapshot.ContentLength == ExpectedLength
                && snapshot.StorageContainer == "local-private-public-spatial"
                && snapshot.StorageObjectName == (RelativeFolder + "/" + FileName).Replace('\\', '/')
                && snapshot.FirstCollectionRun?.StatusCode == 외부데이터수집StatusCodes.Partial
                && snapshot.FirstCollectionRun.ErrorCode == Quality
                && snapshot.FirstCollectionRun.FetchedCount == ExpectedTotalRows
                && snapshot.FirstCollectionRun.NormalizedCount == ExpectedSelectedRows
                && snapshot.FirstCollectionRun.InsertedCount == ExpectedSelectedRows,
            "SagajeongCrosswalkRawReadbackMismatch");
        result["verifiedRows"] = stored.Count;
        result["verifiedRawSnapshots"] = 1;
        result["independentReadback"] = true;
        result["sourceHash"] = frozen.Receipt.Source.Sha256;
    }

    private static async Task<List<외부데이터정규화Record>> ReadExistingAsync(
        PublicDataIngestionDbContext db,
        IReadOnlyList<string> keys)
    {
        var result = new List<외부데이터정규화Record>();
        for (var start = 0; start < keys.Count; start += 400)
        {
            var batch = keys.Skip(start).Take(400).ToList();
            result.AddRange(await db.NormalizedRecords.AsNoTracking()
                .Include(item => item.RawSnapshot)
                .Where(item => batch.Contains(item.RecordKey))
                .ToListAsync());
        }
        return result;
    }

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
           && stored.SpatialPrecisionCode == incoming.SpatialPrecisionCode
           && stored.TemporalPrecisionCode == incoming.TemporalPrecisionCode
           && stored.QualityCode == incoming.QualityCode
           && stored.LimitationCode == incoming.LimitationCode
           && stored.DimensionKey == incoming.DimensionKey
           && stored.SourceVersion == incoming.SourceVersion
           && stored.DataRevision == incoming.DataRevision;

    private static void SetSummary(Dictionary<string, object?> result, FrozenAcquisition frozen, ParsedData parsed)
    {
        var station = parsed.Candidates.Where(item => item.IntersectionManagementNumber == "3205").ToArray();
        result["sourceFiles"] = 1;
        result["sourceRows"] = parsed.TotalRows;
        result["coordinateMissingRows"] = parsed.CoordinateMissingRows;
        result["selectedRows"] = parsed.Records.Count;
        result["pedestrianSignalPresentRows"] = parsed.Candidates.Count(item => item.PedestrianSignalPresent);
        result["pedestrianSignalAbsentRows"] = parsed.Candidates.Count(item => !item.PedestrianSignalPresent);
        result["distinctIntersections"] = parsed.Candidates.Where(item => item.IntersectionManagementNumber is not null)
            .Select(item => item.IntersectionManagementNumber).Distinct(StringComparer.Ordinal).Count();
        result["sagajeongIntersectionNumber"] = "3205";
        result["sagajeongIntersectionRows"] = station.Length;
        result["sagajeongPedestrianSignalPresentRows"] = station.Count(item => item.PedestrianSignalPresent);
        result["sagajeongPedestrianSignalAbsentRows"] = station.Count(item => !item.PedestrianSignalPresent);
        result["selectionEnvelopeMeters"] = new { width = Scope.MaxX - Scope.MinX, height = Scope.MaxY - Scope.MinY };
        result["selectionCoordinateReferenceSystem"] = "EPSG:5186";
        result["stationStableId"] = StationStableId;
        result["worldRegionStableId"] = WorldRegionStableId;
        result["regionStableId"] = DbRegionStableId;
        result["reviewStatus"] = Quality;
        result["distributionApproved"] = false;
        result["runtimeAuthorized"] = false;
        result["traversalAuthorized"] = false;
        result["gameplayAuthorized"] = false;
        result["sourceHash"] = frozen.Receipt.Source.Sha256;
        result["folder"] = RelativeFolder;
    }

    private static XDocument LoadXml(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        return XDocument.Load(stream, LoadOptions.None);
    }

    private static ZipArchiveEntry RequiredEntry(ZipArchive archive, string path)
        => archive.GetEntry(path)
           ?? throw new InvalidDataException("SagajeongCrosswalkXlsxEntryMissing:" + path);

    private static string[] ReadSharedStrings(ZipArchive archive, XNamespace spreadsheet)
    {
        var document = LoadXml(RequiredEntry(archive, "xl/sharedStrings.xml"));
        return document.Descendants(spreadsheet + "si")
            .Select(item => string.Concat(item.Descendants(spreadsheet + "t").Select(text => text.Value)))
            .ToArray();
    }

    private static string[] ReadRow(XElement row, XNamespace spreadsheet, IReadOnlyList<string> sharedStrings)
    {
        var values = new string[ExpectedHeaders.Length];
        foreach (var cell in row.Elements(spreadsheet + "c"))
        {
            Require(cell.Element(spreadsheet + "f") is null, "SagajeongCrosswalkFormulaUnexpected");
            var reference = (string?)cell.Attribute("r")
                ?? throw new InvalidDataException("SagajeongCrosswalkCellReferenceMissing");
            Require(!string.IsNullOrWhiteSpace(reference), "SagajeongCrosswalkCellReferenceEmpty");
            var column = ColumnIndex(reference);
            Require(column >= 0 && column < ExpectedHeaders.Length, "SagajeongCrosswalkUnexpectedColumn");
            var type = (string?)cell.Attribute("t");
            var raw = cell.Element(spreadsheet + "v")?.Value ?? string.Empty;
            string value;
            if (type == "s")
            {
                Require(int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var sharedIndex)
                        && sharedIndex >= 0 && sharedIndex < sharedStrings.Count,
                    "SagajeongCrosswalkSharedStringIndexInvalid");
                value = sharedStrings[sharedIndex];
            }
            else if (type == "inlineStr")
            {
                value = string.Concat(cell.Descendants(spreadsheet + "t").Select(item => item.Value));
            }
            else
            {
                Require(type is null or "str" or "n", "SagajeongCrosswalkCellTypeChanged");
                value = raw;
            }
            values[column] = value.Trim();
        }
        return values;
    }

    private static int RequiredRowNumber(XElement row)
        => int.TryParse((string?)row.Attribute("r"), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidDataException("SagajeongCrosswalkRowNumberInvalid");

    private static int ColumnIndex(string reference)
    {
        var value = 0;
        var letters = 0;
        foreach (var character in reference)
        {
            if (character is < 'A' or > 'Z') break;
            value = checked(value * 26 + character - 'A' + 1);
            letters++;
        }
        Require(letters > 0, "SagajeongCrosswalkCellReferenceInvalid");
        return value - 1;
    }

    private static double RequiredDouble(string raw, string code)
        => double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
           && double.IsFinite(value)
            ? value
            : throw new InvalidDataException(code);

    private static string? EmptyToNull(string value) => value.Length == 0 ? null : value;

    private static DateTimeOffset RoundToMilliseconds(DateTimeOffset value)
        => new(value.Ticks - value.Ticks % TimeSpan.TicksPerMillisecond, value.Offset);

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }

    private sealed record AcquisitionReceipt(
        string SchemaVersion,
        DateTimeOffset AcquiredAtUtc,
        string StationStableId,
        string WorldRegionStableId,
        string DbRegionStableId,
        Wgs84Bounds Wgs84Bounds,
        Bounds Epsg5186Bounds,
        string SelectionMethod,
        bool DistributionApproved,
        bool RuntimeAuthorized,
        bool TraversalAuthorized,
        bool GameplayAuthorized,
        string ReviewStatus,
        SourceReceipt Source);

    private sealed record SourceReceipt(
        string InfId,
        string DatasetTitle,
        string OfficialPageUrl,
        string FileName,
        DateTimeOffset FileModifiedDate,
        string CoordinateReferenceSystem,
        long ContentLength,
        string Sha256,
        string DownloadSequence,
        string InformationSequence,
        string LicenseCode,
        string WorksheetName,
        int TotalRows,
        int SelectedRows,
        string LimitationNote);

    private sealed record FrozenAcquisition(string Folder, string Path, AcquisitionReceipt Receipt);
    private sealed record ParsedData(
        int TotalRows,
        int CoordinateMissingRows,
        IReadOnlyList<Candidate> Candidates,
        IReadOnlyList<외부데이터정규화Record> Records);
    private sealed record Candidate(
        int SourceSheetRow,
        string SourceSerialNumber,
        string District,
        string CrosswalkManagementNumber,
        string? IntersectionManagementNumber,
        string CrosswalkType,
        bool PedestrianSignalPresent,
        string? IntersectionName,
        double X,
        double Y);
    private sealed record Wgs84Bounds(double MinLongitude, double MinLatitude, double MaxLongitude, double MaxLatitude);

    private readonly record struct Bounds(double MinX, double MinY, double MaxX, double MaxY)
    {
        public bool Contains(double x, double y)
            => x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
    }
}
