using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

internal static class 중랑구전통시장시각자료
{
    private const string RelativeFolder = "artifacts/local/public-data/jungnang-traditional-market-visuals-20260914-r1";
    private const string PdfFileName = "jungnang-news-2016-09.pdf";
    private const string ReceiptFileName = "acquisition.json";
    private const string MarketSourceRelativePath = "artifacts/local/public-data/eight-life-domains/market-20260908-r1/markets.json";
    private const string SourceUrl = "https://ecatalog.jungnang.go.kr/src/viewer/download.php?host=main&no=1&site=20160825_091645";
    private const string SourceId = "jungnang-gu-official-newsletter";
    private const string DatasetId = "jungnang-news-2016-09-traditional-markets";
    private const string MarketSourceId = "semas-traditional-market-status";
    private const string MarketDatasetId = "data-go-kr-15012894-standard";
    private const string Revision = "jungnang-traditional-market-visual-collection.r1";
    private const string Region = "region:kr:sig:11260";
    private const string ExpectedPdfSha256 = "8f5ede7e2a31068f421fa2cf977dce9da78b250ac3b903c115cd4df8cc05e0a4";
    private const int ExpectedPdfLength = 23_907_272;
    private const string ExpectedMarketSha256 = "13ffd04a946ec7eec28222c8c3762f2e77aab919cd250888cdaaf7f51cce303b";

    private sealed record Panel(
        string Slug,
        string HistoricalName,
        int Page,
        string FileName,
        int X,
        int Y,
        int Width,
        int Height,
        string HistoricalAddress,
        string[] CurrentRowNames,
        string IdentityReviewCode,
        string[] VisualFindings,
        string[] MissingModelEvidence);

    private static readonly Panel[] Panels =
    [
        new(
            "woorim", "우림시장", 2, "woorim-market-02.png", 45, 700, 535, 860,
            "서울특별시 중랑구 망우로62길 52-4",
            ["우림골목시장"],
            "OneCurrentAliasAndAddressMatchPendingCanonicalReview",
            ["StreetSpanningEntranceArch", "ArcadeRoofRhythm", "MixedVehiclePedestrianApproach", "LowRiseStorefrontEdge"],
            ["AllEntranceCoordinates", "MarketCenterlineAndWidth", "CanopyHeight", "SideAndRearMass", "CurrentCondition"]),
        new(
            "dongbu", "동부시장", 2, "dongbu-market-02.png", 590, 700, 535, 860,
            "서울특별시 중랑구 중랑천로10길 61",
            ["중랑동부시장(동부골목시장)"],
            "OneCurrentAliasButAddressChangedPendingIdentityReview",
            ["StreetFacingMultiStoreyEntrance", "RedVerticalMarketSign", "OpenStreetFront", "AdjacentLowRiseCommercialMass"],
            ["AddressChangeHistory", "ArcadeInterior", "MarketCenterlineAndWidth", "CanopyHeight", "CurrentCondition"]),
        new(
            "myeonmok", "면목시장", 3, "myeonmok-market-03.png", 560, 160, 570, 690,
            "서울특별시 중랑구 사가정로50길 84",
            ["면목골목시장", "면목시장"],
            "HistoricalNameMatchesOneRowButAddressMatchesOtherRowPendingIdentityReview",
            ["AlleyEntranceBanner", "ContinuousBarrelVaultArcade", "DenseTwoSidedStalls", "NarrowPedestrianShoppingAxis"],
            ["CanonicalMarketIdentity", "AllEntranceCoordinates", "MarketCenterlineAndWidth", "CanopyHeight", "CurrentCondition"]),
        new(
            "dongwon", "동원시장", 3, "dongwon-market-03.png", 35, 820, 540, 750,
            "서울특별시 중랑구 상봉로11길 37",
            ["동원시장", "동원전통종합시장"],
            "TwoCurrentRowsAndHistoricalAddressDifferencePendingIdentityReview",
            ["StationAdjacentStreetFacade", "DenseSignageBand", "MixedOpenAndArcadedCharacter", "MultiStoreyCommercialMass"],
            ["CanonicalMarketIdentity", "ExactEntranceThreshold", "ArcadeExtent", "MarketCenterlineAndWidth", "CurrentCondition"]),
        new(
            "sagajeong", "사가정시장", 3, "sagajeong-market-03.png", 565, 820, 565, 750,
            "서울특별시 중랑구 면목로44길 16",
            ["사가정시장"],
            "OneCurrentExactNameAndAddressMatchPendingCanonicalReview",
            ["OpenAlleyEntrance", "VerticalMarketSign", "NoContinuousArcadeVisibleAtEntrance", "DenseLowRiseStorefrontWalls"],
            ["AllEntranceCoordinates", "MarketCenterlineAndWidth", "RepresentativeAlleyCrossSection", "CanopyHeight", "CurrentCondition"]),
    ];

    private static readonly string[] OfficialRowNames =
    [
        "우림골목시장",
        "중랑동부시장(동부골목시장)",
        "면목골목시장",
        "면목시장",
        "동원시장",
        "동원전통종합시장",
        "사가정시장",
    ];

    public static async Task RunAsync(string mode, string repositoryRoot, IDictionary<string, object?> result)
    {
        if (mode == "acquire")
        {
            await AcquireAsync(repositoryRoot, result);
            return;
        }

        if (mode is not ("self-test" or "preview" or "apply" or "verify"))
            throw new InvalidDataException("JungnangMarketVisualModeInvalid");

        var folder = Path.Combine(repositoryRoot, RelativeFolder);
        var receiptPath = Path.Combine(folder, ReceiptFileName);
        Require(File.Exists(receiptPath), "JungnangMarketVisualReceiptMissing");
        using var receipt = JsonDocument.Parse(await File.ReadAllTextAsync(receiptPath));
        var collectedAt = ValidateReceiptAndFiles(receipt.RootElement, repositoryRoot, folder);
        var records = BuildRecords(receipt.RootElement, collectedAt);
        Require(records.Count == 11, "JungnangMarketVisualNormalizedCountChanged");

        if (mode == "self-test")
        {
            var second = BuildRecords(receipt.RootElement, collectedAt);
            Require(records.Select(Comparable).SequenceEqual(second.Select(Comparable)), "JungnangMarketVisualProjectionNondeterministic");
            Require(records.Count(record => record.MetricCode == "market-visual-reference") == 5, "JungnangMarketVisualPanelCountChanged");
            Require(records.Count(record => record.MetricCode == "market-identity-review") == 5, "JungnangMarketIdentityReviewCountChanged");
            Require(records.Single(record => record.MetricCode == "market-visual-rights-boundary").QualityCode == "RightsUnverified", "JungnangMarketRightsBoundaryMissing");
            Require(records.All(record => record.LimitationCode.Contains("NoUnity", StringComparison.Ordinal)), "JungnangMarketUnityPrematurelyAuthorized");
            result["selfTestsPassed"] = 17;
            result["marketPanels"] = 5;
            result["officialRowCandidates"] = 7;
            result["blenderAuthorized"] = false;
            result["unityAuthorized"] = false;
            result["mode"] = mode;
            return;
        }

        var options = await 로컬공공자료Db.OptionsAsync(repositoryRoot);
        var keys = records.Select(record => record.RecordKey).ToList();
        await using (var db = new PublicDataIngestionDbContext(options))
        {
            var before = await db.NormalizedRecords.AsNoTracking().Where(record => keys.Contains(record.RecordKey)).ToListAsync();
            Require(before.All(stored => records.Any(candidate => Same(candidate, stored))), "JungnangMarketVisualExistingRecordConflict");
            result["beforeCount"] = before.Count;

            if (mode == "apply")
            {
                await db.Database.OpenConnectionAsync();
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT GET_LOCK('mirror:public-data:jungnang-market-visual-r1',0)";
                Require(Convert.ToInt32(await command.ExecuteScalarAsync()) == 1, "JungnangMarketVisualImportBusy");
                await using var transaction = await db.Database.BeginTransactionAsync();
                result["databaseWriteAttempted"] = true;

                var registration = new 평창군공공공간원본등록Service(db);
                result["writeStage"] = "RegisterSourcePdf";
                var pdfRegistration = await registration.RegisterFileAsync(
                    Path.Combine(folder, PdfFileName),
                    new 공공공간원본등록Request(
                        SourceId,
                        DatasetId,
                        "official-newsletter-issued-2016-08-25",
                        Revision,
                        DateTimeOffset.Parse("2016-08-25T00:00:00Z"),
                        "application/pdf",
                        RelativeFolder + "/" + PdfFileName));

                var panelSnapshotIds = new Dictionary<string, long>(StringComparer.Ordinal);
                foreach (var panel in Panels)
                {
                    result["writeStage"] = "RegisterPanel:" + panel.Slug;
                    var registered = await registration.RegisterFileAsync(
                        Path.Combine(folder, "market-panels", panel.FileName),
                        new 공공공간원본등록Request(
                            SourceId,
                            DatasetId,
                            "official-newsletter-issued-2016-08-25-panel-crop",
                            Revision,
                            DateTimeOffset.Parse("2016-08-25T00:00:00Z"),
                            "image/png",
                            RelativeFolder + "/market-panels/" + panel.FileName));
                    panelSnapshotIds.Add(panel.Slug, registered.RawSnapshotId);
                }

                result["writeStage"] = "RegisterOfficialMarketSource";
                var marketRegistration = await registration.RegisterFileAsync(
                    Path.Combine(repositoryRoot, MarketSourceRelativePath),
                    new 공공공간원본등록Request(
                        MarketSourceId,
                        MarketDatasetId,
                        "official-standard-reference-2025-11-10",
                        Revision,
                        DateTimeOffset.Parse("2025-11-10T00:00:00Z"),
                        "application/json",
                        MarketSourceRelativePath));

                foreach (var record in records)
                {
                    if (record.MetricCode == "market-visual-reference")
                    {
                        var slug = record.DimensionKey.Split(';', StringSplitOptions.RemoveEmptyEntries)
                            .Single(part => part.StartsWith("market=", StringComparison.Ordinal))[7..];
                        record.RawSnapshotId = panelSnapshotIds[slug];
                    }
                    else if (record.MetricCode == "market-identity-review")
                    {
                        record.RawSnapshotId = marketRegistration.RawSnapshotId;
                    }
                    else
                    {
                        record.RawSnapshotId = pdfRegistration.RawSnapshotId;
                    }
                }

                result["writeStage"] = "UpsertNormalizedRecords";
                var saved = await new EfExternalDataIngestionStore(db).UpsertNormalizedAsync(records);
                Require(saved.UpdatedCount == 0, "JungnangMarketVisualUnexpectedUpdate");
                if (pdfRegistration.Inserted)
                {
                    var snapshot = await db.RawSnapshots.SingleAsync(item => item.Id == pdfRegistration.RawSnapshotId);
                    snapshot.CollectedAtUtc = collectedAt;
                    var run = await db.IngestionRuns.SingleAsync(item => item.Id == snapshot.FirstCollectionRunId);
                    run.StatusCode = 외부데이터수집StatusCodes.Partial;
                    run.FetchedCount = Panels.Length;
                    run.NormalizedCount = records.Count;
                    run.RejectedCount = Panels.Length;
                    run.InsertedCount = saved.InsertedCount;
                    run.ExistingCount = saved.ExistingCount;
                    run.ErrorCode = "PendingHumanReview";
                    run.ErrorSummary = "Historical official newsletter panels are private review evidence only. Publication image rights, current market identity, geometry, Blender derivation, Unity use and distribution remain unapproved.";
                    result["writeStage"] = "UpdateIngestionRun";
                    await db.SaveChangesAsync();
                }

                result["writeStage"] = "Commit";
                await transaction.CommitAsync();
                result["committed"] = true;
                result["inserted"] = saved.InsertedCount;
                result["existing"] = saved.ExistingCount;
            }
        }

        await using var verification = new PublicDataIngestionDbContext(options);
        var actual = await verification.NormalizedRecords.AsNoTracking()
            .Where(record => keys.Contains(record.RecordKey))
            .Include(record => record.RawSnapshot)
            .OrderBy(record => record.StableId)
            .ToListAsync();
        if (mode != "preview") Require(actual.Count == records.Count, "JungnangMarketVisualReadbackCountMismatch");
        Require(actual.All(stored => records.Any(candidate => Same(candidate, stored))
            && stored.RawSnapshot != null
            && stored.RawSnapshot.ContentHashSha256.Length == 64), "JungnangMarketVisualReadbackMismatch");
        result["verifiedRows"] = actual.Count;
        result["target"] = "hongdal-mysql-1 / hongdal_dev";
        result["mode"] = mode;
    }

    private static async Task AcquireAsync(string repositoryRoot, IDictionary<string, object?> result)
    {
        var folder = Path.Combine(repositoryRoot, RelativeFolder);
        var panelFolder = Path.Combine(folder, "market-panels");
        var pdfPath = Path.Combine(folder, PdfFileName);
        var receiptPath = Path.Combine(folder, ReceiptFileName);
        Directory.CreateDirectory(panelFolder);

        if (File.Exists(receiptPath))
        {
            using var existing = JsonDocument.Parse(await File.ReadAllTextAsync(receiptPath));
            ValidateReceiptAndFiles(existing.RootElement, repositoryRoot, folder);
            result["mode"] = "acquire";
            result["existingReceiptReused"] = true;
            result["folder"] = RelativeFolder;
            result["marketPanels"] = Panels.Length;
            result["receiptSha256"] = Hash(await File.ReadAllBytesAsync(receiptPath));
            return;
        }

        if (!File.Exists(pdfPath))
        {
            using var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All, AllowAutoRedirect = false };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SsalddelPublicDataCollector/1.0 (local research)");
            var bytes = await ReadBoundedAsync(client, SourceUrl, 30 * 1024 * 1024);
            Require(Hash(bytes) == ExpectedPdfSha256 && bytes.Length == ExpectedPdfLength, "JungnangMarketVisualPdfRevisionChanged");
            await File.WriteAllBytesAsync(pdfPath, bytes);
        }

        var pdfBytes = await File.ReadAllBytesAsync(pdfPath);
        Require(Hash(pdfBytes) == ExpectedPdfSha256 && pdfBytes.Length == ExpectedPdfLength, "JungnangMarketVisualPdfRevisionChanged");

        var marketSourcePath = Path.Combine(repositoryRoot, MarketSourceRelativePath);
        Require(File.Exists(marketSourcePath), "JungnangMarketOfficialRowsMissing");
        var marketBytes = await File.ReadAllBytesAsync(marketSourcePath);
        Require(Hash(marketBytes) == ExpectedMarketSha256, "JungnangMarketOfficialRowsRevisionChanged");

        foreach (var panel in Panels)
        {
            var panelPath = Path.Combine(panelFolder, panel.FileName);
            if (!File.Exists(panelPath)) await RenderPanelAsync(pdfPath, panelFolder, panel);
            var (width, height) = ReadPngDimensions(panelPath);
            Require(width == panel.Width && height == panel.Height, "JungnangMarketPanelDimensionsChanged:" + panel.Slug);
        }

        using var marketDocument = JsonDocument.Parse(marketBytes);
        var officialRows = marketDocument.RootElement.EnumerateArray()
            .Where(item => OfficialRowNames.Contains(item.GetProperty("MRKT_NM").GetString(), StringComparer.Ordinal))
            .Where(item => item.GetProperty("RDNMADR").GetString()?.StartsWith("서울특별시 중랑구", StringComparison.Ordinal) == true)
            .Select(item => item.Clone())
            .OrderBy(item => item.GetProperty("MRKT_NM").GetString(), StringComparer.Ordinal)
            .ToArray();
        Require(officialRows.Length == OfficialRowNames.Length, "JungnangMarketOfficialRowCountChanged");

        var collectedAt = TrimToMicrosecond(DateTimeOffset.UtcNow);
        var panels = Panels.Select(panel =>
        {
            var bytes = File.ReadAllBytes(Path.Combine(panelFolder, panel.FileName));
            return new
            {
                slug = panel.Slug,
                historicalName = panel.HistoricalName,
                page = panel.Page,
                fileName = panel.FileName,
                crop = new { x = panel.X, y = panel.Y, width = panel.Width, height = panel.Height, dpi = 110 },
                sourceUrl = SourceUrl,
                sourcePublicationDate = "2016-08-25",
                historicalAddress = panel.HistoricalAddress,
                currentRowNames = panel.CurrentRowNames,
                identityReviewCode = panel.IdentityReviewCode,
                visualFindings = panel.VisualFindings,
                missingModelEvidence = panel.MissingModelEvidence,
                sha256 = Hash(bytes),
                byteLength = bytes.Length,
                mime = "image/png",
                contentReviewStatus = "PendingHumanReview",
                rightsStatus = "ItemLevelRightsUnverified",
                modelDerivationAllowed = false,
                gameDistributionAllowed = false,
            };
        }).ToArray();

        var identityReviews = Panels.Select(panel => new
        {
            slug = panel.Slug,
            historicalName = panel.HistoricalName,
            historicalAddress = panel.HistoricalAddress,
            identityReviewCode = panel.IdentityReviewCode,
            status = "PendingHumanReview",
            officialRowCandidates = officialRows
                .Where(row => panel.CurrentRowNames.Contains(row.GetProperty("MRKT_NM").GetString(), StringComparer.Ordinal))
                .Select(row => row.Clone())
                .ToArray(),
        }).ToArray();
        Require(identityReviews.All(item => item.officialRowCandidates.Length > 0), "JungnangMarketIdentityCandidateMissing");

        var receiptBytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = "ssalddel.jungnang-traditional-market-visual-acquisition.v1",
            revision = Revision,
            collectedAtUtc = collectedAt,
            scope = new
            {
                regionStableId = Region,
                historicalMarketCount = Panels.Length,
                officialRowCandidateCount = officialRows.Length,
                privateReviewOnly = true,
                blenderWorkAuthorized = false,
                unityRuntimeAuthorized = false,
                distributionApproved = false,
            },
            sourceDocument = new
            {
                title = "중랑구소식지 2016년 9월호",
                sourceUrl = SourceUrl,
                fileName = PdfFileName,
                sha256 = Hash(pdfBytes),
                byteLength = pdfBytes.Length,
                publicationDate = "2016-08-25",
                publisher = "서울특별시 중랑구",
                rightsStatus = "ItemLevelRightsUnverified",
                rendererProfile = "pdftoppm-png-110dpi-fixed-crop",
            },
            officialMarketSource = new
            {
                sourceId = MarketSourceId,
                datasetId = MarketDatasetId,
                relativePath = MarketSourceRelativePath,
                sha256 = Hash(marketBytes),
                byteLength = marketBytes.Length,
                referenceDate = "2025-11-10",
                selectedRows = officialRows,
            },
            panels,
            identityReviews,
            rightsBoundary = new
            {
                sourcePublicationIsOfficial = true,
                itemLevelCommercialUseVerified = false,
                itemLevelModificationVerified = false,
                facesAndThirdPartyMarksMayBePresent = true,
                rawFilesMayEnterGit = false,
                rawFilesMayEnterUnity = false,
                mayGuideBlenderGeometry = false,
                nextRequiredAction = "ObtainItemLevelPermissionOrCollectSelfCapturedPermittedReferences",
            },
        }, JsonOptions);
        await File.WriteAllBytesAsync(receiptPath, receiptBytes);

        result["mode"] = "acquire";
        result["folder"] = RelativeFolder;
        result["marketPanels"] = Panels.Length;
        result["officialRowCandidates"] = officialRows.Length;
        result["receiptSha256"] = Hash(receiptBytes);
        result["blenderAuthorized"] = false;
        result["unityAuthorized"] = false;
    }

    private static DateTimeOffset ValidateReceiptAndFiles(JsonElement receipt, string repositoryRoot, string folder)
    {
        Require(receipt.GetProperty("schemaVersion").GetString() == "ssalddel.jungnang-traditional-market-visual-acquisition.v1", "JungnangMarketVisualReceiptSchemaChanged");
        Require(receipt.GetProperty("revision").GetString() == Revision, "JungnangMarketVisualReceiptRevisionChanged");
        var scope = receipt.GetProperty("scope");
        Require(scope.GetProperty("privateReviewOnly").GetBoolean()
            && !scope.GetProperty("blenderWorkAuthorized").GetBoolean()
            && !scope.GetProperty("unityRuntimeAuthorized").GetBoolean()
            && !scope.GetProperty("distributionApproved").GetBoolean(), "JungnangMarketVisualAuthorityBoundaryChanged");

        var pdfPath = Path.Combine(folder, PdfFileName);
        var pdfBytes = File.ReadAllBytes(pdfPath);
        Require(Hash(pdfBytes) == ExpectedPdfSha256 && pdfBytes.Length == ExpectedPdfLength, "JungnangMarketVisualPdfChanged");
        var marketBytes = File.ReadAllBytes(Path.Combine(repositoryRoot, MarketSourceRelativePath));
        Require(Hash(marketBytes) == ExpectedMarketSha256, "JungnangMarketOfficialRowsChanged");

        var panels = receipt.GetProperty("panels").EnumerateArray().ToArray();
        Require(panels.Length == Panels.Length, "JungnangMarketVisualPanelCountChanged");
        foreach (var panel in Panels)
        {
            var item = panels.Single(candidate => candidate.GetProperty("slug").GetString() == panel.Slug);
            Require(!item.GetProperty("modelDerivationAllowed").GetBoolean()
                && !item.GetProperty("gameDistributionAllowed").GetBoolean(), "JungnangMarketVisualPrematurePromotion:" + panel.Slug);
            var bytes = File.ReadAllBytes(Path.Combine(folder, "market-panels", panel.FileName));
            Require(Hash(bytes) == item.GetProperty("sha256").GetString(), "JungnangMarketVisualPanelHashChanged:" + panel.Slug);
            Require(bytes.Length == item.GetProperty("byteLength").GetInt32(), "JungnangMarketVisualPanelLengthChanged:" + panel.Slug);
            var (width, height) = ReadPngDimensions(Path.Combine(folder, "market-panels", panel.FileName));
            Require(width == panel.Width && height == panel.Height, "JungnangMarketVisualPanelDimensionsChanged:" + panel.Slug);
        }

        Require(receipt.GetProperty("identityReviews").GetArrayLength() == Panels.Length, "JungnangMarketIdentityReviewCountChanged");
        var rights = receipt.GetProperty("rightsBoundary");
        Require(rights.GetProperty("sourcePublicationIsOfficial").GetBoolean()
            && !rights.GetProperty("itemLevelCommercialUseVerified").GetBoolean()
            && !rights.GetProperty("itemLevelModificationVerified").GetBoolean()
            && !rights.GetProperty("mayGuideBlenderGeometry").GetBoolean(), "JungnangMarketRightsBoundaryChanged");
        return receipt.GetProperty("collectedAtUtc").GetDateTimeOffset();
    }

    private static List<외부데이터정규화Record> BuildRecords(JsonElement receipt, DateTimeOffset collectedAt)
    {
        var records = new List<외부데이터정규화Record>();
        var visualEvidenceAt = DateTimeOffset.Parse("2016-08-25T00:00:00Z");
        foreach (var panel in receipt.GetProperty("panels").EnumerateArray())
        {
            var slug = panel.GetProperty("slug").GetString()!;
            records.Add(Record(
                "visual-evidence:jungnang-news:traditional-market:" + slug + ".r1",
                SourceId,
                DatasetId,
                Region,
                "market-visual-reference",
                visualEvidenceAt,
                "market=" + slug + ";page=" + panel.GetProperty("page").GetInt32(),
                JsonSerializer.Serialize(panel),
                "publication-panel-crop",
                "publication-date",
                "PendingHumanReview",
                "Historical2016;PrivateReviewOnly;ItemLevelRightsUnverified;IdentityPending;GeometryIncomplete;NoBlender;NoUnity;NoDistribution",
                "official-newsletter-issued-2016-08-25",
                collectedAt));
        }

        foreach (var identity in receipt.GetProperty("identityReviews").EnumerateArray())
        {
            var slug = identity.GetProperty("slug").GetString()!;
            records.Add(Record(
                "market-identity-review:jungnang:" + slug + ".r1",
                MarketSourceId,
                MarketDatasetId,
                Region,
                "market-identity-review",
                DateTimeOffset.Parse("2025-11-10T00:00:00Z"),
                "market=" + slug,
                JsonSerializer.Serialize(identity),
                "official-point-row-candidate",
                "reference-date",
                "PendingHumanReview",
                "HistoricalNewsletterIdentity;CurrentOfficialRowCandidates;CanonicalIdentityUnresolved;NoOperationalAuthority;NoBlender;NoUnity;NoDistribution",
                "official-standard-reference-2025-11-10",
                collectedAt));
        }

        records.Add(Record(
            "visual-rights-boundary:jungnang-news:traditional-markets.r1",
            SourceId,
            DatasetId,
            Region,
            "market-visual-rights-boundary",
            visualEvidenceAt,
            "markets=woorim,dongbu,myeonmok,dongwon,sagajeong",
            receipt.GetProperty("rightsBoundary").GetRawText(),
            "publication-level",
            "publication-date",
            "RightsUnverified",
            "OfficialPublicationDoesNotProveItemLevelRights;PrivateReviewOnly;NoBlender;NoUnity;NoDistribution",
            "official-newsletter-issued-2016-08-25",
            collectedAt));
        return records;
    }

    private static 외부데이터정규화Record Record(
        string stableId,
        string sourceId,
        string datasetId,
        string region,
        string metric,
        DateTimeOffset evidenceAt,
        string dimension,
        string text,
        string spatialPrecision,
        string temporalPrecision,
        string quality,
        string limitation,
        string sourceVersion,
        DateTimeOffset collectedAt)
        => new()
        {
            RecordKey = 외부데이터RecordKey.Create(sourceId, datasetId, region, metric, evidenceAt, dimension),
            StableId = stableId,
            SourceId = sourceId,
            DatasetId = datasetId,
            RegionStableId = region,
            MetricCode = metric,
            NumericValue = null,
            TextValue = text,
            UnitCode = "visual-evidence-review",
            EvidenceAsOfUtc = evidenceAt,
            CollectedAtUtc = collectedAt,
            SpatialPrecisionCode = spatialPrecision,
            TemporalPrecisionCode = temporalPrecision,
            QualityCode = quality,
            LimitationCode = limitation,
            DimensionKey = dimension,
            SourceVersion = sourceVersion,
            DataRevision = Revision,
            FirstSeenAtUtc = collectedAt,
            LastSeenAtUtc = collectedAt,
        };

    private static bool Same(외부데이터정규화Record left, 외부데이터정규화Record right)
        => Comparable(left) == Comparable(right);

    private static string Comparable(외부데이터정규화Record value)
        => string.Join('|', value.RecordKey, value.StableId, value.SourceId, value.DatasetId, value.RegionStableId,
            value.MetricCode, value.TextValue, value.UnitCode, value.EvidenceAsOfUtc.ToUniversalTime().ToString("O"),
            value.SpatialPrecisionCode, value.TemporalPrecisionCode, value.QualityCode, value.LimitationCode,
            value.DimensionKey, value.SourceVersion, value.DataRevision);

    private static async Task RenderPanelAsync(string pdfPath, string panelFolder, Panel panel)
    {
        var prefix = Path.Combine(panelFolder, Path.GetFileNameWithoutExtension(panel.FileName)[..^3]);
        var startInfo = new ProcessStartInfo
        {
            FileName = "pdftoppm",
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in new[]
        {
            "-png", "-r", "110", "-f", panel.Page.ToString(), "-l", panel.Page.ToString(),
            "-x", panel.X.ToString(), "-y", panel.Y.ToString(), "-W", panel.Width.ToString(), "-H", panel.Height.ToString(),
            pdfPath, prefix,
        })
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("JungnangMarketPanelRendererUnavailable");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidDataException("JungnangMarketPanelRenderTimeout:" + panel.Slug);
        }
        Require(process.ExitCode == 0, "JungnangMarketPanelRenderFailed:" + panel.Slug + ":" + await process.StandardError.ReadToEndAsync());
        Require(File.Exists(Path.Combine(panelFolder, panel.FileName)), "JungnangMarketPanelRenderOutputMissing:" + panel.Slug);
    }

    private static (int Width, int Height) ReadPngDimensions(string path)
    {
        var header = new byte[24];
        using var stream = File.OpenRead(path);
        Require(stream.Read(header, 0, header.Length) == header.Length, "JungnangMarketPanelPngHeaderMissing");
        Require(header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }), "JungnangMarketPanelNotPng");
        return (ReadBigEndianInt32(header, 16), ReadBigEndianInt32(header, 20));
    }

    private static int ReadBigEndianInt32(byte[] bytes, int offset)
        => (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];

    private static async Task<byte[]> ReadBoundedAsync(HttpClient client, string url, int maximumBytes)
    {
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        Require(response.IsSuccessStatusCode, "JungnangMarketVisualHttpStatus:" + (int)response.StatusCode);
        if (response.Content.Headers.ContentLength is long length)
            Require(length <= maximumBytes, "JungnangMarketVisualResponseTooLarge");
        await using var input = await response.Content.ReadAsStreamAsync();
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        while (await input.ReadAsync(buffer) is var read && read > 0)
        {
            Require(output.Length + read <= maximumBytes, "JungnangMarketVisualResponseTooLarge");
            await output.WriteAsync(buffer.AsMemory(0, read));
        }
        return output.ToArray();
    }

    private static DateTimeOffset TrimToMicrosecond(DateTimeOffset value)
        => new(value.Ticks - value.Ticks % 10, TimeSpan.Zero);

    private static string Hash(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}
