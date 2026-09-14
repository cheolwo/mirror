using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

internal static class 사가정차선신호자료
{
    private const string RelativeFolder = "artifacts/local/public-data/sagajeong-static-traffic-20260913-r1";
    private const string ReceiptFileName = "acquisition.json";
    private const string SourceId = "seoul-open-data-tgis";
    private const string DataRevision = "sagajeong-static-traffic-20260913.r1";
    private const string StationStableId = "station:kr:kric:s1107:0722";
    private const string WorldRegionStableId = "world-region:kr:seoul:jungnang:sagajeong.r1";
    private const string DbRegionStableId = "area:kr:seoul:jungnang:sagajeong-station-0722-one-kilometer";
    private const string CrsUnresolvedReviewRegionStableId = "area:kr:seoul:traffic-signal-crs-unresolved-review";
    private const string Quality = "PendingHumanReview";
    private const string CommonLimit = "PrivateReviewOnly;NoPublication;NoRuntime;NoTraversalAuthority;NoGameplayAuthority;CrossVintageSources";
    private const string SelectionMethod = "OfficialWholeFileThenEpsg5186BboxOrCrsUnresolvedNumericEnvelopeCandidateSelection";
    private const string DownloadEndpoint = "https://datafile.seoul.go.kr/bigfile/iot/inf/nio_download.do?&useCache=false";
    private const long MaximumDownloadBytes = 80L * 1024 * 1024;

    // 기존 사가정 표현 창과 동일한 WGS84 범위를 EPSG:5186으로 변환한 고정 선택 창이다.
    // 이 숫자 자체가 통행 권위나 차로 중심선을 만들지 않는다.
    private static readonly Bounds Scope = new(207286.783d, 552964.590d, 208321.206d, 553986.657d);

    private static readonly SourceSpec[] Sources =
    [
        new("lane", "OA-15537", "서울시 차선 관련 정보", "A058_L_차선.zip", "oa-15537-lane-line",
            new DateTimeOffset(2021, 8, 9, 0, 0, 0, TimeSpan.Zero), 27_185_849,
            "6A930A8B70B76F6FD0E2EE5576F2CA666620396AC3653E9A7CA28BDEC8A00120", "application/zip", 341_957, 1_039),
        new("direction", "OA-15536", "서울시 방향표시 관련 정보", "A055_P_방향표시_20260213.zip", "oa-15536-pavement-direction",
            new DateTimeOffset(2026, 2, 13, 0, 0, 0, TimeSpan.Zero), 6_048_732,
            "62481AB1432C1C4ACC3398CE914C43C30957D0DD2E830C04BCF9623AF17A1DBE", "application/zip", 157_889, 318),
        new("intersection", "OA-15534", "서울시 교차로 관련 정보", "A008_P_20250814.zip", "oa-15534-intersection",
            new DateTimeOffset(2025, 8, 14, 0, 0, 0, TimeSpan.Zero), 534_342,
            "A77B4D4FD2886C934D2D097558A52580FA95ADB079BA828F1305DFD15F0E0449", "application/zip", 8_097, 25),
        new("controller", "OA-15538", "서울시 교통신호제어기 관련 정보", "A061_P_제어기.zip", "oa-15538-signal-controller",
            new DateTimeOffset(2021, 8, 9, 0, 0, 0, TimeSpan.Zero), 377_655,
            "39C1185668CAE5819BC11CEF6D341BA4AD6790F130374BCE1D8AE3684CFDA4C0", "application/zip", 4_383, 16),
        new("signal", "OA-15546", "서울시 신호등 관련 정보", "신호등(부착대).csv", "oa-15546-signal-head-arm",
            new DateTimeOffset(2023, 6, 23, 0, 0, 0, TimeSpan.Zero), 13_284_355,
            "B41A21AF6A4D69CF8AAC670829ECC53FD9026FE7354F837317DDD69F03A30628", "text/csv", 44_020, 125)
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

        Require(mode is "self-test" or "preview" or "apply" or "verify", "SagajeongTrafficModeInvalid");
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
                "SagajeongTrafficExistingRecordConflict");
            result["beforeCount"] = existing.Count;
            return;
        }

        if (mode == "apply")
            await ApplyAsync(root, options, frozen, parsed, result);

        await VerifyReadbackAsync(options, frozen, parsed, result);
    }

    private static async Task AcquireAsync(string root, Dictionary<string, object?> result)
    {
        var folder = Path.Combine(root, RelativeFolder);
        if (Directory.Exists(folder))
        {
            var frozen = LoadFrozen(root);
            result["reusedFrozenAcquisition"] = true;
            result["sourceFiles"] = frozen.Receipt.Sources.Count;
            result["folder"] = RelativeFolder;
            return;
        }

        var parent = Path.GetDirectoryName(folder)!;
        Directory.CreateDirectory(parent);
        var staging = folder + ".acquiring-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(staging);
        var acquiredAt = RoundToMilliseconds(DateTimeOffset.UtcNow);
        var receipts = new List<SourceReceipt>();
        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        using var http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(60),
            MaxResponseContentBufferSize = 2 * 1024 * 1024
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mirror-Sagajeong-Static-Traffic-Acquisition/1.0");

        foreach (var spec in Sources)
        {
            var pageUrl = PageUrl(spec);
            using var pageResponse = await http.GetAsync(pageUrl, HttpCompletionOption.ResponseContentRead);
            Require(pageResponse.StatusCode == HttpStatusCode.OK
                    && pageResponse.RequestMessage?.RequestUri?.Host == "data.seoul.go.kr",
                "SagajeongTrafficOfficialPageUnavailable:" + spec.Key);
            var page = WebUtility.HtmlDecode(await pageResponse.Content.ReadAsStringAsync());
            Require(page.Contains(spec.DatasetTitle, StringComparison.Ordinal)
                    && page.Contains("EPSG:5186", StringComparison.OrdinalIgnoreCase)
                    && page.Contains("공공누리 1유형", StringComparison.Ordinal)
                    && page.Contains("datafile.seoul.go.kr/bigfile/iot/inf/nio_download.do", StringComparison.Ordinal),
                "SagajeongTrafficOfficialPageContractChanged:" + spec.Key);
            var sequence = FindDownloadSequence(page, spec.FileName);
            var fileForm = RequiredMatch(page,
                "<form\\s+name=\"frmFile\"(?<value>[\\s\\S]*?)</form>", "value");
            var infSeq = RequiredMatch(fileForm,
                "name=\"infSeq\"\\s+value=\"(?<value>[0-9]+)\"", "value");

            using var request = new HttpRequestMessage(HttpMethod.Post, DownloadEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["infId"] = spec.InfId,
                    ["seqNo"] = sequence,
                    ["seq"] = sequence,
                    ["infSeq"] = infSeq
                })
            };
            using var downloadTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, downloadTimeout.Token);
            Require(response.StatusCode == HttpStatusCode.OK
                    && response.RequestMessage?.RequestUri?.Host == "datafile.seoul.go.kr"
                    && response.Content.Headers.ContentType?.MediaType != "text/html",
                "SagajeongTrafficDownloadRejected:" + spec.Key);
            if (response.Content.Headers.ContentLength is long advertised)
                Require(advertised == spec.ExpectedLength && advertised <= MaximumDownloadBytes,
                    "SagajeongTrafficDownloadLengthChanged:" + spec.Key);

            var path = Path.Combine(staging, spec.FileName);
            var downloaded = await DownloadBoundedAsync(response, path, downloadTimeout.Token);
            Require(downloaded.Length == spec.ExpectedLength
                    && downloaded.Hash.Equals(spec.ExpectedHash, StringComparison.OrdinalIgnoreCase),
                "SagajeongTrafficDownloadHashChanged:" + spec.Key);
            receipts.Add(new SourceReceipt(
                spec.Key, spec.InfId, spec.DatasetTitle, pageUrl, spec.FileName,
                spec.FileModifiedDate, SourceCoordinateReferenceSystem(spec), downloaded.Length, downloaded.Hash,
                sequence, infSeq, "KOGL-Type1-Attribution", spec.TotalRows, spec.SelectedRows,
                SourceCrsNote(spec)));
        }

        var receipt = new AcquisitionReceipt(
            "sagajeong-static-traffic-acquisition.r1", acquiredAt, StationStableId,
            WorldRegionStableId, DbRegionStableId, CrsUnresolvedReviewRegionStableId,
            new Wgs84Bounds(127.0825d, 37.5762d, 127.0942d, 37.5854d), Scope,
            SelectionMethod,
            false, false, false, Quality, receipts);
        await File.WriteAllTextAsync(
            Path.Combine(staging, ReceiptFileName),
            JsonSerializer.Serialize(receipt, JsonOptions) + Environment.NewLine,
            new UTF8Encoding(false));
        Directory.Move(staging, folder);
        result["sourceFiles"] = receipts.Count;
        result["downloadedBytes"] = receipts.Sum(item => item.ContentLength);
        result["folder"] = RelativeFolder;
        result["reviewStatus"] = Quality;
    }

    private static FrozenAcquisition LoadFrozen(string root)
    {
        var folder = Path.Combine(root, RelativeFolder);
        Require(Directory.Exists(folder), "SagajeongTrafficAcquisitionMissing");
        var receiptPath = Path.Combine(folder, ReceiptFileName);
        Require(File.Exists(receiptPath), "SagajeongTrafficReceiptMissing");
        var receipt = JsonSerializer.Deserialize<AcquisitionReceipt>(File.ReadAllText(receiptPath), JsonOptions)
            ?? throw new InvalidDataException("SagajeongTrafficReceiptInvalid");
        Require(receipt.SchemaVersion == "sagajeong-static-traffic-acquisition.r1"
                && receipt.StationStableId == StationStableId
                && receipt.WorldRegionStableId == WorldRegionStableId
                && receipt.DbRegionStableId == DbRegionStableId
                && receipt.CrsUnresolvedReviewRegionStableId == CrsUnresolvedReviewRegionStableId
                && receipt.Wgs84Bounds == new Wgs84Bounds(127.0825d, 37.5762d, 127.0942d, 37.5854d)
                && receipt.SelectionMethod == SelectionMethod
                && !receipt.DistributionApproved
                && !receipt.RuntimeAuthorized
                && !receipt.TraversalAuthorized
                && receipt.ReviewStatus == Quality
                && receipt.Epsg5186Bounds == Scope
                && receipt.Sources.Count == Sources.Length,
            "SagajeongTrafficReceiptContractChanged");

        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var spec in Sources)
        {
            var source = receipt.Sources.SingleOrDefault(item => item.Key == spec.Key)
                ?? throw new InvalidDataException("SagajeongTrafficReceiptSourceMissing:" + spec.Key);
            Require(source.InfId == spec.InfId
                    && source.DatasetTitle == spec.DatasetTitle
                    && source.OfficialPageUrl == PageUrl(spec)
                    && source.FileName == spec.FileName
                    && source.FileModifiedDate == spec.FileModifiedDate
                    && source.CoordinateReferenceSystem == SourceCoordinateReferenceSystem(spec)
                    && source.ContentLength == spec.ExpectedLength
                    && source.Sha256.Equals(spec.ExpectedHash, StringComparison.OrdinalIgnoreCase)
                    && source.LicenseCode == "KOGL-Type1-Attribution"
                    && source.TotalRows == spec.TotalRows
                    && source.SelectedRows == spec.SelectedRows
                    && source.CrsNote == SourceCrsNote(spec),
                "SagajeongTrafficReceiptSourceChanged:" + spec.Key);
            var path = Path.Combine(folder, spec.FileName);
            Require(File.Exists(path) && new FileInfo(path).Length == spec.ExpectedLength,
                "SagajeongTrafficRawFileMissing:" + spec.Key);
            Require(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))
                    .Equals(spec.ExpectedHash, StringComparison.OrdinalIgnoreCase),
                "SagajeongTrafficRawHashChanged:" + spec.Key);
            paths.Add(spec.Key, path);
        }
        return new FrozenAcquisition(folder, receipt, paths);
    }

    private static ParsedData Parse(FrozenAcquisition frozen)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var all = new List<Candidate>();
        var counts = new Dictionary<string, SourceCount>(StringComparer.Ordinal);
        foreach (var spec in Sources)
        {
            ParsedSource parsed = spec.Key == "signal"
                ? ParseSignalCsv(frozen.Paths[spec.Key], spec)
                : ParseShapefileZip(frozen.Paths[spec.Key], spec);
            Require(parsed.TotalRows == spec.TotalRows && parsed.Candidates.Count == spec.SelectedRows,
                "SagajeongTrafficSourceCountChanged:" + spec.Key);
            all.AddRange(parsed.Candidates);
            counts.Add(spec.Key, new SourceCount(parsed.TotalRows, parsed.Candidates.Count));
        }

        var records = all.Select(candidate => ToRecord(candidate, frozen.Receipt))
            .OrderBy(item => item.DatasetId, StringComparer.Ordinal)
            .ThenBy(item => item.StableId, StringComparer.Ordinal)
            .ThenBy(item => item.DimensionKey, StringComparer.Ordinal)
            .ToArray();
        Require(records.Length == Sources.Sum(item => item.SelectedRows)
                && records.Select(item => item.RecordKey).Distinct(StringComparer.Ordinal).Count() == records.Length,
            "SagajeongTrafficRecordIdentityCollision");
        return new ParsedData(all, records, counts);
    }

    private static ParsedSource ParseShapefileZip(string path, SourceSpec spec)
    {
        using var archive = new ZipArchive(File.OpenRead(path), ZipArchiveMode.Read, false, Encoding.GetEncoding(949));
        var shp = SingleEntry(archive, ".shp");
        var dbf = SingleEntry(archive, ".dbf");
        var prj = SingleEntry(archive, ".prj");
        var cpg = SingleEntry(archive, ".cpg");
        Require(new[] { shp, dbf, prj, cpg }
                .Select(item => Path.GetFileNameWithoutExtension(item.FullName))
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1,
            "SagajeongTrafficArchiveLayerNameMismatch:" + spec.Key);
        var projection = ReadText(prj, Encoding.UTF8);
        Require(projection.Contains("False_Easting\",200000", StringComparison.OrdinalIgnoreCase)
                && projection.Contains("False_Northing\",600000", StringComparison.OrdinalIgnoreCase)
                && projection.Contains("Central_Meridian\",127", StringComparison.OrdinalIgnoreCase)
                && projection.Contains("Latitude_Of_Origin\",38", StringComparison.OrdinalIgnoreCase)
                && projection.Contains("Scale_Factor\",1", StringComparison.OrdinalIgnoreCase)
                && projection.Contains("PROJECTION[\"Transverse_Mercator\"]", StringComparison.OrdinalIgnoreCase)
                && projection.Contains("SPHEROID[\"GRS_1980\",6378137", StringComparison.OrdinalIgnoreCase)
                && projection.Contains("UNIT[\"Meter\",1", StringComparison.OrdinalIgnoreCase)
                && ReadText(cpg, Encoding.ASCII).Trim().Equals("EUC-KR", StringComparison.OrdinalIgnoreCase),
            "SagajeongTrafficShapefileCrsChanged:" + spec.Key);

        using var shapeStream = shp.Open();
        using var dbfStream = dbf.Open();
        var shapes = new ShapefileReader(shapeStream, spec.Key == "lane" ? 3 : 1);
        var rows = new DbfReader(dbfStream, Encoding.GetEncoding(949));
        var candidates = new List<Candidate>();
        var total = 0;
        while (shapes.TryRead(out var shape))
        {
            Require(rows.TryRead(out var row), "SagajeongTrafficDbfEndedEarly:" + spec.Key);
            total++;
            if (row.Deleted || shape.ShapeType == 0) continue;
            var selected = spec.Key == "lane"
                ? Scope.Intersects(shape.Bounds)
                : Scope.Contains(shape.X, shape.Y);
            if (selected)
                candidates.Add(new Candidate(spec, shape.RecordNumber, shape, row.Values));
        }
        Require(!rows.TryRead(out _), "SagajeongTrafficShpEndedEarly:" + spec.Key);
        Require(total == rows.RecordCount, "SagajeongTrafficShpDbfCountMismatch:" + spec.Key);
        return new ParsedSource(total, candidates);
    }

    private static ParsedSource ParseSignalCsv(string path, SourceSpec spec)
    {
        using var reader = new StreamReader(path, Encoding.GetEncoding(949), true);
        using var rows = CsvRows(reader).GetEnumerator();
        Require(rows.MoveNext(), "SagajeongTrafficSignalCsvEmpty");
        var headers = rows.Current;
        string[] expected =
        [
            "부착대관리번호", "상태", "부착방식", "부착대길이", "고가", "신호등종류", "배면등종류", "설치일", "교체일",
            "지주관리번호", "신호발광구분", "제조회사", "작업구분", "표출구분", "신규정규화ID", "공사관리번호",
            "부착대구관리번호", "이력ID", "위치정보", "X좌표", "Y좌표", "공사형태", "공간데이터", "부착대방향", "신호등수량", "배면등수량"
        ];
        Require(headers.SequenceEqual(expected, StringComparer.Ordinal), "SagajeongTrafficSignalCsvSchemaChanged");
        var candidates = new List<Candidate>();
        var total = 0;
        while (rows.MoveNext())
        {
            var values = rows.Current;
            Require(values.Length == expected.Length, "SagajeongTrafficSignalCsvRowInvalid");
            total++;
            var xParsed = double.TryParse(values[19], NumberStyles.Float, CultureInfo.InvariantCulture, out var x);
            var yParsed = double.TryParse(values[20], NumberStyles.Float, CultureInfo.InvariantCulture, out var y);
            Require(xParsed && yParsed && double.IsFinite(x) && double.IsFinite(y),
                "SagajeongTrafficSignalCoordinateInvalid");
            Require(values[22].Contains(",2093,", StringComparison.Ordinal),
                "SagajeongTrafficSignalEmbeddedSridChanged");
            if (!Scope.Contains(x, y)) continue;
            var fields = expected.Select((name, index) => (name, values[index]))
                .ToDictionary(item => item.name, item => item.Item2, StringComparer.Ordinal);
            var shape = new ShapeRecord(total, 1, x, y, new Bounds(x, y, x, y), 1);
            candidates.Add(new Candidate(spec, total, shape, fields));
        }
        return new ParsedSource(total, candidates);
    }

    private static 외부데이터정규화Record ToRecord(Candidate candidate, AcquisitionReceipt receipt)
    {
        var spec = candidate.Spec;
        var collected = receipt.AcquiredAtUtc;
        string stableId;
        string metric;
        string dimension;
        string text;
        string spatial;
        string limitation;
        switch (spec.Key)
        {
            case "lane":
            {
                var manager = Field(candidate, "MGRNU");
                var history = Field(candidate, "HISID");
                stableId = $"candidate:seoul-tgis:lane:{manager.ToLowerInvariant()}:history:{history}";
                metric = "lane-line-geometry-candidate";
                dimension = $"source-record={candidate.SourceRecordNumber};manager={manager};history={history}";
                text = JsonSerializer.Serialize(new
                {
                    sourceRecordNumber = candidate.SourceRecordNumber,
                    managementNumber = manager,
                    historyId = history,
                    kindCode = Field(candidate, "A058_KND_C"),
                    subKindCode = OptionalField(candidate, "A058_KND2_"),
                    formCode = OptionalField(candidate, "FRM_CDE"),
                    roadClassCode = OptionalField(candidate, "ROD_GBN_CD"),
                    reportedLengthMeters = OptionalDecimal(candidate, "LENX"),
                    boundsEpsg5186 = candidate.Shape.Bounds,
                    candidate.Shape.VertexCount
                });
                spatial = "epsg5186-polyline-bbox-candidate";
                limitation = CommonLimit + ";BboxCandidate;NoLaneCenterInference";
                break;
            }
            case "direction":
            {
                var manager = Field(candidate, "MGRNU");
                var history = Field(candidate, "HISID");
                stableId = $"candidate:seoul-tgis:direction:{manager.ToLowerInvariant()}:history:{history}";
                metric = "pavement-direction-point-candidate";
                dimension = $"source-record={candidate.SourceRecordNumber};manager={manager};history={history}";
                text = JsonSerializer.Serialize(new
                {
                    sourceRecordNumber = candidate.SourceRecordNumber,
                    managementNumber = manager,
                    historyId = history,
                    directionDegrees = RequiredDecimal(candidate, "DRN"),
                    kindCode = Field(candidate, "A055_KND_C"),
                    pointEpsg5186 = new[] { candidate.Shape.X, candidate.Shape.Y }
                });
                spatial = "epsg5186-point-candidate";
                limitation = CommonLimit + ";DirectionCodeUninterpreted";
                break;
            }
            case "intersection":
            {
                var manager = Field(candidate, "MGRNU");
                var css = Field(candidate, "CSS_NUM");
                stableId = $"candidate:seoul-tgis:intersection:{css}:manager:{manager.ToLowerInvariant()}";
                metric = "intersection-point-candidate";
                dimension = $"source-record={candidate.SourceRecordNumber};manager={manager};css={css}";
                text = JsonSerializer.Serialize(new
                {
                    sourceRecordNumber = candidate.SourceRecordNumber,
                    managementNumber = manager,
                    intersectionNumber = css,
                    name = Field(candidate, "CSS_NAM"),
                    kindCode = Field(candidate, "A008_KND_C"),
                    linkedIntersectionNumber = OptionalDecimal(candidate, "LK_CS_CDE"),
                    pointEpsg5186 = new[] { candidate.Shape.X, candidate.Shape.Y }
                });
                spatial = "epsg5186-point-candidate";
                limitation = CommonLimit + ";NoSignalPhaseOrTiming";
                break;
            }
            case "controller":
            {
                var manager = Field(candidate, "MGRNU");
                var css = Field(candidate, "CSS_NUM");
                stableId = $"candidate:seoul-tgis:signal-controller:{css}:manager:{manager.ToLowerInvariant()}";
                metric = "signal-controller-point-candidate";
                dimension = $"source-record={candidate.SourceRecordNumber};manager={manager};css={css}";
                text = JsonSerializer.Serialize(new
                {
                    sourceRecordNumber = candidate.SourceRecordNumber,
                    managementNumber = manager,
                    controllerManagementNumber = OptionalField(candidate, "CTR_MGRNU"),
                    intersectionNumber = css,
                    name = Field(candidate, "CTR_NAM"),
                    kindCode = Field(candidate, "A061_KND_C"),
                    pointEpsg5186 = new[] { candidate.Shape.X, candidate.Shape.Y }
                });
                spatial = "epsg5186-point-candidate";
                limitation = CommonLimit + ";NoSignalPhaseOrTiming";
                break;
            }
            case "signal":
            {
                var manager = Field(candidate, "부착대관리번호");
                stableId = "candidate:seoul-tgis:signal-head-arm:" + manager.ToLowerInvariant();
                metric = "signal-head-arm-point-candidate";
                dimension = $"source-row={candidate.SourceRecordNumber};manager={manager}";
                text = JsonSerializer.Serialize(new
                {
                    sourceRowNumber = candidate.SourceRecordNumber,
                    mountingManagementNumber = manager,
                    legacyMountingManagementNumber = OptionalField(candidate, "부착대구관리번호"),
                    supportManagementNumber = OptionalField(candidate, "지주관리번호"),
                    signalTypeCode = Field(candidate, "신호등종류"),
                    mountingDirectionDegrees = RequiredDecimal(candidate, "부착대방향"),
                    signalCount = RequiredDecimal(candidate, "신호등수량"),
                    sourceNumericPoint = new[] { candidate.Shape.X, candidate.Shape.Y },
                    portalDeclaredCrs = "EPSG:5186",
                    embeddedGeometrySrid = 2093,
                    crsResolutionStatus = "Unresolved"
                });
                spatial = "crs-conflict-numeric-envelope-candidate";
                limitation = CommonLimit + ";EmbeddedSridMismatch;NumericEnvelopeOnly;RegionMembershipUnresolved;NoSignalPhaseOrTiming";
                break;
            }
            default:
                throw new InvalidDataException("SagajeongTrafficSourceKeyInvalid");
        }

        Require(text.Length <= 2_000 && limitation.Length <= 240 && dimension.Length <= 500,
            "SagajeongTrafficDatabaseFieldTooLong:" + spec.Key);
        var regionStableId = spec.Key == "signal"
            ? CrsUnresolvedReviewRegionStableId
            : DbRegionStableId;
        return new 외부데이터정규화Record
        {
            RecordKey = 외부데이터RecordKey.Create(SourceId, spec.DatasetId, regionStableId,
                metric, spec.FileModifiedDate, dimension),
            StableId = stableId,
            SourceId = SourceId,
            DatasetId = spec.DatasetId,
            RegionStableId = regionStableId,
            MetricCode = metric,
            NumericValue = null,
            TextValue = text,
            UnitCode = "static-traffic-observation-json",
            EvidenceAsOfUtc = spec.FileModifiedDate,
            CollectedAtUtc = collected,
            SpatialPrecisionCode = spatial,
            TemporalPrecisionCode = "portal-file-modified-date-not-observation-date",
            QualityCode = Quality,
            LimitationCode = limitation,
            DimensionKey = dimension,
            SourceVersion = $"portal-file:{spec.FileModifiedDate:yyyy-MM-dd};sha256:{spec.ExpectedHash.ToLowerInvariant()}",
            DataRevision = DataRevision,
            FirstSeenAtUtc = collected,
            LastSeenAtUtc = collected
        };
    }

    private static int SelfTest(FrozenAcquisition frozen, ParsedData parsed)
    {
        var tests = 0;
        void Test(bool condition, string code) { Require(condition, "SagajeongTrafficSelfTest:" + code); tests++; }
        Test(parsed.Records.Count == 1_523, "SelectedTotal");
        Test(parsed.Counts["lane"] == new SourceCount(341_957, 1_039), "LaneCount");
        Test(parsed.Counts["direction"] == new SourceCount(157_889, 318), "DirectionCount");
        Test(parsed.Counts["intersection"] == new SourceCount(8_097, 25), "IntersectionCount");
        Test(parsed.Counts["controller"] == new SourceCount(4_383, 16), "ControllerCount");
        Test(parsed.Counts["signal"] == new SourceCount(44_020, 125), "SignalCount");
        var laneCandidates = parsed.Candidates.Where(item => item.Spec.Key == "lane").ToArray();
        Test(laneCandidates.Select(item => Field(item, "MGRNU")).Distinct(StringComparer.Ordinal).Count() == 1_036
             && laneCandidates.GroupBy(item => Field(item, "MGRNU"), StringComparer.Ordinal).Count(group => group.Count() > 1) == 3,
            "LaneManagementIdentityCount");
        Test(Math.Abs((Scope.MaxX - Scope.MinX) - 1_034.423d) < 0.001d
             && Math.Abs((Scope.MaxY - Scope.MinY) - 1_022.067d) < 0.001d,
            "NominalOneKilometerEnvelopeDimensions");
        Test(parsed.Records.All(item => item.QualityCode == Quality
            && item.LimitationCode.Contains("NoRuntime", StringComparison.Ordinal)
            && item.LimitationCode.Contains("NoTraversalAuthority", StringComparison.Ordinal)
            && item.LimitationCode.Contains("NoGameplayAuthority", StringComparison.Ordinal)), "AuthorityBoundary");
        Test(parsed.Records.Where(item => item.DatasetId != "oa-15546-signal-head-arm")
            .All(item => item.RegionStableId == DbRegionStableId
                && item.NumericValue == null && item.TextValue.Length <= 2_000), "ResolvedProjectionBoundary");
        Test(parsed.Records.Where(item => item.DatasetId == "oa-15546-signal-head-arm")
            .All(item => item.RegionStableId == CrsUnresolvedReviewRegionStableId
                && item.NumericValue == null && item.TextValue.Length <= 2_000), "CrsUnresolvedProjectionBoundary");
        Test(parsed.Candidates.Where(item => item.Spec.Key != "lane")
            .All(item => Scope.Contains(item.Shape.X, item.Shape.Y)), "PointScope");
        Test(parsed.Candidates.Where(item => item.Spec.Key == "lane")
            .All(item => Scope.Intersects(item.Shape.Bounds)), "LaneScope");
        Test(parsed.Candidates.Where(item => item.Spec.Key == "direction")
            .All(item => RequiredDecimal(item, "DRN") is >= 0m and <= 360m), "DirectionDegrees");
        Test(parsed.Candidates.Count(item => item.Spec.Key == "intersection"
            && Field(item, "CSS_NUM") == "3205" && Field(item, "CSS_NAM") == "사가정역") == 1, "SagajeongIntersection");
        Test(parsed.Candidates.Count(item => item.Spec.Key == "controller"
            && Field(item, "CSS_NUM") == "3205" && Field(item, "CTR_NAM") == "사가정역") == 1, "SagajeongController");
        Test(parsed.Candidates.Where(item => item.Spec.Key == "signal")
            .All(item => Field(item, "부착대방향").Length > 0 && Field(item, "신호등수량").Length > 0), "SignalOrientation");
        Test(frozen.Receipt.Sources.Single(item => item.Key == "signal").CrsNote.Contains("Srid2093", StringComparison.Ordinal)
            && parsed.Records.Where(item => item.DatasetId == "oa-15546-signal-head-arm")
                .All(item => item.SpatialPrecisionCode == "crs-conflict-numeric-envelope-candidate"
                    && item.LimitationCode.Contains("EmbeddedSridMismatch", StringComparison.Ordinal)
                    && item.LimitationCode.Contains("RegionMembershipUnresolved", StringComparison.Ordinal)
                    && !item.TextValue.Contains("pointEpsg5186", StringComparison.Ordinal)), "SignalCrsMismatchRetained");
        Test(parsed.Records.All(item => !item.TextValue.Contains("JIBUN", StringComparison.OrdinalIgnoreCase)
            && !item.TextValue.Contains("공간데이터", StringComparison.Ordinal)), "AddressAndRawGeometryOmitted");
        Test(parsed.Records.Select(item => item.RecordKey).SequenceEqual(
            parsed.Records.OrderBy(item => item.DatasetId, StringComparer.Ordinal)
                .ThenBy(item => item.StableId, StringComparer.Ordinal)
                .ThenBy(item => item.DimensionKey, StringComparer.Ordinal)
                .Select(item => item.RecordKey)), "DeterministicOrder");
        return tests;
    }

    private static async Task ApplyAsync(
        string root,
        DbContextOptions<PublicDataIngestionDbContext> options,
        FrozenAcquisition frozen,
        ParsedData parsed,
        Dictionary<string, object?> result)
    {
        await using var db = new PublicDataIngestionDbContext(options);
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT GET_LOCK('mirror:public-data:sagajeong-static-traffic-r1',0)";
        Require(Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 1,
            "SagajeongTrafficImportBusy");
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            result["databaseWriteAttempted"] = true;
            var service = new 평창군공공공간원본등록Service(db);
            var registrations = new Dictionary<string, 공공공간원본등록Result>(StringComparer.Ordinal);
            foreach (var spec in Sources)
            {
                var registration = await service.RegisterFileAsync(
                    frozen.Paths[spec.Key],
                    new 공공공간원본등록Request(
                        SourceId, spec.DatasetId,
                        $"portal-file:{spec.FileModifiedDate:yyyy-MM-dd};sha256:{spec.ExpectedHash.ToLowerInvariant()}",
                        DataRevision, spec.FileModifiedDate, spec.ContentType,
                        RelativeFolder + "/" + spec.FileName));
                registrations.Add(spec.Key, registration);
            }

            foreach (var record in parsed.Records)
            {
                var key = Sources.Single(item => item.DatasetId == record.DatasetId).Key;
                record.RawSnapshotId = registrations[key].RawSnapshotId;
            }
            var before = await ReadExistingAsync(db, parsed.Records.Select(item => item.RecordKey).ToArray());
            Require(before.All(item => parsed.Records.Any(candidate => Equivalent(item, candidate))),
                "SagajeongTrafficExistingRecordConflict");

            var inserted = 0;
            var existing = 0;
            var insertedByDataset = new Dictionary<string, int>(StringComparer.Ordinal);
            var existingByDataset = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var group in parsed.Records.GroupBy(item => item.DatasetId, StringComparer.Ordinal))
            {
                var stored = await new EfExternalDataIngestionStore(db).UpsertNormalizedAsync(group.ToArray());
                Require(stored.UpdatedCount == 0, "SagajeongTrafficUnexpectedUpdate");
                inserted += stored.InsertedCount;
                existing += stored.ExistingCount;
                insertedByDataset.Add(group.Key, stored.InsertedCount);
                existingByDataset.Add(group.Key, stored.ExistingCount);
            }

            foreach (var spec in Sources)
            {
                var registration = registrations[spec.Key];
                if (!registration.Inserted) continue;
                var snapshot = await db.RawSnapshots.SingleAsync(item => item.Id == registration.RawSnapshotId);
                snapshot.CollectedAtUtc = frozen.Receipt.AcquiredAtUtc;
                var run = await db.IngestionRuns.SingleAsync(item => item.Id == snapshot.FirstCollectionRunId);
                run.StatusCode = 외부데이터수집StatusCodes.Partial;
                run.FetchedCount = spec.TotalRows;
                run.NormalizedCount = spec.SelectedRows;
                run.InsertedCount = insertedByDataset[spec.DatasetId];
                run.ExistingCount = existingByDataset[spec.DatasetId];
                run.ErrorCode = Quality;
                run.ErrorSummary = "Official whole-file static traffic observation cropped to the frozen Sagajeong bbox; cross-vintage IDs/geometry require human review; no publication, runtime, traversal or gameplay authority.";
            }
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            result["committed"] = true;
            result["insertedCount"] = inserted;
            result["existingCount"] = existing;
            result["rawInsertedCount"] = registrations.Values.Count(item => item.Inserted);
        }
        finally
        {
            command.CommandText = "SELECT RELEASE_LOCK('mirror:public-data:sagajeong-static-traffic-r1')";
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
        var scopedCount = await db.NormalizedRecords.AsNoTracking().CountAsync(item =>
            item.SourceId == SourceId
            && item.RegionStableId == DbRegionStableId
            && item.DataRevision == DataRevision);
        var unresolvedCount = await db.NormalizedRecords.AsNoTracking().CountAsync(item =>
            item.SourceId == SourceId
            && item.RegionStableId == CrsUnresolvedReviewRegionStableId
            && item.DataRevision == DataRevision);
        Require(stored.Count == parsed.Records.Count
                && scopedCount == 1_398
                && unresolvedCount == 125
                && stored.All(item => parsed.Records.Any(candidate => Equivalent(item, candidate)))
                && stored.All(item => item.RawSnapshot is not null
                    && Sources.Any(spec => spec.DatasetId == item.DatasetId
                        && item.RawSnapshot.ContentHashSha256.Equals(spec.ExpectedHash, StringComparison.OrdinalIgnoreCase))),
            "SagajeongTrafficReadbackMismatch");
        // List.Contains keeps this translatable for the current EF provider; arrays can bind
        // to the .NET span overload during parameter evaluation.
        var datasetIds = Sources.Select(item => item.DatasetId).ToList();
        var snapshots = await db.RawSnapshots.AsNoTracking()
            .Include(item => item.FirstCollectionRun)
            .Where(item => item.SourceId == SourceId && datasetIds.Contains(item.DatasetId))
            .ToListAsync();
        foreach (var spec in Sources)
        {
            var source = snapshots.SingleOrDefault(item => item.DatasetId == spec.DatasetId
                && item.ContentHashSha256 == spec.ExpectedHash.ToLowerInvariant());
            Require(source is not null
                    && source.ContentLength == spec.ExpectedLength
                    && source.StorageContainer == "local-private-public-spatial"
                    && source.StorageObjectName == (RelativeFolder + "/" + spec.FileName).Replace('\\', '/')
                    && source.FirstCollectionRun?.StatusCode == 외부데이터수집StatusCodes.Partial
                    && source.FirstCollectionRun.ErrorCode == Quality
                    && source.FirstCollectionRun.FetchedCount == spec.TotalRows
                    && source.FirstCollectionRun.NormalizedCount == spec.SelectedRows
                    && source.FirstCollectionRun.InsertedCount == spec.SelectedRows,
                "SagajeongTrafficRawReadbackMismatch:" + spec.Key);
        }
        result["verifiedRows"] = stored.Count;
        result["verifiedRawSnapshots"] = Sources.Length;
        result["independentReadback"] = true;
        result["sourceHashes"] = frozen.Receipt.Sources.ToDictionary(item => item.Key, item => item.Sha256);
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
                .Where(item => batch.Contains(item.RecordKey)).ToListAsync());
        }
        return result;
    }

    // 수집 시각은 같은 source bytes를 다시 받은 관찰 시각이다. 의미 동등성에는
    // 포함하지 않고 저장소의 first/last-seen 갱신에 맡긴다.
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

    private static void SetSummary(
        Dictionary<string, object?> result,
        FrozenAcquisition frozen,
        ParsedData parsed)
    {
        result["sourceFiles"] = Sources.Length;
        result["sourceRows"] = parsed.Counts.Values.Sum(item => item.TotalRows);
        result["selectedRows"] = parsed.Records.Count;
        result["crsResolvedCandidateRows"] = parsed.Records.Count(item => item.DatasetId != "oa-15546-signal-head-arm");
        result["crsUnresolvedNumericEnvelopeRows"] = parsed.Records.Count(item => item.DatasetId == "oa-15546-signal-head-arm");
        result["selectionEnvelopeMeters"] = new { width = Scope.MaxX - Scope.MinX, height = Scope.MaxY - Scope.MinY };
        result["selectedByLayer"] = parsed.Counts.ToDictionary(item => item.Key, item => item.Value.SelectedRows);
        result["stationStableId"] = StationStableId;
        result["worldRegionStableId"] = WorldRegionStableId;
        result["crsUnresolvedReviewRegionStableId"] = CrsUnresolvedReviewRegionStableId;
        result["selectionCoordinateReferenceSystem"] = "EPSG:5186";
        result["sourceCoordinateReferenceSystemStatus"] = "FourShapefileSourcesEpsg5186;SignalPortalEpsg5186EmbeddedSrid2093Unresolved";
        result["reviewStatus"] = Quality;
        result["distributionApproved"] = false;
        result["runtimeAuthorized"] = false;
        result["traversalAuthorized"] = false;
        result["folder"] = Path.GetRelativePath(Path.GetFullPath(Path.Combine(frozen.Folder, "..", "..", "..", "..")), frozen.Folder).Replace('\\', '/');
    }

    private static async Task<DownloadedFile> DownloadBoundedAsync(
        HttpResponseMessage response,
        string path,
        CancellationToken cancellationToken)
    {
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81_920, true);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81_920];
        long total = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            total += read;
            Require(total <= MaximumDownloadBytes, "SagajeongTrafficDownloadTooLarge");
            hash.AppendData(buffer, 0, read);
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        await output.FlushAsync(cancellationToken);
        return new DownloadedFile(total, Convert.ToHexString(hash.GetHashAndReset()));
    }

    private static string FindDownloadSequence(string page, string fileName)
    {
        var pattern = "title=\"" + Regex.Escape(fileName)
            + "\"[^>]*onclick=\"javascript:downloadFile\\('(?<value>[0-9]+)'\\);\"";
        return RequiredMatch(page, pattern, "value");
    }

    private static string RequiredMatch(string input, string pattern, string group)
    {
        var match = Regex.Match(input, pattern, RegexOptions.CultureInvariant);
        Require(match.Success, "SagajeongTrafficOfficialPageDownloadContractChanged");
        return match.Groups[group].Value;
    }

    private static string PageUrl(SourceSpec spec)
        => $"https://data.seoul.go.kr/dataList/{spec.InfId}/S/1/datasetView.do";

    private static string SourceCoordinateReferenceSystem(SourceSpec spec)
        => spec.Key == "signal"
            ? "PortalDeclaredEPSG:5186;EmbeddedSRID:2093"
            : "EPSG:5186";

    private static string SourceCrsNote(SourceSpec spec)
        => spec.Key == "signal"
            ? "PortalDeclaresEpsg5186;EmbeddedGeometryDeclaresSrid2093;NumericXYSelectionPendingReview"
            : "PortalAndPrjDeclareEpsg5186";

    private static string Field(Candidate candidate, string name)
        => candidate.Fields.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new InvalidDataException("SagajeongTrafficRequiredFieldMissing:" + candidate.Spec.Key + ":" + name);

    private static string? OptionalField(Candidate candidate, string name)
        => candidate.Fields.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    private static decimal RequiredDecimal(Candidate candidate, string name)
        => decimal.TryParse(Field(candidate, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidDataException("SagajeongTrafficNumberInvalid:" + candidate.Spec.Key + ":" + name);

    private static decimal? OptionalDecimal(Candidate candidate, string name)
        => candidate.Fields.TryGetValue(name, out var raw)
           && decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static ZipArchiveEntry SingleEntry(ZipArchive archive, string extension)
    {
        var entries = archive.Entries.Where(item => item.FullName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)).ToArray();
        Require(entries.Length == 1 && entries[0].Length > 0, "SagajeongTrafficArchiveEntryInvalid:" + extension);
        return entries[0];
    }

    private static string ReadText(ZipArchiveEntry entry, Encoding encoding)
    {
        using var reader = new StreamReader(entry.Open(), encoding, true);
        return reader.ReadToEnd();
    }

    private static IEnumerable<string[]> CsvRows(TextReader reader)
    {
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        while (reader.Read() is var value && value >= 0)
        {
            var character = (char)value;
            if (quoted)
            {
                if (character == '"' && reader.Peek() == '"') { reader.Read(); field.Append('"'); }
                else if (character == '"') quoted = false;
                else field.Append(character);
                continue;
            }
            if (character == '"' && field.Length == 0) quoted = true;
            else if (character == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (character == '\n')
            {
                row.Add(field.ToString().TrimEnd('\r')); field.Clear();
                yield return row.ToArray(); row.Clear();
            }
            else if (character != '\r') field.Append(character);
        }
        Require(!quoted, "SagajeongTrafficCsvUnterminatedQuote");
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); yield return row.ToArray(); }
    }

    private static DateTimeOffset RoundToMilliseconds(DateTimeOffset value)
        => DateTimeOffset.FromUnixTimeMilliseconds(value.ToUnixTimeMilliseconds());

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }

    private sealed class DbfReader
    {
        private readonly Stream stream;
        private readonly Encoding encoding;
        private readonly FieldSpec[] fields;
        private readonly int recordLength;
        private int remaining;

        public DbfReader(Stream stream, Encoding encoding)
        {
            this.stream = stream;
            this.encoding = encoding;
            var header = new byte[32];
            stream.ReadExactly(header);
            RecordCount = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4, 4));
            var headerLength = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(8, 2));
            recordLength = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(10, 2));
            Require(RecordCount > 0 && RecordCount <= 1_000_000 && headerLength >= 33 && recordLength >= 2,
                "SagajeongTrafficDbfHeaderInvalid");
            var fieldCount = (headerLength - 33) / 32;
            Require(fieldCount > 0 && fieldCount <= 100 && 32 + fieldCount * 32 + 1 <= headerLength,
                "SagajeongTrafficDbfFieldCountInvalid");
            var list = new List<FieldSpec>(fieldCount);
            var offset = 1;
            for (var index = 0; index < fieldCount; index++)
            {
                var descriptor = new byte[32];
                stream.ReadExactly(descriptor);
                var name = Encoding.ASCII.GetString(descriptor, 0, 11).TrimEnd('\0');
                var length = descriptor[16];
                Require(name.Length > 0 && length > 0, "SagajeongTrafficDbfFieldInvalid");
                list.Add(new FieldSpec(name, offset, length));
                offset += length;
            }
            Require(stream.ReadByte() == 0x0D && offset == recordLength, "SagajeongTrafficDbfLayoutInvalid");
            var consumed = 32 + fieldCount * 32 + 1;
            if (headerLength > consumed)
            {
                var padding = new byte[headerLength - consumed];
                stream.ReadExactly(padding);
            }
            fields = list.ToArray();
            remaining = RecordCount;
        }

        public int RecordCount { get; }

        public bool TryRead(out DbfRow row)
        {
            if (remaining == 0) { row = default!; return false; }
            var bytes = new byte[recordLength];
            stream.ReadExactly(bytes);
            remaining--;
            Require(bytes[0] is (byte)' ' or (byte)'*', "SagajeongTrafficDbfDeletionFlagInvalid");
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var field in fields)
                values.Add(field.Name, encoding.GetString(bytes, field.Offset, field.Length).Trim('\0', ' '));
            row = new DbfRow(bytes[0] == (byte)'*', values);
            return true;
        }
    }

    private sealed class ShapefileReader
    {
        private readonly Stream stream;
        private readonly int requiredShapeType;
        private int remainingBytes;

        public ShapefileReader(Stream stream, int requiredShapeType)
        {
            this.stream = stream;
            this.requiredShapeType = requiredShapeType;
            var header = new byte[100];
            stream.ReadExactly(header);
            Require(BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(0, 4)) == 9994
                    && BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(28, 4)) == 1000
                    && BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(32, 4)) == requiredShapeType,
                "SagajeongTrafficShapefileHeaderInvalid");
            var totalBytes = checked(BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(24, 4)) * 2);
            Require(totalBytes >= 100, "SagajeongTrafficShapefileLengthInvalid");
            remainingBytes = totalBytes - 100;
        }

        public int ExpectedRecords { get; private set; }

        public bool TryRead(out ShapeRecord shape)
        {
            if (remainingBytes == 0) { shape = default!; return false; }
            Require(remainingBytes >= 8, "SagajeongTrafficShapefileRecordHeaderTruncated");
            var header = new byte[8];
            stream.ReadExactly(header);
            var recordNumber = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(0, 4));
            var length = checked(BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(4, 4)) * 2);
            Require(length >= 4 && length <= 16 * 1024 * 1024 && remainingBytes >= 8 + length,
                "SagajeongTrafficShapefileRecordLengthInvalid");
            var body = new byte[length];
            stream.ReadExactly(body);
            remainingBytes -= 8 + length;
            ExpectedRecords++;
            Require(recordNumber == ExpectedRecords, "SagajeongTrafficShapefileRecordOrderInvalid");
            var shapeType = BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(0, 4));
            if (shapeType == 0)
            {
                shape = new ShapeRecord(recordNumber, 0, 0, 0, default, 0);
                return true;
            }
            Require(shapeType == requiredShapeType, "SagajeongTrafficShapefileMixedShapeType");
            if (shapeType == 1)
            {
                Require(body.Length >= 20, "SagajeongTrafficPointTruncated");
                var x = BitConverter.ToDouble(body, 4);
                var y = BitConverter.ToDouble(body, 12);
                Require(double.IsFinite(x) && double.IsFinite(y), "SagajeongTrafficPointInvalid");
                shape = new ShapeRecord(recordNumber, shapeType, x, y, new Bounds(x, y, x, y), 1);
                return true;
            }

            Require(body.Length >= 44, "SagajeongTrafficPolylineTruncated");
            var bounds = new Bounds(BitConverter.ToDouble(body, 4), BitConverter.ToDouble(body, 12),
                BitConverter.ToDouble(body, 20), BitConverter.ToDouble(body, 28));
            var parts = BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(36, 4));
            var points = BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(40, 4));
            Require(parts > 0 && points >= 2 && 44L + parts * 4L + points * 16L <= body.Length
                    && bounds.IsFiniteAndOrdered,
                "SagajeongTrafficPolylineLayoutInvalid");
            shape = new ShapeRecord(recordNumber, shapeType, double.NaN, double.NaN, bounds, points);
            return true;
        }
    }

    private sealed record SourceSpec(
        string Key,
        string InfId,
        string DatasetTitle,
        string FileName,
        string DatasetId,
        DateTimeOffset FileModifiedDate,
        long ExpectedLength,
        string ExpectedHash,
        string ContentType,
        int TotalRows,
        int SelectedRows);

    private sealed record SourceReceipt(
        string Key,
        string InfId,
        string DatasetTitle,
        string OfficialPageUrl,
        string FileName,
        DateTimeOffset FileModifiedDate,
        string CoordinateReferenceSystem,
        long ContentLength,
        string Sha256,
        string DownloadSequence,
        string InfSeq,
        string LicenseCode,
        int TotalRows,
        int SelectedRows,
        string CrsNote);

    private sealed record AcquisitionReceipt(
        string SchemaVersion,
        DateTimeOffset AcquiredAtUtc,
        string StationStableId,
        string WorldRegionStableId,
        string DbRegionStableId,
        string CrsUnresolvedReviewRegionStableId,
        Wgs84Bounds Wgs84Bounds,
        Bounds Epsg5186Bounds,
        string SelectionMethod,
        bool DistributionApproved,
        bool RuntimeAuthorized,
        bool TraversalAuthorized,
        string ReviewStatus,
        IReadOnlyList<SourceReceipt> Sources);

    private sealed record FrozenAcquisition(
        string Folder,
        AcquisitionReceipt Receipt,
        IReadOnlyDictionary<string, string> Paths);

    private sealed record ParsedData(
        IReadOnlyList<Candidate> Candidates,
        IReadOnlyList<외부데이터정규화Record> Records,
        IReadOnlyDictionary<string, SourceCount> Counts);

    private sealed record ParsedSource(int TotalRows, IReadOnlyList<Candidate> Candidates);
    private sealed record Candidate(SourceSpec Spec, int SourceRecordNumber, ShapeRecord Shape, IReadOnlyDictionary<string, string> Fields);
    private sealed record ShapeRecord(int RecordNumber, int ShapeType, double X, double Y, Bounds Bounds, int VertexCount);
    private sealed record DbfRow(bool Deleted, IReadOnlyDictionary<string, string> Values);
    private sealed record FieldSpec(string Name, int Offset, int Length);
    private sealed record DownloadedFile(long Length, string Hash);
    private sealed record SourceCount(int TotalRows, int SelectedRows);
    private sealed record Wgs84Bounds(double MinLongitude, double MinLatitude, double MaxLongitude, double MaxLatitude);

    private readonly record struct Bounds(double MinX, double MinY, double MaxX, double MaxY)
    {
        public bool Contains(double x, double y) => x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
        public bool Intersects(Bounds other) => !(other.MaxX < MinX || other.MinX > MaxX || other.MaxY < MinY || other.MinY > MaxY);
        public bool IsFiniteAndOrdered => double.IsFinite(MinX) && double.IsFinite(MinY)
                                          && double.IsFinite(MaxX) && double.IsFinite(MaxY)
                                          && MinX <= MaxX && MinY <= MaxY;
    }
}
