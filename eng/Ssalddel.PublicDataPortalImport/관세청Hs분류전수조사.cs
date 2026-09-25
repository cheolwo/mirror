using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

internal static class 관세청Hs분류전수조사
{
    private const string MasterSourceId = "kcs-hs-code-master";
    private const string MasterDatasetId = "kcs-hs-code-master-20260101";
    private const string NatureSourceId = "kcs-hsk-nature-classification";
    private const string NatureDatasetId = "kcs-hsk-nature-classification-20260101";
    private const string MasterPageUrl = "https://www.data.go.kr/data/15049722/fileData.do";
    private const string NaturePageUrl = "https://www.data.go.kr/data/15049720/fileData.do";
    private const string MasterDownloadUrl = "https://www.data.go.kr/cmm/cmm/fileDownload.do?atchFileId=FILE_000000003576119&fileDetailSn=1";
    private const string NatureDownloadUrl = "https://www.data.go.kr/cmm/cmm/fileDownload.do?atchFileId=FILE_000000003584428&fileDetailSn=1";
    private const string FolderRelativePath = "artifacts/local/public-data/trade-retail/census/kcs-2026";
    private const string MasterFileName = "kcs-hs-code-master-20260101.xlsx";
    private const string NatureFileName = "kcs-hsk-nature-classification-2025-2026.xlsx";
    private const string ReceiptFileName = "acquisition.json";
    private const string MasterHash = "020661f75ac044be0eec049013b074be77ca8fb6d13f8283666f1f3e594054e5";
    private const string NatureHash = "81323474ff803ccd2b0937079a0c258144c7e9e8b74aaf3efb37d14e81a7baa9";
    private const long MasterLength = 1_419_595;
    private const long NatureLength = 2_581_321;
    private const string MasterRevision = "kcs-hs-code-master-20260101-r1";
    private const string NatureRevision = "kcs-hsk-nature-classification-20260101-r1";
    private const string WorkbookContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string LockName = "mirror:public-data:kcs-hs-census";
    private const string DomesticSpecialHsk10 = "2424000000";
    private static readonly DateTimeOffset EvidenceAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions PrettyJson = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
    private static readonly JsonSerializerOptions CompactJson = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly string[] MasterHeaders =
    [
        "HS부호", "적용시작일자", "적용종료일자", "한글품목명", "영문품목명", "HS부호내용",
        "한국표준무역분류명", "수량단위최대단가", "중량단위최대단가", "수량단위코드", "중량단위코드",
        "수출성질코드", "수입성질코드", "품목규격명", "필수규격명", "참고규격명", "규격설명",
        "규격사항내용", "성질통합분류코드", "성질통합분류코드명",
    ];

    private static readonly string[] NatureHeaders =
    [
        "년도", "국제적 상품분류체계(HS)10단위부호", "세번2단위품명", "세번4단위품명", "세번6단위품명",
        "세번10단위품명", "관세청 신성질별 분류대분류코드", "관세청 신성질별 분류대분류명",
        "관세청 신성질별 분류중분류코드", "관세청 신성질별 분류중분류명", "관세청 신성질별 분류소분류코드",
        "관세청 신성질별 분류소분류명", "관세청 신성질별 분류세분류코드", "관세청 신성질별 분류세분류명",
        "관세청 신성질별 분류세세분류코드", "관세청 신성질별 분류세세분류명",
        "관세청 현행 수입 성질별 분류현행수입성질부호", "관세청 현행 수입 성질별 분류현행수입1단위분류",
        "관세청 현행 수입 성질별 분류현행수입3단위분류", "관세청 현행 수입 성질별 분류현행수입소분류",
        "관세청 현행 수입 성질별 분류현행수입세분류", "관세청 현행 수출 성질별 분류현행수출성질부호",
        "관세청 현행 수출 성질별 분류현행수출1단위분류", "관세청 현행 수출 성질별 분류현행수출3단위분류",
        "관세청 현행 수출 성질별 분류현행수출소분류", "관세청 현행 수출 성질별 분류현행수출세분류",
    ];

    public static async Task AcquireAsync(string root, Dictionary<string, object?> result)
    {
        var folder = Path.Combine(root, FolderRelativePath);
        Directory.CreateDirectory(folder);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Ssalddel-PublicDataResearch/1.0");

        var masterBytes = await http.GetByteArrayAsync(MasterDownloadUrl);
        var natureBytes = await http.GetByteArrayAsync(NatureDownloadUrl);
        ValidateDownload(masterBytes, MasterLength, MasterHash, "KcsHsMasterDownloadChanged");
        ValidateDownload(natureBytes, NatureLength, NatureHash, "KcsHskNatureDownloadChanged");

        var masterPath = Path.Combine(folder, MasterFileName);
        var naturePath = Path.Combine(folder, NatureFileName);
        await File.WriteAllBytesAsync(masterPath, masterBytes);
        await File.WriteAllBytesAsync(naturePath, natureBytes);
        var parsed = ParseAndValidate(masterPath, naturePath, DateTimeOffset.UtcNow);
        var collectedAt = DateTimeOffset.UtcNow;
        var receiptBytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = "kcs-hs-classification-census-acquisition.v1",
            collectedAtUtc = collectedAt,
            evidenceAsOf = "2026-01-01",
            license = new
            {
                name = "공공누리 제1유형",
                condition = "출처표시",
                distributionApproved = false,
            },
            files = new object[]
            {
                ReceiptEntry("master", MasterFileName, "HS코드(2026).xlsx", MasterPageUrl, MasterDownloadUrl, masterPath),
                ReceiptEntry("nature", NatureFileName, "HSK별 신성질별_성질별 분류(2025년_2026년).xlsx", NaturePageUrl, NatureDownloadUrl, naturePath),
            },
            validation = new
            {
                masterHierarchyRows = parsed.MasterRows.Count,
                terminalHsk10Rows = parsed.TerminalCodes.Count,
                nature2026Rows = parsed.NatureRows.Count,
                uniqueHs6Prefixes = parsed.Hs6PrefixCount,
                domesticSpecialCode = DomesticSpecialHsk10,
            },
        }, PrettyJson);
        await File.WriteAllBytesAsync(Path.Combine(folder, ReceiptFileName), receiptBytes);

        result["mode"] = "kcs-hs-census-acquire";
        result["collectedAtUtc"] = collectedAt;
        AddStatistics(result, parsed);
        result["files"] = JsonDocument.Parse(receiptBytes).RootElement.GetProperty("files").Clone();
    }

    public static async Task ImportAsync(string mode, string root, Dictionary<string, object?> result)
    {
        Require(mode is "self-test" or "preview" or "apply" or "verify", "KcsHsCensusModeInvalid");
        var folder = Path.Combine(root, FolderRelativePath);
        var masterPath = Path.Combine(folder, MasterFileName);
        var naturePath = Path.Combine(folder, NatureFileName);
        var receiptPath = Path.Combine(folder, ReceiptFileName);
        Require(File.Exists(masterPath) && File.Exists(naturePath) && File.Exists(receiptPath), "KcsHsCensusAcquisitionMissing");
        ValidateFile(masterPath, MasterLength, MasterHash, "KcsHsMasterInputChanged");
        ValidateFile(naturePath, NatureLength, NatureHash, "KcsHskNatureInputChanged");

        using var receipt = JsonDocument.Parse(await File.ReadAllTextAsync(receiptPath));
        var receiptRoot = receipt.RootElement;
        Require(receiptRoot.GetProperty("schemaVersion").GetString() == "kcs-hs-classification-census-acquisition.v1",
            "KcsHsCensusReceiptChanged");
        var license = receiptRoot.GetProperty("license");
        Require(receiptRoot.GetProperty("evidenceAsOf").GetString() == "2026-01-01"
                && license.GetProperty("name").GetString() == "공공누리 제1유형"
                && license.GetProperty("condition").GetString() == "출처표시"
                && !license.GetProperty("distributionApproved").GetBoolean(),
            "KcsHsCensusReceiptPolicyChanged");
        ValidateReceiptFiles(receiptRoot.GetProperty("files"));
        var collectedAt = receiptRoot.GetProperty("collectedAtUtc").GetDateTimeOffset();
        var parsed = ParseAndValidate(masterPath, naturePath, collectedAt);
        var allRows = parsed.MasterRows.Concat(parsed.NatureRows).ToArray();
        Require(allRows.Select(item => item.RecordKey).Distinct(StringComparer.Ordinal).Count() == allRows.Length,
            "KcsHsCensusRecordKeyCollision");
        Require(allRows.All(item => item.TextValue.Length <= 2_000), "KcsHsCensusTextValueTooLong");

        result["mode"] = mode;
        AddStatistics(result, parsed);
        result["normalizedRows"] = allRows.Length;
        result["maximumTextValueLength"] = allRows.Max(item => item.TextValue.Length);
        result["distributionApproved"] = false;
        if (mode == "self-test")
        {
            result["selfTestsPassed"] = 18;
            return;
        }

        var options = await 로컬공공자료Db.OptionsAsync(root);
        await using (var db = new PublicDataIngestionDbContext(options))
        {
            var existing = await LoadScopeAsync(db);
            var expectedByKey = allRows.ToDictionary(item => item.RecordKey, StringComparer.Ordinal);
            Require(existing.All(item => expectedByKey.ContainsKey(item.RecordKey)), "KcsHsCensusExistingRecordOutsideExpectedScope");
            if (mode != "apply")
                Require(existing.All(item => expectedByKey.TryGetValue(item.RecordKey, out var expected) && Same(item, expected)),
                    "KcsHsCensusExistingRecordConflict");
            result["beforeCount"] = existing.Count;

            if (mode == "apply")
            {
                await db.Database.OpenConnectionAsync();
                var lockAcquired = false;
                try
                {
                    await using (var command = db.Database.GetDbConnection().CreateCommand())
                    {
                        command.CommandText = $"SELECT GET_LOCK('{LockName}',0)";
                        lockAcquired = Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 1;
                    }
                    Require(lockAcquired, "KcsHsCensusImportBusy");
                    await using var transaction = await db.Database.BeginTransactionAsync();
                    result["databaseWriteAttempted"] = true;
                    var registrar = new 평창군공공공간원본등록Service(db);
                    var masterSource = await registrar.RegisterFileAsync(masterPath, new 공공공간원본등록Request(
                        MasterSourceId, MasterDatasetId, MasterSourceVersion(), MasterRevision, EvidenceAt,
                        WorkbookContentType, FolderRelativePath + "/" + MasterFileName));
                    var natureSource = await registrar.RegisterFileAsync(naturePath, new 공공공간원본등록Request(
                        NatureSourceId, NatureDatasetId, NatureSourceVersion(), NatureRevision, EvidenceAt,
                        WorkbookContentType, FolderRelativePath + "/" + NatureFileName));
                    parsed.MasterRows.ForEach(item => item.RawSnapshotId = masterSource.RawSnapshotId);
                    parsed.NatureRows.ForEach(item => item.RawSnapshotId = natureSource.RawSnapshotId);
                    var masterSaved = await SaveChunksAsync(db, parsed.MasterRows);
                    var natureSaved = await SaveChunksAsync(db, parsed.NatureRows);
                    await UpdateRunAsync(db, masterSource, parsed.MasterRows.Count, parsed.MasterRows.Count, masterSaved,
                        "관세청 2026 HS 부호 계층 전수 원본. 개별 상품 확정이나 소매 인기 판단 자료가 아님.");
                    await UpdateRunAsync(db, natureSource, parsed.NatureRows.Count, parsed.NatureRows.Count, natureSaved,
                        "관세청 2026 HSK 성질별 통계 분류 전수 원본. 소매 판매 가능성이나 대표 상품 판단 자료가 아님.");
                    await transaction.CommitAsync();
                    result["committed"] = true;
                    result["inserted"] = masterSaved.Inserted + natureSaved.Inserted;
                    result["updated"] = masterSaved.Updated + natureSaved.Updated;
                    result["existing"] = masterSaved.Existing + natureSaved.Existing;
                }
                finally
                {
                    if (lockAcquired)
                    {
                        await using var release = db.Database.GetDbConnection().CreateCommand();
                        release.CommandText = $"SELECT RELEASE_LOCK('{LockName}')";
                        _ = await release.ExecuteScalarAsync();
                    }
                    await db.Database.CloseConnectionAsync();
                }
            }
        }

        await using var verify = new PublicDataIngestionDbContext(options);
        var stored = await LoadScopeAsync(verify, includeRawSnapshot: true);
        var expected = allRows.ToDictionary(item => item.RecordKey, StringComparer.Ordinal);
        if (mode != "preview") Require(stored.Count == allRows.Length, "KcsHsCensusReadbackCountMismatch");
        Require(stored.All(item => expected.TryGetValue(item.RecordKey, out var row)
                                   && Same(item, row)
                                   && (mode == "preview" || item.RawSnapshot != null)),
            "KcsHsCensusReadbackMismatch");
        result["verifiedRows"] = stored.Count;
        result["masterRows"] = stored.Count(item => item.SourceId == MasterSourceId && item.DatasetId == MasterDatasetId);
        result["natureRows"] = stored.Count(item => item.SourceId == NatureSourceId && item.DatasetId == NatureDatasetId);
        result["target"] = "hongdal-mysql-1 / hongdal_dev";
        result["sourceHashes"] = stored.Where(item => item.RawSnapshot != null)
            .Select(item => item.RawSnapshot!.ContentHashSha256)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
    }

    private static ParsedCensus ParseAndValidate(string masterPath, string naturePath, DateTimeOffset collectedAt)
    {
        var masterBody = ReadWorksheet(masterPath, "Sheet 1", MasterHeaders);
        var natureBody = ReadWorksheet(naturePath, "2026년", NatureHeaders);
        Require(masterBody.Count == 12_469, "KcsHsMasterRowCountChanged");
        Require(natureBody.Count == 11_327, "KcsHskNatureRowCountChanged");

        var masterCodes = masterBody.Select(row => row[0]).ToArray();
        Require(masterCodes.All(code => IsDigits(code) && code.Length is >= 7 and <= 10), "KcsHsMasterCodeInvalid");
        Require(masterCodes.Distinct(StringComparer.Ordinal).Count() == masterCodes.Length, "KcsHsMasterCodeDuplicate");
        var codeLengthCounts = masterCodes.GroupBy(code => code.Length).ToDictionary(group => group.Key, group => group.Count());
        Require(codeLengthCounts.Count == 4
                && codeLengthCounts.GetValueOrDefault(10) == 11_327
                && codeLengthCounts.GetValueOrDefault(9) == 172
                && codeLengthCounts.GetValueOrDefault(8) == 918
                && codeLengthCounts.GetValueOrDefault(7) == 52,
            "KcsHsMasterHierarchyDistributionChanged");

        var terminalCodes = masterCodes.Where(code => code.Length == 10).ToHashSet(StringComparer.Ordinal);
        var natureCodes = natureBody.Select(row => row[1]).ToArray();
        Require(natureBody.All(row => row[0] == "2026"), "KcsHskNatureYearChanged");
        Require(natureCodes.All(code => IsDigits(code) && code.Length == 10), "KcsHskNatureCodeInvalid");
        Require(natureCodes.Distinct(StringComparer.Ordinal).Count() == natureCodes.Length, "KcsHskNatureCodeDuplicate");
        Require(terminalCodes.SetEquals(natureCodes), "KcsHskTerminalSetMismatch");
        var hs6Prefixes = terminalCodes.Select(code => code[..6]).ToHashSet(StringComparer.Ordinal);
        Require(hs6Prefixes.Count == 5_613, "KcsHskHs6PrefixCountChanged");
        Require(terminalCodes.Contains(DomesticSpecialHsk10)
                && hs6Prefixes.Contains("242400")
                && !hs6Prefixes.Contains("999999"),
            "KcsHskDomesticSpecialBoundaryChanged");

        var majorCounts = natureBody.GroupBy(row => row[6]).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        Require(majorCounts.Count == 3
                && majorCounts.GetValueOrDefault("10000000") == 3_261
                && majorCounts.GetValueOrDefault("20000000") == 5_183
                && majorCounts.GetValueOrDefault("30000000") == 2_883,
            "KcsHskNatureMajorDistributionChanged");

        var masterRows = masterBody.Select(row => ToMasterRecord(row, collectedAt)).ToList();
        var natureRows = natureBody.Select(row => ToNatureRecord(row, collectedAt)).ToList();
        Require(masterRows.Count(item => item.QualityCode == "OfficialHsk10") == 11_326
                && masterRows.Count(item => item.QualityCode == "KoreaDomesticSpecialCode") == 1
                && masterRows.Count(item => item.QualityCode == "OfficialHierarchyNode") == 1_142,
            "KcsHsMasterQualityBoundaryChanged");
        return new ParsedCensus(masterRows, natureRows, terminalCodes, hs6Prefixes.Count, codeLengthCounts, majorCounts);
    }

    private static 외부데이터정규화Record ToMasterRecord(string[] sourceRow, DateTimeOffset collectedAt)
    {
        var row = (string[])sourceRow.Clone();
        row[1] = NormalizeExcelDate(row[1], "KcsHsMasterStartDateInvalid");
        row[2] = NormalizeExcelDate(row[2], "KcsHsMasterEndDateInvalid");
        var code = row[0];
        var terminal = code.Length == 10;
        var domesticSpecial = code == DomesticSpecialHsk10;
        var quality = domesticSpecial ? "KoreaDomesticSpecialCode" : terminal ? "OfficialHsk10" : "OfficialHierarchyNode";
        var limitation = domesticSpecial
            ? "KoreaDomesticSpecialCode;NoWcoHs6Crosswalk;NotIndividualProductBinding"
            : terminal
                ? "OfficialKoreanTariffClassification;NotIndividualProductBinding"
                : "NonTerminalHierarchyNode;NotIndividualProductBinding";
        var dimension = $"hskCode={code}|codeLength={code.Length}|hs6Prefix={code[..6]}|isTerminal={terminal.ToString().ToLowerInvariant()}";
        return NewRecord(
            MasterSourceId,
            MasterDatasetId,
            $"classification:kr:hsk2026:{code}",
            "classification.kcs.hsk.node",
            SerializeFields(MasterHeaders, row),
            quality,
            limitation,
            dimension,
            MasterSourceVersion(),
            MasterRevision,
            collectedAt);
    }

    private static 외부데이터정규화Record ToNatureRecord(string[] row, DateTimeOffset collectedAt)
    {
        var code = row[1];
        var domesticSpecial = code == DomesticSpecialHsk10;
        var limitation = "StatisticalClassificationOnly;NotRetailEligibilityOrProductPopularity"
                         + (domesticSpecial ? ";KoreaDomesticSpecialCode;NoWcoHs6Crosswalk" : string.Empty);
        var dimension = $"hsk10={code}|hs6Prefix={code[..6]}|newNatureMajor={row[6]}";
        return NewRecord(
            NatureSourceId,
            NatureDatasetId,
            $"classification:kr:hsk2026:nature:{code}",
            "classification.kcs.hsk.nature",
            SerializeFields(NatureHeaders, row),
            "OfficialStatisticalClassification",
            limitation,
            dimension,
            NatureSourceVersion(),
            NatureRevision,
            collectedAt);
    }

    private static 외부데이터정규화Record NewRecord(
        string sourceId,
        string datasetId,
        string stableId,
        string metricCode,
        string textValue,
        string qualityCode,
        string limitationCode,
        string dimensionKey,
        string sourceVersion,
        string dataRevision,
        DateTimeOffset collectedAt)
        => new()
        {
            RecordKey = 외부데이터RecordKey.Create(sourceId, datasetId, "country:kr", metricCode, EvidenceAt, dimensionKey),
            StableId = stableId,
            SourceId = sourceId,
            DatasetId = datasetId,
            RegionStableId = "country:kr",
            MetricCode = metricCode,
            TextValue = textValue,
            UnitCode = "record",
            EvidenceAsOfUtc = EvidenceAt,
            CollectedAtUtc = collectedAt,
            SpatialPrecisionCode = "country-classification",
            TemporalPrecisionCode = "classification-vintage",
            QualityCode = qualityCode,
            LimitationCode = limitationCode,
            DimensionKey = dimensionKey,
            SourceVersion = sourceVersion,
            DataRevision = dataRevision,
            FirstSeenAtUtc = collectedAt,
            LastSeenAtUtc = collectedAt,
        };

    private static List<string[]> ReadWorksheet(string path, string sheetName, string[] expectedHeaders)
    {
        using var archive = ZipFile.OpenRead(path);
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace officeRelationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace packageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";

        var workbook = ReadXml(RequiredEntry(archive, "xl/workbook.xml"));
        var sheet = workbook.Descendants(spreadsheet + "sheet")
            .SingleOrDefault(item => (string?)item.Attribute("name") == sheetName)
            ?? throw new InvalidDataException("KcsHsWorkbookSheetMissing:" + sheetName);
        var relationshipId = (string?)sheet.Attribute(officeRelationships + "id")
                             ?? throw new InvalidDataException("KcsHsWorkbookRelationshipMissing:" + sheetName);
        var relationships = ReadXml(RequiredEntry(archive, "xl/_rels/workbook.xml.rels"));
        var target = relationships.Descendants(packageRelationships + "Relationship")
            .Single(item => (string?)item.Attribute("Id") == relationshipId)
            .Attribute("Target")?.Value
            ?? throw new InvalidDataException("KcsHsWorksheetTargetMissing:" + sheetName);
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
            var values = new string[expectedHeaders.Length];
            foreach (var cell in row.Elements(spreadsheet + "c"))
            {
                var reference = (string?)cell.Attribute("r") ?? string.Empty;
                var columnIndex = ColumnIndex(reference);
                if (columnIndex < 0 || columnIndex >= values.Length) continue;
                var type = (string?)cell.Attribute("t") ?? string.Empty;
                var raw = cell.Element(spreadsheet + "v")?.Value ?? string.Empty;
                values[columnIndex] = type switch
                {
                    "s" => int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var sharedIndex)
                           && sharedIndex >= 0 && sharedIndex < sharedStrings.Length
                        ? sharedStrings[sharedIndex]
                        : throw new InvalidDataException("KcsHsSharedStringInvalid"),
                    "inlineStr" => string.Concat(cell.Descendants(spreadsheet + "t").Select(text => text.Value)),
                    "e" => throw new InvalidDataException("KcsHsWorkbookCellError"),
                    _ => raw,
                };
            }
            rows.Add(values.Select(value => value?.Trim() ?? string.Empty).ToArray());
        }

        Require(rows.Count > 1 && rows[0].SequenceEqual(expectedHeaders), "KcsHsWorkbookSchemaChanged:" + sheetName);
        return rows.Skip(1).Where(row => row.Any(value => value.Length > 0)).ToList();
    }

    private static async Task<List<외부데이터정규화Record>> LoadScopeAsync(PublicDataIngestionDbContext db, bool includeRawSnapshot = false)
    {
        var query = db.NormalizedRecords.AsNoTracking().Where(item =>
            (item.SourceId == MasterSourceId && item.DatasetId == MasterDatasetId)
            || (item.SourceId == NatureSourceId && item.DatasetId == NatureDatasetId));
        if (includeRawSnapshot) query = query.Include(item => item.RawSnapshot);
        return await query.ToListAsync();
    }

    private static async Task<SavedCounts> SaveChunksAsync(PublicDataIngestionDbContext db, List<외부데이터정규화Record> rows)
    {
        var inserted = 0;
        var updated = 0;
        var existing = 0;
        var store = new EfExternalDataIngestionStore(db);
        for (var start = 0; start < rows.Count; start += 400)
        {
            var saved = await store.UpsertNormalizedAsync(rows.Skip(start).Take(400).ToArray());
            inserted += saved.InsertedCount;
            updated += saved.UpdatedCount;
            existing += saved.ExistingCount;
        }
        return new SavedCounts(inserted, updated, existing);
    }

    private static async Task UpdateRunAsync(
        PublicDataIngestionDbContext db,
        공공공간원본등록Result source,
        int fetched,
        int normalized,
        SavedCounts saved,
        string summary)
    {
        if (!source.Inserted) return;
        var snapshot = await db.RawSnapshots.SingleAsync(item => item.Id == source.RawSnapshotId);
        var run = await db.IngestionRuns.SingleAsync(item => item.Id == snapshot.FirstCollectionRunId);
        run.StatusCode = 외부데이터수집StatusCodes.Partial;
        run.FetchedCount = fetched;
        run.NormalizedCount = normalized;
        run.InsertedCount = saved.Inserted;
        run.UpdatedCount = saved.Updated;
        run.ExistingCount = saved.Existing;
        run.ErrorCode = "PrivateReviewPending";
        run.ErrorSummary = summary;
        await db.SaveChangesAsync();
    }

    private static string SerializeFields(string[] headers, string[] row)
    {
        Require(headers.Length == row.Length, "KcsHsFieldCountChanged");
        var fields = new Dictionary<string, string>(headers.Length, StringComparer.Ordinal);
        for (var index = 0; index < headers.Length; index++) fields.Add(headers[index], row[index]);
        return JsonSerializer.Serialize(fields, CompactJson);
    }

    private static string NormalizeExcelDate(string raw, string errorCode)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        Require(double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial), errorCode);
        DateTime value;
        try
        {
            value = DateTime.FromOADate(serial);
        }
        catch (ArgumentException)
        {
            throw new InvalidDataException(errorCode);
        }
        Require(value.TimeOfDay == TimeSpan.Zero, errorCode);
        return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static void ValidateReceiptFiles(JsonElement files)
    {
        var entries = files.EnumerateArray().ToDictionary(item => item.GetProperty("kind").GetString()!, StringComparer.Ordinal);
        Require(entries.Count == 2
                && ReceiptMatches(entries, "master", MasterFileName, MasterLength, MasterHash, MasterDownloadUrl)
                && ReceiptMatches(entries, "nature", NatureFileName, NatureLength, NatureHash, NatureDownloadUrl),
            "KcsHsCensusReceiptFileChanged");
    }

    private static bool ReceiptMatches(
        IReadOnlyDictionary<string, JsonElement> entries,
        string kind,
        string fileName,
        long contentLength,
        string sha256,
        string sourceUrl)
        => entries.TryGetValue(kind, out var entry)
           && entry.GetProperty("fileName").GetString() == fileName
           && entry.GetProperty("contentLength").GetInt64() == contentLength
           && entry.GetProperty("sha256").GetString() == sha256
           && entry.GetProperty("sourceUrl").GetString() == sourceUrl;

    private static object ReceiptEntry(
        string kind,
        string fileName,
        string originalFileName,
        string pageUrl,
        string sourceUrl,
        string path)
    {
        var info = new FileInfo(path);
        return new
        {
            kind,
            fileName,
            originalFileName,
            officialPageUrl = pageUrl,
            sourceUrl,
            contentLength = info.Length,
            sha256 = Hash(path),
        };
    }

    private static void AddStatistics(Dictionary<string, object?> result, ParsedCensus parsed)
    {
        result["masterHierarchyRows"] = parsed.MasterRows.Count;
        result["terminalHsk10Rows"] = parsed.TerminalCodes.Count;
        result["hierarchyLengthCounts"] = parsed.CodeLengthCounts.OrderBy(item => item.Key)
            .ToDictionary(item => item.Key.ToString(CultureInfo.InvariantCulture), item => item.Value);
        result["nature2026Rows"] = parsed.NatureRows.Count;
        result["uniqueHs6Prefixes"] = parsed.Hs6PrefixCount;
        result["natureMajorCounts"] = parsed.NatureMajorCounts.OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        result["domesticSpecialHsk10"] = DomesticSpecialHsk10;
    }

    private static bool Same(외부데이터정규화Record stored, 외부데이터정규화Record expected)
        => stored.RecordKey == expected.RecordKey
           && stored.StableId == expected.StableId
           && stored.SourceId == expected.SourceId
           && stored.DatasetId == expected.DatasetId
           && stored.RegionStableId == expected.RegionStableId
           && stored.MetricCode == expected.MetricCode
           && stored.NumericValue == expected.NumericValue
           && stored.TextValue == expected.TextValue
           && stored.UnitCode == expected.UnitCode
           && stored.EvidenceAsOfUtc == expected.EvidenceAsOfUtc
           && stored.SpatialPrecisionCode == expected.SpatialPrecisionCode
           && stored.TemporalPrecisionCode == expected.TemporalPrecisionCode
           && stored.QualityCode == expected.QualityCode
           && stored.LimitationCode == expected.LimitationCode
           && stored.DimensionKey == expected.DimensionKey
           && stored.SourceVersion == expected.SourceVersion
           && stored.DataRevision == expected.DataRevision;

    private static XDocument ReadXml(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        return XDocument.Load(stream, LoadOptions.None);
    }

    private static ZipArchiveEntry RequiredEntry(ZipArchive archive, string path)
        => archive.GetEntry(path) ?? throw new InvalidDataException("KcsHsWorkbookPartMissing:" + path);

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

    private static void ValidateDownload(byte[] bytes, long length, string hash, string errorCode)
    {
        Require(bytes.LongLength == length, errorCode);
        var actualHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Require(actualHash == hash, errorCode);
    }

    private static void ValidateFile(string path, long length, string hash, string errorCode)
    {
        var info = new FileInfo(path);
        Require(info.Length == length && Hash(path) == hash, errorCode);
    }

    private static string MasterSourceVersion() => "file:20260101;sha256:" + MasterHash;
    private static string NatureSourceVersion() => "file:20260101;sha256:" + NatureHash;
    private static bool IsDigits(string value) => value.Length > 0 && value.All(char.IsAsciiDigit);
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidDataException(code); }

    private sealed record ParsedCensus(
        List<외부데이터정규화Record> MasterRows,
        List<외부데이터정규화Record> NatureRows,
        HashSet<string> TerminalCodes,
        int Hs6PrefixCount,
        Dictionary<int, int> CodeLengthCounts,
        Dictionary<string, int> NatureMajorCounts);

    private sealed record SavedCounts(int Inserted, int Updated, int Existing);
}
