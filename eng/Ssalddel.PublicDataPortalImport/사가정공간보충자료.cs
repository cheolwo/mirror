using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

internal static class 사가정공간보충자료
{
    private const string RelativeFolder = "artifacts/local/public-data/sagajeong-spatial-supplement-20260914-r1";
    private const string ReceiptFileName = "acquisition.json";
    private const string CoverageTreeFileName = "coverage-tree.json";
    private const string SourceId = "seoul-open-data-sagajeong-spatial-supplement";
    private const string DataRevision = "sagajeong-spatial-supplement-20260914.r1";
    private const string StationStableId = "station:kr:kric:s1107:0722";
    private const string RegionStableId = "area:kr:seoul:jungnang:sagajeong-station-0722-one-kilometer";
    private const string Quality = "PendingHumanReview";
    private const string CommonLimit = "PrivateReviewOnly;NoPublication;NoRuntime;NoTraversalAuthority;NoGameplayAuthority";
    private const string SheetBase = "https://data.seoul.go.kr/dataList/dataView.do?onepagerow=100&srvType=S&serviceKind=0&ssUserId=SAMPLE_VIEW&strWhere=&strOrderby=";
    private const string DownloadEndpoint = "https://datafile.seoul.go.kr/bigfile/iot/inf/nio_download.do?&useCache=false";
    private const long MaximumDownloadBytes = 32L * 1024 * 1024;

    private const double MinLongitude = 127.0825d;
    private const double MinLatitude = 37.5762d;
    private const double MaxLongitude = 127.0942d;
    private const double MaxLatitude = 37.5854d;
    private const double OriginLatitude = 37.5806971d;
    private const double OriginLongitude = 127.0884106d;
    private const double OffsetX = 550d;
    private const double OffsetZ = 8d;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly SourceDefinition[] SourceDefinitions =
    [
        new("pedestrian", "OA-21208", "서울시 자치구별 도보 네트워크 공간정보",
            "oa-21208-pedestrian-network", "walk-network.json", "application/json",
            new DateTimeOffset(2020, 12, 31, 0, 0, 0, TimeSpan.Zero),
            "HistoricalSpatialReference;SourceDescriptionSays2020;CurrentWorkTimestampIsPerRow"),
        new("elevator", "OA-21212", "서울시 지하철역 엘리베이터 위치정보",
            "oa-21212-subway-elevator", "station-elevator.json", "application/json",
            new DateTimeOffset(2020, 12, 31, 0, 0, 0, TimeSpan.Zero),
            "HistoricalAccessibilityReference;SourceDescriptionSays2020;NoExitNumber"),
        new("park", "OA-15529", "서울시 생활권계획 시설(공원) 공간정보",
            "oa-15529-living-zone-park", "UPIS_SHP_ZON216.zip", "application/zip",
            new DateTimeOffset(2018, 12, 21, 0, 0, 0, TimeSpan.Zero),
            "ReferenceOnly;NoLegalEffect;Epsg5174GeometryNormalizationDeferred"),
        new("bus-stop", "OA-15067", "서울시 버스정류소 위치정보",
            "oa-15067-bus-stop-location", "bus-stops.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            default, "OfficialFileDateFromFileName;Wgs84CoordinateValues"),
        new("roadside-tree", "OA-1325", "서울시 가로수 위치정보 (좌표계: WGS1984)",
            "oa-1325-roadside-tree-wgs84", "roadside-trees.json", "application/json",
            new DateTimeOffset(2022, 10, 14, 0, 0, 0, TimeSpan.Zero),
            "HistoricalVegetationReference;PortalDataUpdate2022-10-14")
    ];

    public static async Task RunAsync(string mode, string root, Dictionary<string, object?> result)
    {
        if (mode == "acquire")
        {
            await AcquireAsync(root, result);
            return;
        }

        Require(mode is "self-test" or "preview" or "apply" or "verify", "SagajeongSpatialSupplementModeInvalid");
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
                "SagajeongSpatialSupplementExistingRecordConflict");
            result["beforeCount"] = existing.Count;
            return;
        }

        if (mode == "apply")
            await ApplyAsync(options, frozen, parsed, result);

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
            result["blockedExternalSources"] = frozen.Receipt.BlockedExternalSources.Count;
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(folder)!);
        var staging = folder + ".acquiring-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(staging);
        try
        {
            using var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            using var http = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(45),
                MaxResponseContentBufferSize = MaximumDownloadBytes
            };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Mirror-Sagajeong-Spatial-Supplement/1.0");

            var acquiredAt = RoundToMilliseconds(DateTimeOffset.UtcNow);
            var sourceReceipts = new List<SourceReceipt>();

            var pedestrian = Source("pedestrian");
            await ValidateOfficialPageAsync(http, pedestrian, "WGS84", "공공누리 1유형");
            var pedestrianPackage = await FetchSheetRowsAsync(http, pedestrian, "EMD_NM", "면목동", 120);
            var pedestrianPath = Path.Combine(staging, pedestrian.RawFileName);
            await WritePackageAsync(pedestrianPath, pedestrianPackage);
            sourceReceipts.Add(FileReceipt(pedestrian, pedestrianPath, pedestrianPackage.TotalRows,
                CountPedestrianSelected(pedestrianPackage.Rows), pedestrian.ReferenceDate, "KOGL-Type1-Attribution"));

            var elevator = Source("elevator");
            await ValidateOfficialPageAsync(http, elevator, "WGS84", "공공누리 1유형");
            var elevatorPackage = await FetchSheetRowsAsync(http, elevator, "SBWY_STN_NM", "사가정", 4);
            var elevatorPath = Path.Combine(staging, elevator.RawFileName);
            await WritePackageAsync(elevatorPath, elevatorPackage);
            sourceReceipts.Add(FileReceipt(elevator, elevatorPath, elevatorPackage.TotalRows,
                CountPointSelected(elevatorPackage.Rows, "NODE_WKT"), elevator.ReferenceDate, "KOGL-Type1-Attribution"));

            var tree = Source("roadside-tree");
            await ValidateOfficialPageAsync(http, tree, "WGS1984", "공공누리 1유형");
            var treePackage = await FetchSheetRowsAsync(http, tree, "GU_NM", "중랑구", 100);
            var treePath = Path.Combine(staging, tree.RawFileName);
            await WritePackageAsync(treePath, treePackage);
            sourceReceipts.Add(FileReceipt(tree, treePath, treePackage.TotalRows,
                CountCoordinateSelected(treePackage.Rows, "LOT", "LAT"), tree.ReferenceDate, "KOGL-Type1-Attribution"));

            var park = Source("park");
            var parkPage = await ReadOfficialPageAsync(http, park, "EPSG:5174", "공공누리 1유형", "F");
            var parkFileName = "UPIS_SHP_ZON216.zip";
            var parkDownload = await DownloadOfficialFileAsync(http, staging, park, parkPage, parkFileName, park.RawFileName);
            var parkSelection = await FetchSheetRowsAsync(http, park, "LBL_NM", "사가정", 4);
            await WritePackageAsync(Path.Combine(staging, "park-selection.json"), parkSelection);
            sourceReceipts.Add(FileReceipt(park, parkDownload.Path, parkSelection.TotalRows,
                parkSelection.Rows.Count, park.ReferenceDate, "KOGL-Type1-Attribution", parkFileName));

            var bus = Source("bus-stop");
            var busPage = await ReadOfficialPageAsync(http, bus, "WGS84", "공공누리 1유형", "F");
            var busSourceFileName = LatestBusFileName(busPage);
            var busDownload = await DownloadOfficialFileAsync(http, staging, bus, busPage, busSourceFileName, bus.RawFileName);
            var busDate = ParseBusFileDate(busSourceFileName);
            var busCounts = CountBusWorkbook(busDownload.Path);
            sourceReceipts.Add(FileReceipt(bus, busDownload.Path, busCounts.TotalRows, busCounts.SelectedRows,
                busDate, "KOGL-Type1-Attribution", busSourceFileName));

            var receipt = new AcquisitionReceipt(
                "sagajeong-spatial-supplement-acquisition.r1", acquiredAt, StationStableId, RegionStableId,
                new Wgs84Bounds(MinLongitude, MinLatitude, MaxLongitude, MaxLatitude),
                "OfficialSourceThenFrozenSagajeongWgs84Envelope;MissingCoverageRemainsExplicit",
                false, false, false, false, Quality,
                sourceReceipts.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray(),
                [
                    new("ngii-numerical-map-v2", "https://www.data.go.kr/data/15059719/fileData.do",
                        "BlockedExternalAccess", "NationalLandInformationPlatformLoginAndDedicatedTransferRequired;NoFallback"),
                    new("ngii-dem", "https://www.data.go.kr/data/15059920/fileData.do",
                        "BlockedExternalAccess", "NationalLandInformationPlatformDownloadToolRequired;NoFallback")
                ],
                new VisualRightsReview(
                    "ExistingEligibleSourcesReused;NoNewVisualOriginalDownloaded",
                    [
                        "eng/world-seedbeds/station-landmarks/sagajeong-public-photo.collection.r2.json",
                        "eng/world-seedbeds/station-landmarks/sagajeong-data-go-kr-photo-research.collection.r1.json",
                        "eng/world-seedbeds/station-landmarks/jungnang-traditional-market-visual.collection.r1.json"
                    ],
                    "OnlyKOGLType1Or0_CC0_PublicDomain_OrProjectApprovedCCBY;CCBYSAStillBlockedByProjectPolicy"));

            await File.WriteAllTextAsync(Path.Combine(staging, ReceiptFileName),
                JsonSerializer.Serialize(receipt, JsonOptions) + Environment.NewLine, new UTF8Encoding(false));
            await WriteCoverageTreeAsync(staging, receipt);
            Directory.Move(staging, folder);

            result["sourceFiles"] = sourceReceipts.Count;
            result["downloadedBytes"] = sourceReceipts.Sum(item => item.ContentLength);
            result["selectedRows"] = sourceReceipts.Sum(item => item.SelectedRows);
            result["folder"] = RelativeFolder;
            result["blockedExternalSources"] = receipt.BlockedExternalSources.Count;
            result["newVisualOriginals"] = 0;
            result["reviewStatus"] = Quality;
        }
        catch
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            throw;
        }
    }

    private static FrozenAcquisition LoadFrozen(string root)
    {
        var folder = Path.Combine(root, RelativeFolder);
        var receiptPath = Path.Combine(folder, ReceiptFileName);
        Require(File.Exists(receiptPath), "SagajeongSpatialSupplementReceiptMissing");
        var receipt = JsonSerializer.Deserialize<AcquisitionReceipt>(File.ReadAllText(receiptPath), JsonOptions)
            ?? throw new InvalidDataException("SagajeongSpatialSupplementReceiptInvalid");
        Require(receipt.SchemaVersion == "sagajeong-spatial-supplement-acquisition.r1"
                && receipt.StationStableId == StationStableId
                && receipt.RegionStableId == RegionStableId
                && receipt.Bounds == new Wgs84Bounds(MinLongitude, MinLatitude, MaxLongitude, MaxLatitude)
                && receipt.Sources.Count == SourceDefinitions.Length
                && receipt.BlockedExternalSources.Count == 2
                && !receipt.DistributionApproved
                && !receipt.RuntimeAuthorized
                && !receipt.TraversalAuthorized
                && !receipt.GameplayAuthorized
                && receipt.ReviewStatus == Quality,
            "SagajeongSpatialSupplementReceiptContractChanged");

        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var definition in SourceDefinitions)
        {
            var source = receipt.Sources.SingleOrDefault(item => item.Key == definition.Key)
                ?? throw new InvalidDataException("SagajeongSpatialSupplementReceiptSourceMissing:" + definition.Key);
            var path = Path.Combine(folder, definition.RawFileName);
            Require(source.DatasetId == definition.DatasetId
                    && source.InfId == definition.InfId
                    && source.DatasetTitle == definition.DatasetTitle
                    && source.OfficialPageUrl == $"https://data.seoul.go.kr/dataList/{definition.InfId}/{OfficialServiceType(definition)}/1/datasetView.do"
                    && source.ContentType == definition.ContentType
                    && source.LicenseCode == "KOGL-Type1-Attribution"
                    && source.SelectedRows > 0
                    && source.TotalRows >= source.SelectedRows
                    && File.Exists(path)
                    && new FileInfo(path).Length == source.ContentLength
                    && HashFile(path).Equals(source.Sha256, StringComparison.OrdinalIgnoreCase),
                "SagajeongSpatialSupplementFrozenSourceChanged:" + definition.Key);
            paths.Add(definition.Key, path);
        }
        Require(File.Exists(Path.Combine(folder, CoverageTreeFileName)), "SagajeongSpatialSupplementCoverageTreeMissing");
        return new FrozenAcquisition(folder, receipt, paths);
    }

    private static ParsedData Parse(FrozenAcquisition frozen)
    {
        var records = new List<외부데이터정규화Record>();
        var counts = new Dictionary<string, SourceCount>(StringComparer.Ordinal);

        var pedestrianRows = ReadPackage(frozen.Paths["pedestrian"]);
        var pedestrianReceipt = Receipt(frozen, "pedestrian");
        foreach (var row in pedestrianRows.Rows.OfType<JObject>())
        {
            if (!TryWktBounds(Text(row, "NODE_TYPE") == "NODE" ? Text(row, "NODE_WKT") : Text(row, "LNKG_WKT"),
                    out var bounds, out var pointCount) || !bounds.Intersects(Scope)) continue;
            var kind = Text(row, "NODE_TYPE");
            var sourceObjectId = kind == "NODE" ? Text(row, "NODE_ID") : Text(row, "LNKG_ID");
            if (sourceObjectId.Length == 0) continue;
            var center = ToLocal((bounds.MinLatitude + bounds.MaxLatitude) / 2d,
                (bounds.MinLongitude + bounds.MaxLongitude) / 2d);
            var dimension = $"kind={kind.ToLowerInvariant()};source-id={sourceObjectId}";
            var text = JsonSerializer.Serialize(new
            {
                nodeOrLinkType = kind,
                sourceObjectId,
                nodeTypeCode = OptionalText(row, "NODE_TYPE_CD"),
                linkTypeCode = OptionalText(row, "LNKG_TYPE_CD"),
                beginNodeId = OptionalText(row, "BGNG_LNKG_ID"),
                endNodeId = OptionalText(row, "END_LNKG_ID"),
                reportedLengthMeters = OptionalDouble(row, "LNKG_LEN"),
                administrativeDong = OptionalText(row, "EMD_NM"),
                sourceWorkDateTime = OptionalText(row, "WORK_DTTM"),
                flags = new
                {
                    subwayNetwork = OptionalText(row, "SBWY_NTW"),
                    crosswalk = OptionalText(row, "CRSWK"),
                    park = OptionalText(row, "PARK"),
                    buildingInterior = OptionalText(row, "BLDG"),
                    bridge = OptionalText(row, "BRG"),
                    tunnel = OptionalText(row, "TNL"),
                    overpass = OptionalText(row, "OVRP")
                },
                boundsWgs84 = bounds,
                pointCount,
                localEnuCenter = center
            });
            records.Add(ToRecord(pedestrianReceipt, $"candidate:seoul-walk:{kind.ToLowerInvariant()}:{sourceObjectId}",
                "pedestrian-network-geometry-candidate", dimension, text,
                "wgs84-wkt-bounds-and-enu-center-candidate",
                "HistoricalSpatialReference;NoCurrentPassability;NoSidewalkWidthOrCurbAuthority"));
        }
        counts.Add("pedestrian", new SourceCount(pedestrianRows.TotalRows, records.Count));

        var elevatorStart = records.Count;
        var elevatorRows = ReadPackage(frozen.Paths["elevator"]);
        var elevatorReceipt = Receipt(frozen, "elevator");
        foreach (var row in elevatorRows.Rows.OfType<JObject>())
        {
            if (!TryWktBounds(Text(row, "NODE_WKT"), out var bounds, out _) || !bounds.Intersects(Scope)) continue;
            var sourceObjectId = Text(row, "NODE_ID");
            var local = ToLocal(bounds.MinLatitude, bounds.MinLongitude);
            var dimension = $"node-id={sourceObjectId};source-station-code={Text(row, "SBWY_STN_CD")}";
            var text = JsonSerializer.Serialize(new
            {
                sourceNodeId = sourceObjectId,
                sourceStationCode = Text(row, "SBWY_STN_CD"),
                sourceStationName = Text(row, "SBWY_STN_NM"),
                nodeTypeCode = OptionalText(row, "NODE_TYPE_CD"),
                latitude = bounds.MinLatitude,
                longitude = bounds.MinLongitude,
                localEnu = local
            });
            records.Add(ToRecord(elevatorReceipt, "candidate:seoul-subway-elevator:" + sourceObjectId,
                "subway-elevator-point-candidate", dimension, text,
                "wgs84-point-and-enu-candidate",
                "HistoricalAccessibilityReference;NoExitNumber;NoCurrentOperatingStatus"));
        }
        counts.Add("elevator", new SourceCount(elevatorRows.TotalRows, records.Count - elevatorStart));

        var parkStart = records.Count;
        var parkRows = ReadPackage(Path.Combine(frozen.Folder, "park-selection.json"));
        var parkReceipt = Receipt(frozen, "park");
        foreach (var row in parkRows.Rows.OfType<JObject>())
        {
            var sourceObjectId = Text(row, "ID");
            if (sourceObjectId.Length == 0) continue;
            var dimension = "source-id=" + sourceObjectId;
            var text = JsonSerializer.Serialize(new
            {
                sourceObjectId,
                label = OptionalText(row, "LBL_NM"),
                classification = OptionalText(row, "CLSF_NM"),
                cityPlanCode = OptionalText(row, "CTY_PLAN_CD"),
                geometrySourceFile = parkReceipt.SourceFileName,
                geometryCrs = "EPSG:5174",
                geometryNormalizationStatus = "Deferred"
            });
            records.Add(ToRecord(parkReceipt, "candidate:seoul-living-zone-park:" + sourceObjectId,
                "park-identity-and-raw-geometry-candidate", dimension, text,
                "official-identity-with-epsg5174-raw-geometry-not-normalized",
                "NoLegalEffect;ParkBoundaryInRawZip;Epsg5174TransformationPendingReview;NoEntranceOrCurrentBoundaryAuthority"));
        }
        counts.Add("park", new SourceCount(parkRows.TotalRows, records.Count - parkStart));

        var busStart = records.Count;
        var busReceipt = Receipt(frozen, "bus-stop");
        foreach (var row in ReadBusWorkbook(frozen.Paths["bus-stop"]).Rows)
        {
            if (!Scope.Contains(row.Longitude, row.Latitude)) continue;
            var local = ToLocal(row.Latitude, row.Longitude);
            var dimension = $"node-id={row.NodeId};ars-id={row.ArsId}";
            var text = JsonSerializer.Serialize(new
            {
                row.NodeId,
                row.ArsId,
                row.Name,
                row.StopType,
                row.Latitude,
                row.Longitude,
                localEnu = local
            });
            records.Add(ToRecord(busReceipt, "candidate:seoul-bus-stop:" + row.NodeId,
                "bus-stop-point-candidate", dimension, text,
                "wgs84-point-and-enu-candidate",
                "StaticLocationReference;NoRouteOrRealtimeArrivalAuthority"));
        }
        counts.Add("bus-stop", new SourceCount(busReceipt.TotalRows, records.Count - busStart));

        var treeStart = records.Count;
        var treeRows = ReadPackage(frozen.Paths["roadside-tree"]);
        var treeReceipt = Receipt(frozen, "roadside-tree");
        foreach (var row in treeRows.Rows.OfType<JObject>())
        {
            if (!TryDouble(Text(row, "LOT"), out var longitude)
                || !TryDouble(Text(row, "LAT"), out var latitude)
                || !Scope.Contains(longitude, latitude)) continue;
            var sourceObjectId = OptionalText(row, "TREE_UNQ_NO") ?? Text(row, "UNQ_NO");
            if (sourceObjectId.Length == 0) continue;
            var local = ToLocal(latitude, longitude);
            var dimension = "tree-id=" + sourceObjectId;
            var text = JsonSerializer.Serialize(new
            {
                sourceTreeId = sourceObjectId,
                species = OptionalText(row, "TREE_NM"),
                roadName = OptionalText(row, "WDTH_NM"),
                position = OptionalText(row, "PSTN"),
                heightMeters = OptionalDouble(row, "THT_HGT"),
                crownWidthMeters = OptionalDouble(row, "ASCTL_BRETH"),
                latitude,
                longitude,
                localEnu = local
            });
            records.Add(ToRecord(treeReceipt, "candidate:seoul-roadside-tree:" + StablePart(sourceObjectId),
                "roadside-tree-point-candidate", dimension, text,
                "wgs84-point-and-enu-candidate",
                "HistoricalVegetationReference;ExistenceAndConditionRequireReview;NoColliderOrOcclusionAuthority"));
        }
        counts.Add("roadside-tree", new SourceCount(treeRows.TotalRows, records.Count - treeStart));

        ResolveSourceIdentityCollisions(records);
        var ordered = records.OrderBy(item => item.DatasetId, StringComparer.Ordinal)
            .ThenBy(item => item.StableId, StringComparer.Ordinal)
            .ThenBy(item => item.DimensionKey, StringComparer.Ordinal).ToArray();
        var collision = ordered.GroupBy(item => item.RecordKey, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        Require(collision is null, "SagajeongSpatialSupplementRecordIdentityCollision:"
                                   + collision?.First().DatasetId + ":" + collision?.First().StableId);
        return new ParsedData(ordered, counts);
    }

    private static void ResolveSourceIdentityCollisions(IReadOnlyList<외부데이터정규화Record> records)
    {
        foreach (var duplicateGroup in records.GroupBy(item => item.RecordKey, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            var occurrence = 0;
            foreach (var record in duplicateGroup.OrderBy(item => item.TextValue, StringComparer.Ordinal))
            {
                occurrence++;
                record.StableId += ":source-occurrence-" + occurrence.ToString(CultureInfo.InvariantCulture);
                record.DimensionKey += ";source-occurrence=" + occurrence.ToString(CultureInfo.InvariantCulture);
                record.RecordKey = 외부데이터RecordKey.Create(record.SourceId, record.DatasetId,
                    record.RegionStableId, record.MetricCode, record.EvidenceAsOfUtc, record.DimensionKey);
            }
        }
    }

    private static 외부데이터정규화Record ToRecord(
        SourceReceipt source, string stableId, string metric, string dimension, string text,
        string spatialPrecision, string limitation)
    {
        Require(text.Length <= 2_000 && dimension.Length <= 500 && limitation.Length + CommonLimit.Length + 1 <= 240,
            "SagajeongSpatialSupplementDatabaseFieldTooLong:" + source.Key);
        var collected = source.CollectedAtUtc;
        return new 외부데이터정규화Record
        {
            RecordKey = 외부데이터RecordKey.Create(SourceId, source.DatasetId, RegionStableId,
                metric, source.ReferenceDate, dimension),
            StableId = stableId,
            SourceId = SourceId,
            DatasetId = source.DatasetId,
            RegionStableId = RegionStableId,
            MetricCode = metric,
            NumericValue = null,
            TextValue = text,
            UnitCode = "spatial-observation-json",
            EvidenceAsOfUtc = source.ReferenceDate,
            CollectedAtUtc = collected,
            SpatialPrecisionCode = spatialPrecision,
            TemporalPrecisionCode = NormalizedTemporalPrecision(source),
            QualityCode = Quality,
            LimitationCode = CommonLimit + ";" + limitation,
            DimensionKey = dimension,
            SourceVersion = $"official-source:{source.ReferenceDate:yyyy-MM-dd};sha256:{source.Sha256.ToLowerInvariant()}",
            DataRevision = DataRevision,
            FirstSeenAtUtc = collected,
            LastSeenAtUtc = collected
        };
    }

    private static int SelfTest(FrozenAcquisition frozen, ParsedData parsed)
    {
        var tests = 0;
        void Test(bool condition, string code) { Require(condition, "SagajeongSpatialSupplementSelfTest:" + code); tests++; }
        Test(parsed.Counts.Count == SourceDefinitions.Length, "FiveSourceCounts");
        Test(parsed.Counts["pedestrian"].SelectedRows > 100, "PedestrianCoverage");
        Test(parsed.Counts["elevator"].SelectedRows == 1, "SagajeongElevator");
        Test(parsed.Counts["park"].SelectedRows == 1, "SagajeongParkIdentity");
        Test(parsed.Counts["bus-stop"].SelectedRows > 0, "BusStops");
        Test(parsed.Counts["roadside-tree"].SelectedRows > 0, "RoadsideTrees");
        Test(parsed.Counts.All(pair => pair.Value == new SourceCount(
            Receipt(frozen, pair.Key).TotalRows, Receipt(frozen, pair.Key).SelectedRows)), "ReceiptCountsMatch");
        Test(parsed.Records.All(item => item.RegionStableId == RegionStableId
            && item.QualityCode == Quality
            && item.NumericValue is null
            && item.TextValue.Length <= 2_000
            && item.LimitationCode.Contains("NoRuntime", StringComparison.Ordinal)
            && item.LimitationCode.Contains("NoTraversalAuthority", StringComparison.Ordinal)
            && item.LimitationCode.Contains("NoGameplayAuthority", StringComparison.Ordinal)), "AuthorityBoundary");
        Test(parsed.Records.Where(item => item.DatasetId != Source("park").DatasetId)
            .All(item => item.TextValue.Contains("localEnu", StringComparison.Ordinal)), "ResolvedSourcesHaveEnu");
        Test(parsed.Records.Where(item => item.DatasetId == Source("park").DatasetId)
            .All(item => item.SpatialPrecisionCode.Contains("not-normalized", StringComparison.Ordinal)
                && item.LimitationCode.Contains("Epsg5174TransformationPendingReview", StringComparison.Ordinal)), "ParkCrsGapExplicit");
        Test(parsed.Records.All(item => item.StableId.Length <= 240
                                       && item.MetricCode.Length <= 160
                                       && item.TextValue.Length <= 2_000
                                       && item.SpatialPrecisionCode.Length <= 80
                                       && item.TemporalPrecisionCode.Length <= 80
                                       && item.LimitationCode.Length <= 240
                                       && item.DimensionKey.Length <= 500
                                       && item.SourceVersion.Length <= 200), "DatabaseFieldLengths");
        Test(frozen.Receipt.BlockedExternalSources.All(item => item.Status == "BlockedExternalAccess"
            && item.Limitation.Contains("NoFallback", StringComparison.Ordinal)), "NgiiBlockersExplicit");
        Test(frozen.Receipt.VisualRightsReview.NewOriginalStatus == "ExistingEligibleSourcesReused;NoNewVisualOriginalDownloaded"
            && frozen.Receipt.VisualRightsReview.Policy.Contains("CCBYSAStillBlocked", StringComparison.Ordinal), "VisualRightsBoundary");
        Test(parsed.Records.Select(item => item.RecordKey).SequenceEqual(
            parsed.Records.OrderBy(item => item.DatasetId, StringComparer.Ordinal)
                .ThenBy(item => item.StableId, StringComparer.Ordinal)
                .ThenBy(item => item.DimensionKey, StringComparer.Ordinal)
                .Select(item => item.RecordKey)), "DeterministicOrder");
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
        command.CommandText = "SELECT GET_LOCK('mirror:public-data:sagajeong-spatial-supplement-r1',0)";
        Require(Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 1,
            "SagajeongSpatialSupplementImportBusy");
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            result["databaseWriteAttempted"] = true;
            var service = new 평창군공공공간원본등록Service(db);
            var registrations = new Dictionary<string, 공공공간원본등록Result>(StringComparer.Ordinal);
            foreach (var source in frozen.Receipt.Sources)
            {
                var definition = Source(source.Key);
                registrations.Add(source.Key, await service.RegisterFileAsync(
                    frozen.Paths[source.Key],
                    new 공공공간원본등록Request(
                        SourceId, source.DatasetId,
                        $"official-source:{source.ReferenceDate:yyyy-MM-dd};sha256:{source.Sha256.ToLowerInvariant()}",
                        DataRevision, source.ReferenceDate, definition.ContentType,
                        RelativeFolder + "/" + definition.RawFileName)));
            }

            foreach (var record in parsed.Records)
            {
                var key = SourceDefinitions.Single(item => item.DatasetId == record.DatasetId).Key;
                record.RawSnapshotId = registrations[key].RawSnapshotId;
            }
            var before = await ReadExistingAsync(db, parsed.Records.Select(item => item.RecordKey).ToArray());
            Require(before.All(item => parsed.Records.Any(candidate => Equivalent(item, candidate))),
                "SagajeongSpatialSupplementExistingRecordConflict");

            var inserted = 0;
            var existing = 0;
            var insertedByDataset = new Dictionary<string, int>(StringComparer.Ordinal);
            var existingByDataset = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var group in parsed.Records.GroupBy(item => item.DatasetId, StringComparer.Ordinal))
            {
                var datasetInserted = 0;
                var datasetExisting = 0;
                var rows = group.ToArray();
                for (var start = 0; start < rows.Length; start += 200)
                {
                    var stored = await new EfExternalDataIngestionStore(db)
                        .UpsertNormalizedAsync(rows.Skip(start).Take(200).ToArray());
                    Require(stored.UpdatedCount == 0, "SagajeongSpatialSupplementUnexpectedUpdate");
                    datasetInserted += stored.InsertedCount;
                    datasetExisting += stored.ExistingCount;
                }
                inserted += datasetInserted;
                existing += datasetExisting;
                insertedByDataset.Add(group.Key, datasetInserted);
                existingByDataset.Add(group.Key, datasetExisting);
            }

            foreach (var source in frozen.Receipt.Sources)
            {
                var registration = registrations[source.Key];
                if (!registration.Inserted) continue;
                var snapshot = await db.RawSnapshots.SingleAsync(item => item.Id == registration.RawSnapshotId);
                snapshot.CollectedAtUtc = frozen.Receipt.AcquiredAtUtc;
                var run = await db.IngestionRuns.SingleAsync(item => item.Id == snapshot.FirstCollectionRunId);
                run.StatusCode = 외부데이터수집StatusCodes.Partial;
                run.FetchedCount = source.TotalRows;
                run.NormalizedCount = source.SelectedRows;
                run.InsertedCount = insertedByDataset[source.DatasetId];
                run.ExistingCount = existingByDataset[source.DatasetId];
                run.ErrorCode = Quality;
                run.ErrorSummary = "Official source cropped to the frozen Sagajeong window. Missing coverage, historical dates, CRS gaps and authority limits remain explicit; no publication, runtime, traversal or gameplay authority.";
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
            command.CommandText = "SELECT RELEASE_LOCK('mirror:public-data:sagajeong-spatial-supplement-r1')";
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
        Require(stored.Count == parsed.Records.Count
                && stored.All(item => parsed.Records.Any(candidate => Equivalent(item, candidate)))
                && stored.All(item => item.RawSnapshot is not null),
            "SagajeongSpatialSupplementReadbackMismatch");
        foreach (var source in frozen.Receipt.Sources)
        {
            var snapshot = await db.RawSnapshots.AsNoTracking().Include(item => item.FirstCollectionRun)
                .SingleOrDefaultAsync(item => item.SourceId == SourceId
                    && item.DatasetId == source.DatasetId
                    && item.ContentHashSha256 == source.Sha256.ToLowerInvariant());
            Require(snapshot is not null
                    && snapshot.ContentLength == source.ContentLength
                    && snapshot.StorageContainer == "local-private-public-spatial"
                    && snapshot.FirstCollectionRun?.StatusCode == 외부데이터수집StatusCodes.Partial
                    && snapshot.FirstCollectionRun.ErrorCode == Quality
                    && snapshot.FirstCollectionRun.FetchedCount == source.TotalRows
                    && snapshot.FirstCollectionRun.NormalizedCount == source.SelectedRows,
                "SagajeongSpatialSupplementRawReadbackMismatch:" + source.Key);
        }
        result["verifiedRows"] = stored.Count;
        result["verifiedRawSnapshots"] = frozen.Receipt.Sources.Count;
        result["independentReadback"] = true;
        result["sourceHashes"] = frozen.Receipt.Sources.ToDictionary(item => item.Key, item => item.Sha256);
    }

    private static void SetSummary(Dictionary<string, object?> result, FrozenAcquisition frozen, ParsedData parsed)
    {
        result["revision"] = DataRevision;
        result["stationStableId"] = StationStableId;
        result["regionStableId"] = RegionStableId;
        result["sourceCounts"] = parsed.Counts;
        result["selectedRows"] = parsed.Records.Count;
        result["maxFieldLengths"] = new
        {
            stableId = parsed.Records.Max(item => item.StableId.Length),
            metricCode = parsed.Records.Max(item => item.MetricCode.Length),
            textValue = parsed.Records.Max(item => item.TextValue.Length),
            spatialPrecisionCode = parsed.Records.Max(item => item.SpatialPrecisionCode.Length),
            temporalPrecisionCode = parsed.Records.Max(item => item.TemporalPrecisionCode.Length),
            limitationCode = parsed.Records.Max(item => item.LimitationCode.Length),
            dimensionKey = parsed.Records.Max(item => item.DimensionKey.Length),
            sourceVersion = parsed.Records.Max(item => item.SourceVersion.Length)
        };
        result["blockedExternalSources"] = frozen.Receipt.BlockedExternalSources.Count;
        result["newVisualOriginals"] = 0;
        result["runtimeAuthorized"] = false;
        result["traversalAuthorized"] = false;
        result["gameplayAuthorized"] = false;
    }

    private static async Task<List<외부데이터정규화Record>> ReadExistingAsync(
        PublicDataIngestionDbContext db, IReadOnlyList<string> keys)
    {
        var result = new List<외부데이터정규화Record>();
        for (var start = 0; start < keys.Count; start += 400)
        {
            var batch = keys.Skip(start).Take(400).ToList();
            result.AddRange(await db.NormalizedRecords.AsNoTracking().Include(item => item.RawSnapshot)
                .Where(item => batch.Contains(item.RecordKey)).ToListAsync());
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

    private static async Task ValidateOfficialPageAsync(
        HttpClient http, SourceDefinition source, params string[] markers)
        => _ = await ReadOfficialPageAsync(http, source, markers, source.Key is "pedestrian" or "roadside-tree" ? "A" : "S");

    private static async Task<string> ReadOfficialPageAsync(
        HttpClient http, SourceDefinition source, string[] markers, string serviceType)
    {
        var url = $"https://data.seoul.go.kr/dataList/{source.InfId}/{serviceType}/1/datasetView.do";
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseContentRead);
        Require(response.StatusCode == HttpStatusCode.OK
                && response.RequestMessage?.RequestUri?.Host == "data.seoul.go.kr",
            "SagajeongSpatialSupplementOfficialPageUnavailable:" + source.Key);
        var page = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Require(page.Contains(source.DatasetTitle, StringComparison.Ordinal)
                && markers.All(marker => page.Contains(marker, StringComparison.OrdinalIgnoreCase)),
            "SagajeongSpatialSupplementOfficialPageContractChanged:" + source.Key);
        return page;
    }

    private static Task<string> ReadOfficialPageAsync(
        HttpClient http, SourceDefinition source, string marker1, string marker2, string serviceType)
        => ReadOfficialPageAsync(http, source, [marker1, marker2], serviceType);

    private static async Task<SheetPackage> FetchSheetRowsAsync(
        HttpClient http, SourceDefinition source, string filterColumn, string filterValue, int maximumPages)
    {
        var rows = new JArray();
        var totalRows = -1;
        var pageCount = -1;
        for (var page = 1; ; page++)
        {
            Require(page <= maximumPages, "SagajeongSpatialSupplementSheetPageBudget:" + source.Key);
            var url = SheetBase
                      + "&infId=" + Uri.EscapeDataString(source.InfId)
                      + "&filterCol=" + Uri.EscapeDataString(filterColumn)
                      + "&pageNo=" + page.ToString(CultureInfo.InvariantCulture)
                      + "&txtFilter=" + Uri.EscapeDataString(filterValue);
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseContentRead);
            Require(response.StatusCode == HttpStatusCode.OK
                    && response.RequestMessage?.RequestUri?.Host == "data.seoul.go.kr",
                "SagajeongSpatialSupplementSheetUnavailable:" + source.Key + ":" + page);
            var text = await response.Content.ReadAsStringAsync();
            var parsed = JObject.Parse(text);
            Require(string.Equals((string?)parsed["result"], "ok", StringComparison.Ordinal),
                "SagajeongSpatialSupplementSheetResult:" + source.Key + ":" + page);
            var pageObject = parsed["page"] as JObject
                ?? throw new InvalidDataException("SagajeongSpatialSupplementSheetPageMissing:" + source.Key);
            var currentTotal = (int?)pageObject["totalCount"] ?? -1;
            var currentPageCount = (int?)pageObject["pageCount"] ?? -1;
            Require(currentTotal >= 0 && currentPageCount >= 1 && currentPageCount <= maximumPages,
                "SagajeongSpatialSupplementSheetCountInvalid:" + source.Key);
            if (page == 1) { totalRows = currentTotal; pageCount = currentPageCount; }
            Require(totalRows == currentTotal && pageCount == currentPageCount,
                "SagajeongSpatialSupplementSheetChangedDuringRead:" + source.Key);
            var list = parsed["list"] as JArray ?? new JArray();
            foreach (var row in list) rows.Add(row.DeepClone());
            if (page >= pageCount) break;
        }
        Require(rows.Count == totalRows, "SagajeongSpatialSupplementSheetRowCountMismatch:" + source.Key);
        return new SheetPackage("seoul-open-data-sheet-package.v1", source.InfId, source.DatasetId,
            filterColumn, filterValue, totalRows, pageCount, rows);
    }

    private static async Task WritePackageAsync(string path, SheetPackage package)
    {
        var root = new JObject
        {
            ["schemaVersion"] = package.SchemaVersion,
            ["infId"] = package.InfId,
            ["datasetId"] = package.DatasetId,
            ["filterColumn"] = package.FilterColumn,
            ["filterValue"] = package.FilterValue,
            ["totalRows"] = package.TotalRows,
            ["pageCount"] = package.PageCount,
            ["rows"] = package.Rows
        };
        await File.WriteAllTextAsync(path, root.ToString(Newtonsoft.Json.Formatting.None) + Environment.NewLine,
            new UTF8Encoding(false));
    }

    private static SheetPackage ReadPackage(string path)
    {
        var root = JObject.Parse(File.ReadAllText(path));
        Require((string?)root["schemaVersion"] == "seoul-open-data-sheet-package.v1",
            "SagajeongSpatialSupplementPackageVersionInvalid");
        return new SheetPackage(
            (string)root["schemaVersion"]!, (string)root["infId"]!, (string)root["datasetId"]!,
            (string)root["filterColumn"]!, (string)root["filterValue"]!, (int)root["totalRows"]!,
            (int)root["pageCount"]!, (JArray)root["rows"]!);
    }

    private static async Task<DownloadedFile> DownloadOfficialFileAsync(
        HttpClient http, string staging, SourceDefinition source, string page,
        string sourceFileName, string storedFileName)
    {
        var form = RequiredMatch(page, "<form\\s+name=\"frmFile\"(?<value>[\\s\\S]*?)</form>", "value");
        var infSeq = RequiredMatch(form, "name=\"infSeq\"\\s+value=\"(?<value>[0-9]+)\"", "value");
        var pattern = "title=\"" + Regex.Escape(sourceFileName)
                      + "\"\\s+onclick=\"javascript:downloadFile\\('(?<value>[0-9]+)'\\);\"";
        var sequence = RequiredMatch(page, pattern, "value");
        using var request = new HttpRequestMessage(HttpMethod.Post, DownloadEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["infId"] = source.InfId,
                ["seqNo"] = sequence,
                ["seq"] = sequence,
                ["infSeq"] = infSeq
            })
        };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        Require(response.StatusCode == HttpStatusCode.OK
                && response.RequestMessage?.RequestUri?.Host == "datafile.seoul.go.kr"
                && response.Content.Headers.ContentType?.MediaType != "text/html",
            "SagajeongSpatialSupplementDownloadRejected:" + source.Key);
        if (response.Content.Headers.ContentLength is long advertised)
            Require(advertised > 0 && advertised <= MaximumDownloadBytes,
                "SagajeongSpatialSupplementDownloadLengthInvalid:" + source.Key);
        var path = Path.Combine(staging, storedFileName);
        await using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
        await using var output = File.Create(path);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[64 * 1024];
        long total = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer, timeout.Token);
            if (read == 0) break;
            total += read;
            Require(total <= MaximumDownloadBytes, "SagajeongSpatialSupplementDownloadBudget:" + source.Key);
            hash.AppendData(buffer, 0, read);
            await output.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
        }
        Require(total > 0, "SagajeongSpatialSupplementDownloadEmpty:" + source.Key);
        return new DownloadedFile(path, total, Convert.ToHexString(hash.GetHashAndReset()), sourceFileName,
            sequence, infSeq);
    }

    private static SourceReceipt FileReceipt(
        SourceDefinition source, string path, int totalRows, int selectedRows,
        DateTimeOffset referenceDate, string licenseCode, string? sourceFileName = null)
        => new(source.Key, source.InfId, source.DatasetTitle,
            $"https://data.seoul.go.kr/dataList/{source.InfId}/{OfficialServiceType(source)}/1/datasetView.do",
            source.DatasetId, sourceFileName ?? source.RawFileName, source.RawFileName,
            source.ContentType, new FileInfo(path).Length, HashFile(path), referenceDate,
            RoundToMilliseconds(DateTimeOffset.UtcNow), licenseCode, totalRows, selectedRows, source.TemporalNote);

    private static string OfficialServiceType(SourceDefinition source)
        => source.Key switch
        {
            "pedestrian" or "roadside-tree" => "A",
            "park" or "bus-stop" => "F",
            _ => "S"
        };

    private static int CountPedestrianSelected(JArray rows)
        => rows.OfType<JObject>().Count(row => TryWktBounds(
            Text(row, "NODE_TYPE") == "NODE" ? Text(row, "NODE_WKT") : Text(row, "LNKG_WKT"),
            out var bounds, out _) && bounds.Intersects(Scope));

    private static int CountPointSelected(JArray rows, string field)
        => rows.OfType<JObject>().Count(row => TryWktBounds(Text(row, field), out var bounds, out _)
                                               && bounds.Intersects(Scope));

    private static int CountCoordinateSelected(JArray rows, string longitudeField, string latitudeField)
        => rows.OfType<JObject>().Count(row => TryDouble(Text(row, longitudeField), out var longitude)
                                               && TryDouble(Text(row, latitudeField), out var latitude)
                                               && Scope.Contains(longitude, latitude));

    private static SourceCount CountBusWorkbook(string path)
    {
        var workbook = ReadBusWorkbook(path);
        return new SourceCount(workbook.TotalRows,
            workbook.Rows.Count(item => Scope.Contains(item.Longitude, item.Latitude)));
    }

    private static BusStopWorkbook ReadBusWorkbook(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var spreadsheet = (XNamespace)"http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var sharedStrings = ReadSharedStrings(archive, spreadsheet);
        var currentHeaders = new[] { "STDR_DE", "NODE_ID", "STTN_NO", "STTN_NM", "CRDNT_X", "CRDNT_Y", "STTN_TY" };
        var legacyHeaders = new[] { "NODE_ID", "ARS_ID", "정류소명", "X좌표", "Y좌표", "정류소타입" };
        var candidates = new List<BusStopWorkbook>();
        var observedHeaders = new List<string>();
        foreach (var entry in archive.Entries
                     .Where(item => Regex.IsMatch(item.FullName, "^xl/worksheets/sheet[0-9]+\\.xml$",
                         RegexOptions.CultureInvariant))
                     .OrderBy(item => item.FullName, StringComparer.Ordinal))
        {
            var worksheet = LoadXml(entry);
            var rows = worksheet.Descendants(spreadsheet + "sheetData").Elements(spreadsheet + "row").ToArray();
            if (rows.Length < 2) continue;
            var headerIndex = -1;
            var currentLayout = false;
            for (var index = 0; index < Math.Min(rows.Length, 12); index++)
            {
                var header = ReadXlsxRow(rows[index], spreadsheet, sharedStrings);
                if (header.SequenceEqual(currentHeaders, StringComparer.Ordinal))
                {
                    headerIndex = index;
                    currentLayout = true;
                    break;
                }
                if (header.Take(legacyHeaders.Length).SequenceEqual(legacyHeaders, StringComparer.Ordinal)
                    && header.Skip(legacyHeaders.Length).All(string.IsNullOrWhiteSpace))
                {
                    headerIndex = index;
                    break;
                }
            }
            if (headerIndex < 0)
            {
                observedHeaders.Add(entry.FullName + ":" + string.Join("|", ReadXlsxRow(rows[0], spreadsheet, sharedStrings)));
                continue;
            }

            var result = new List<BusStopRow>(rows.Length - headerIndex - 1);
            foreach (var row in rows.Skip(headerIndex + 1))
            {
                var values = ReadXlsxRow(row, spreadsheet, sharedStrings);
                var nodeIndex = currentLayout ? 1 : 0;
                var arsIndex = currentLayout ? 2 : 1;
                var nameIndex = currentLayout ? 3 : 2;
                var longitudeIndex = currentLayout ? 4 : 3;
                var latitudeIndex = currentLayout ? 5 : 4;
                var typeIndex = currentLayout ? 6 : 5;
                if (!TryDouble(values[longitudeIndex], out var longitude)
                    || !TryDouble(values[latitudeIndex], out var latitude)) continue;
                if (string.IsNullOrWhiteSpace(values[nodeIndex])) continue;
                result.Add(new BusStopRow(values[nodeIndex], values[arsIndex].PadLeft(5, '0'),
                    values[nameIndex], longitude, latitude, values[typeIndex]));
            }
            if (result.Count > 0)
                candidates.Add(new BusStopWorkbook(rows.Length - headerIndex - 1, result));
            else
                observedHeaders.Add(entry.FullName + ":samples=" + string.Join("/", rows.Skip(headerIndex + 1)
                    .Select(row => ReadXlsxRow(row, spreadsheet, sharedStrings))
                    .Where(values => values.Any(value => !string.IsNullOrWhiteSpace(value)))
                    .Take(8)
                    .Select(values => string.Join("|", values))));
        }
        Require(candidates.Count > 0, "SagajeongSpatialSupplementBusDataSheetMissing:"
                                     + string.Join("/", observedHeaders));
        return candidates.OrderByDescending(item => item.Rows.Count).First();
    }

    private static XDocument LoadXml(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        return XDocument.Load(stream, LoadOptions.None);
    }

    private static ZipArchiveEntry RequiredEntry(ZipArchive archive, string path)
        => archive.GetEntry(path) ?? throw new InvalidDataException("SagajeongSpatialSupplementArchiveEntryMissing:" + path);

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive, XNamespace spreadsheet)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null) return Array.Empty<string>();
        var document = LoadXml(entry);
        return document.Root?.Elements(spreadsheet + "si")
                   .Select(item => string.Concat(item.Descendants(spreadsheet + "t").Select(text => text.Value)))
                   .ToArray() ?? Array.Empty<string>();
    }

    private static string[] ReadXlsxRow(XElement row, XNamespace spreadsheet, IReadOnlyList<string> sharedStrings)
    {
        var result = new string[7];
        foreach (var cell in row.Elements(spreadsheet + "c"))
        {
            var reference = (string?)cell.Attribute("r") ?? "";
            var column = ColumnIndex(reference);
            if (column is < 0 or >= 7) continue;
            var cellType = (string?)cell.Attribute("t");
            var raw = cellType == "inlineStr"
                ? string.Concat(cell.Descendants(spreadsheet + "t").Select(text => text.Value))
                : cell.Element(spreadsheet + "v")?.Value ?? "";
            if (cellType == "s")
            {
                Require(int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
                        && index >= 0 && index < sharedStrings.Count,
                    "SagajeongSpatialSupplementSharedStringInvalid");
                raw = sharedStrings[index];
            }
            result[column] = raw.Trim().TrimStart('\uFEFF');
        }
        return result;
    }

    private static int ColumnIndex(string reference)
    {
        var value = 0;
        var any = false;
        foreach (var character in reference)
        {
            if (character is < 'A' or > 'Z') break;
            value = checked(value * 26 + character - 'A' + 1);
            any = true;
        }
        return any ? value - 1 : -1;
    }

    private static string LatestBusFileName(string page)
    {
        var names = Regex.Matches(page, "서울시버스정류소위치정보\\((?<date>[0-9]{8})\\)\\.xlsx")
            .Select(match => new { Name = match.Value, Date = match.Groups["date"].Value })
            .DistinctBy(item => item.Name, StringComparer.Ordinal)
            .OrderByDescending(item => item.Date, StringComparer.Ordinal).ToArray();
        Require(names.Length > 0, "SagajeongSpatialSupplementBusFileMissing");
        return names[0].Name;
    }

    private static DateTimeOffset ParseBusFileDate(string fileName)
    {
        var value = RequiredMatch(fileName, "\\((?<value>[0-9]{8})\\)", "value");
        return new DateTimeOffset(DateTime.ParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture,
            DateTimeStyles.None), TimeSpan.Zero);
    }

    private static bool TryWktBounds(string wkt, out Wgs84Bounds bounds, out int pointCount)
    {
        bounds = default;
        pointCount = 0;
        if (string.IsNullOrWhiteSpace(wkt)) return false;
        var numbers = Regex.Matches(wkt, "-?[0-9]+(?:\\.[0-9]+)?")
            .Select(match => double.Parse(match.Value, CultureInfo.InvariantCulture)).ToArray();
        if (numbers.Length < 2 || numbers.Length % 2 != 0) return false;
        var longitudes = new List<double>();
        var latitudes = new List<double>();
        for (var index = 0; index < numbers.Length; index += 2)
        {
            longitudes.Add(numbers[index]);
            latitudes.Add(numbers[index + 1]);
        }
        pointCount = longitudes.Count;
        bounds = new Wgs84Bounds(longitudes.Min(), latitudes.Min(), longitudes.Max(), latitudes.Max());
        return bounds.IsFiniteAndOrdered;
    }

    private static LocalPoint ToLocal(double latitude, double longitude)
    {
        var origin = ToEcef(OriginLatitude, OriginLongitude);
        var point = ToEcef(latitude, longitude);
        var latitudeRadians = DegreesToRadians(OriginLatitude);
        var longitudeRadians = DegreesToRadians(OriginLongitude);
        var dx = point.X - origin.X;
        var dy = point.Y - origin.Y;
        var dz = point.Z - origin.Z;
        var east = -Math.Sin(longitudeRadians) * dx + Math.Cos(longitudeRadians) * dy;
        var north = -Math.Sin(latitudeRadians) * Math.Cos(longitudeRadians) * dx
                    - Math.Sin(latitudeRadians) * Math.Sin(longitudeRadians) * dy
                    + Math.Cos(latitudeRadians) * dz;
        return new LocalPoint(Math.Round(OffsetX + east, 3), Math.Round(OffsetZ + north, 3));
    }

    private static EcefPoint ToEcef(double latitude, double longitude)
    {
        const double semiMajor = 6_378_137d;
        const double inverseFlattening = 298.257223563d;
        var flattening = 1d / inverseFlattening;
        var eccentricitySquared = flattening * (2d - flattening);
        var latitudeRadians = DegreesToRadians(latitude);
        var longitudeRadians = DegreesToRadians(longitude);
        var sinLatitude = Math.Sin(latitudeRadians);
        var primeVertical = semiMajor / Math.Sqrt(1d - eccentricitySquared * sinLatitude * sinLatitude);
        return new EcefPoint(
            primeVertical * Math.Cos(latitudeRadians) * Math.Cos(longitudeRadians),
            primeVertical * Math.Cos(latitudeRadians) * Math.Sin(longitudeRadians),
            primeVertical * (1d - eccentricitySquared) * sinLatitude);
    }

    private static double DegreesToRadians(double value) => value * Math.PI / 180d;

    private static string Text(JObject row, string name) => row[name]?.ToString().Trim() ?? "";
    private static string? OptionalText(JObject row, string name)
        => string.IsNullOrWhiteSpace(Text(row, name)) ? null : Text(row, name);
    private static double? OptionalDouble(JObject row, string name)
        => TryDouble(Text(row, name), out var value) ? value : null;
    private static bool TryDouble(string value, out double result)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result)
           && double.IsFinite(result);

    private static string StablePart(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..24];

    private static string NormalizedTemporalPrecision(SourceReceipt source)
        => source.Key == "pedestrian"
            ? "HistoricalSpatialReference;Source2020;PerRowWorkTimestamp"
            : source.TemporalNote;

    private static string RequiredMatch(string input, string pattern, string group)
    {
        var match = Regex.Match(input, pattern, RegexOptions.CultureInvariant);
        Require(match.Success, "SagajeongSpatialSupplementOfficialPageDownloadContractChanged");
        return match.Groups[group].Value;
    }

    private static string HashFile(string path)
        => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static SourceDefinition Source(string key)
        => SourceDefinitions.Single(item => item.Key == key);

    private static SourceReceipt Receipt(FrozenAcquisition frozen, string key)
        => frozen.Receipt.Sources.Single(item => item.Key == key);

    private static DateTimeOffset RoundToMilliseconds(DateTimeOffset value)
        => DateTimeOffset.FromUnixTimeMilliseconds(value.ToUnixTimeMilliseconds());

    private static async Task WriteCoverageTreeAsync(string folder, AcquisitionReceipt receipt)
    {
        var tree = new
        {
            schemaVersion = "station-diorama-source-coverage-tree.v1",
            revision = DataRevision,
            stationStableId = StationStableId,
            regionStableId = RegionStableId,
            flow = "official-source -> private-raw -> normalized-observation -> station-layer -> future-unity-adapter",
            layers = new object[]
            {
                Layer("PedestrianAccess", receipt, "pedestrian", "Exit/crosswalk/network relation reference only"),
                Layer("Accessibility", receipt, "elevator", "Elevator orientation reference only"),
                Layer("GreenSpace", receipt, "park", "Identity ready; EPSG:5174 boundary normalization deferred"),
                Layer("TransitFurniture", receipt, "bus-stop", "Static stop placement candidate only"),
                Layer("Vegetation", receipt, "roadside-tree", "Historical procedural placement candidate only")
            },
            blockers = receipt.BlockedExternalSources,
            visualRights = receipt.VisualRightsReview,
            authorityBoundary = "NoRuntime;NoTraversal;NoGameplay;NoUnitySceneOrPrefabChange"
        };
        await File.WriteAllTextAsync(Path.Combine(folder, CoverageTreeFileName),
            JsonSerializer.Serialize(tree, JsonOptions) + Environment.NewLine, new UTF8Encoding(false));
    }

    private static object Layer(string layer, AcquisitionReceipt receipt, string key, string limitation)
    {
        var source = receipt.Sources.Single(item => item.Key == key);
        return new
        {
            layer,
            source = source.InfId,
            source.DatasetId,
            privateRaw = source.StoredFileName,
            source.TotalRows,
            source.SelectedRows,
            source.Sha256,
            status = "PendingHumanReview",
            limitation
        };
    }

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }

    private sealed record SourceDefinition(
        string Key, string InfId, string DatasetTitle, string DatasetId, string RawFileName,
        string ContentType, DateTimeOffset ReferenceDate, string TemporalNote);

    private sealed record SourceReceipt(
        string Key, string InfId, string DatasetTitle, string OfficialPageUrl, string DatasetId,
        string SourceFileName, string StoredFileName, string ContentType, long ContentLength, string Sha256,
        DateTimeOffset ReferenceDate, DateTimeOffset CollectedAtUtc, string LicenseCode,
        int TotalRows, int SelectedRows, string TemporalNote);

    private sealed record BlockedExternalSource(string Key, string OfficialPageUrl, string Status, string Limitation);
    private sealed record VisualRightsReview(string NewOriginalStatus, IReadOnlyList<string> ExistingEvidenceRefs, string Policy);

    private sealed record AcquisitionReceipt(
        string SchemaVersion, DateTimeOffset AcquiredAtUtc, string StationStableId, string RegionStableId,
        Wgs84Bounds Bounds, string SelectionMethod, bool DistributionApproved, bool RuntimeAuthorized,
        bool TraversalAuthorized, bool GameplayAuthorized, string ReviewStatus,
        IReadOnlyList<SourceReceipt> Sources, IReadOnlyList<BlockedExternalSource> BlockedExternalSources,
        VisualRightsReview VisualRightsReview);

    private sealed record FrozenAcquisition(
        string Folder, AcquisitionReceipt Receipt, IReadOnlyDictionary<string, string> Paths);
    private sealed record ParsedData(
        IReadOnlyList<외부데이터정규화Record> Records, IReadOnlyDictionary<string, SourceCount> Counts);
    private sealed record SheetPackage(
        string SchemaVersion, string InfId, string DatasetId, string FilterColumn, string FilterValue,
        int TotalRows, int PageCount, JArray Rows);
    private sealed record DownloadedFile(
        string Path, long Length, string Sha256, string SourceFileName, string DownloadSequence, string InformationSequence);
    private sealed record BusStopRow(
        string NodeId, string ArsId, string Name, double Longitude, double Latitude, string StopType);
    private sealed record BusStopWorkbook(int TotalRows, IReadOnlyList<BusStopRow> Rows);
    private sealed record SourceCount(int TotalRows, int SelectedRows);
    private sealed record LocalPoint(double X, double Z);
    private sealed record EcefPoint(double X, double Y, double Z);

    private readonly record struct Wgs84Bounds(
        double MinLongitude, double MinLatitude, double MaxLongitude, double MaxLatitude)
    {
        public bool Contains(double longitude, double latitude)
            => longitude >= MinLongitude && longitude <= MaxLongitude
               && latitude >= MinLatitude && latitude <= MaxLatitude;
        public bool Intersects(Wgs84Bounds other)
            => !(other.MaxLongitude < MinLongitude || other.MinLongitude > MaxLongitude
                 || other.MaxLatitude < MinLatitude || other.MinLatitude > MaxLatitude);
        public bool IsFiniteAndOrdered
            => double.IsFinite(MinLongitude) && double.IsFinite(MinLatitude)
               && double.IsFinite(MaxLongitude) && double.IsFinite(MaxLatitude)
               && MinLongitude <= MaxLongitude && MinLatitude <= MaxLatitude;
    }

    private static Wgs84Bounds Scope => new(MinLongitude, MinLatitude, MaxLongitude, MaxLatitude);
}
