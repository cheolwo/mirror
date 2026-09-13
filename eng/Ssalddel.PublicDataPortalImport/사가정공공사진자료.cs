using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

internal static class 사가정공공사진자료
{
    private const string RelativeFolder = "artifacts/local/public-data/sagajeong-public-photos-20260913-r2";
    private const string SourceId = "wikimedia-commons";
    private const string DatasetId = "sagajeong-station-public-photo-review";
    private const string SearchSourceId = "station-public-photo-search-review";
    private const string SearchDatasetId = "sagajeong-market-and-jungnang-public-photo-search";
    private const string Revision = "sagajeong-public-photo-collection.r2";
    private const string Region = "point:kr:seoul:jungnang:sagajeong-station";
    private const string CommonsCategoryApi = "https://commons.wikimedia.org/w/api.php?action=query&generator=categorymembers&gcmtitle=Category%3ASagajeong_Station&gcmtype=file&gcmlimit=50&prop=imageinfo&iiprop=url%7Csize%7Cmime%7Cextmetadata&format=json&formatversion=2";

    private sealed record Candidate(
        string Title,
        string FileName,
        string Author,
        string PhotoDate,
        string LicenseCode,
        string? LicenseUrl,
        string SubjectCode,
        string EvidenceClass,
        bool LicenseProfileAccepted);

    private static readonly Candidate[] Candidates =
    [
        new("File:Sgjs01.jpg", "Sgjs01.jpg", "Marcopolis", "2007-12-06", "Public domain", null,
            "StationIdentitySign", "BuildingSpecificVisualEvidence", true),
        new("File:Sgjs02.jpg", "Sgjs02.jpg", "Marcopolis", "2007-12-11", "Public domain", null,
            "StationInteriorPlatform", "BuildingSpecificVisualEvidence", true),
        new("File:Sgjs03.jpg", "Sgjs03.jpg", "Marcopolis", "2007-12-11", "Public domain", null,
            "StationInteriorPlatform", "BuildingSpecificVisualEvidence", true),
        new("File:Seoul-metro-722-Sagajeong-station-entrance-1-20181123-092938.jpg",
            "Seoul-metro-722-Sagajeong-station-entrance-1-20181123-092938.jpg", "LERK", "2018-11-23",
            "CC BY-SA 4.0", "https://creativecommons.org/licenses/by-sa/4.0", "StationEntrance1",
            "BuildingSpecificVisualEvidence", false),
        new("File:Seoul-metro-722-Sagajeong-station-entrance-2-20181123-093122.jpg",
            "Seoul-metro-722-Sagajeong-station-entrance-2-20181123-093122.jpg", "LERK", "2018-11-23",
            "CC BY-SA 4.0", "https://creativecommons.org/licenses/by-sa/4.0", "StationEntrance2",
            "BuildingSpecificVisualEvidence", false),
    ];

    private static readonly string[] MarketQueries =
    [
        "사가정시장",
        "Sagajeong Market",
        "면목로44길 사가정시장",
        "면목동 시장",
    ];

    public static async Task RunAsync(string mode, string repositoryRoot, IDictionary<string, object?> result)
    {
        if (mode == "acquire")
        {
            await AcquireAsync(repositoryRoot, result);
            return;
        }

        if (mode is not ("self-test" or "preview" or "apply" or "verify"))
            throw new InvalidDataException("SagajeongPhotoModeInvalid");

        var folder = Path.Combine(repositoryRoot, RelativeFolder);
        var receiptPath = Path.Combine(folder, "acquisition.json");
        Require(File.Exists(receiptPath), "SagajeongPhotoReceiptMissing");
        using var receipt = JsonDocument.Parse(await File.ReadAllTextAsync(receiptPath));
        var collectedAt = ValidateReceiptAndFiles(receipt.RootElement, folder);
        var records = BuildRecords(receipt.RootElement, collectedAt);
        Require(records.Count == 9, "SagajeongPhotoNormalizedCountChanged");

        if (mode == "self-test")
        {
            var second = BuildRecords(receipt.RootElement, collectedAt);
            Require(records.Select(Comparable).SequenceEqual(second.Select(Comparable)), "SagajeongPhotoProjectionNondeterministic");
            Require(records.Count(record => record.MetricCode == "visual-evidence-candidate") == 5, "SagajeongPhotoCandidateCountChanged");
            Require(records.Count(record => record.MetricCode == "visual-evidence-rights-blocked") == 3, "SagajeongPhotoRightsBlockCountChanged");
            Require(records.Single(record => record.MetricCode == "visual-evidence-search-miss").QualityCode == "Unavailable", "SagajeongMarketSearchMissMissing");
            result["selfTestsPassed"] = 18;
            result["downloadedImages"] = 5;
            result["exactMarketImages"] = 0;
            result["rightsBlockedKoglItems"] = 3;
            result["mode"] = mode;
            return;
        }

        var options = await 로컬공공자료Db.OptionsAsync(repositoryRoot);
        var keys = records.Select(record => record.RecordKey).ToList();
        await using (var db = new PublicDataIngestionDbContext(options))
        {
            var before = await db.NormalizedRecords.AsNoTracking().Where(record => keys.Contains(record.RecordKey)).ToListAsync();
            Require(before.All(stored => records.Any(candidate => Same(candidate, stored))), "SagajeongPhotoExistingRecordConflict");
            result["beforeCount"] = before.Count;
            if (mode == "apply")
            {
                await db.Database.OpenConnectionAsync();
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT GET_LOCK('mirror:public-data:sagajeong-public-photo-r2',0)";
                Require(Convert.ToInt32(await command.ExecuteScalarAsync()) == 1, "SagajeongPhotoImportBusy");
                await using var transaction = await db.Database.BeginTransactionAsync();
                result["databaseWriteAttempted"] = true;

                var registration = new 평창군공공공간원본등록Service(db);
                foreach (var candidate in Candidates)
                {
                    var registered = await registration.RegisterFileAsync(
                        Path.Combine(folder, candidate.FileName),
                        new 공공공간원본등록Request(
                            SourceId,
                            DatasetId,
                            "commons-file-metadata-observed-2026-09-13",
                            Revision,
                            DateTimeOffset.Parse(candidate.PhotoDate + "T00:00:00Z"),
                            "image/jpeg",
                            RelativeFolder + "/" + candidate.FileName));
                    foreach (var record in records.Where(record => record.StableId.EndsWith(candidate.FileName, StringComparison.Ordinal)))
                        record.RawSnapshotId = registered.RawSnapshotId;
                }

                var receiptRegistration = await registration.RegisterFileAsync(
                    receiptPath,
                    new 공공공간원본등록Request(
                        SearchSourceId,
                        SearchDatasetId,
                        "official-search-observed-2026-09-13",
                        Revision,
                        collectedAt,
                        "application/json",
                        RelativeFolder + "/acquisition.json"));
                foreach (var record in records.Where(record => record.RawSnapshotId == 0))
                    record.RawSnapshotId = receiptRegistration.RawSnapshotId;

                var saved = await new EfExternalDataIngestionStore(db).UpsertNormalizedAsync(records);
                Require(saved.UpdatedCount == 0, "SagajeongPhotoUnexpectedUpdate");
                if (receiptRegistration.Inserted)
                {
                    var snapshot = await db.RawSnapshots.SingleAsync(item => item.Id == receiptRegistration.RawSnapshotId);
                    snapshot.CollectedAtUtc = collectedAt;
                    var run = await db.IngestionRuns.SingleAsync(item => item.Id == snapshot.FirstCollectionRunId);
                    run.StatusCode = 외부데이터수집StatusCodes.Partial;
                    run.FetchedCount = 5;
                    run.NormalizedCount = records.Count;
                    run.RejectedCount = 3;
                    run.InsertedCount = saved.InsertedCount;
                    run.ExistingCount = saved.ExistingCount;
                    run.ErrorCode = "PendingHumanReview";
                    run.ErrorSummary = "Private review only; exact Sagajeong Market photo unavailable, KOGL Type 4 items metadata-only, CC BY-SA files await project license-profile review; no Blender, Unity, publication or runtime use.";
                    await db.SaveChangesAsync();
                }
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
        if (mode != "preview") Require(actual.Count == records.Count, "SagajeongPhotoReadbackCountMismatch");
        Require(actual.All(stored => records.Any(candidate => Same(candidate, stored))
            && stored.RawSnapshot != null
            && stored.RawSnapshot.ContentHashSha256.Length == 64), "SagajeongPhotoReadbackMismatch");
        var receiptHash = Hash(await File.ReadAllBytesAsync(receiptPath));
        foreach (var stored in actual)
        {
            if (stored.MetricCode == "visual-evidence-candidate")
            {
                using var text = JsonDocument.Parse(stored.TextValue);
                Require(stored.RawSnapshot!.ContentHashSha256 == text.RootElement.GetProperty("sha256").GetString(), "SagajeongPhotoRawImageHashMismatch");
            }
            else
            {
                Require(stored.RawSnapshot!.ContentHashSha256 == receiptHash, "SagajeongPhotoRawReceiptHashMismatch");
            }
        }
        result["verifiedRows"] = actual.Count;
        result["target"] = "hongdal-mysql-1 / hongdal_dev";
        result["mode"] = mode;
    }

    private static async Task AcquireAsync(string repositoryRoot, IDictionary<string, object?> result)
    {
        var folder = Path.Combine(repositoryRoot, RelativeFolder);
        Require(!Directory.Exists(folder), "SagajeongPhotoAcquisitionFolderExists");
        Directory.CreateDirectory(folder);

        using var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All, AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SsalddelPublicDataCollector/1.0 (local research)");

        var categoryBytes = await ReadBoundedAsync(client, CommonsCategoryApi, 2 * 1024 * 1024);
        await File.WriteAllBytesAsync(Path.Combine(folder, "commons-category.json"), categoryBytes);
        using var category = JsonDocument.Parse(categoryBytes);
        var pages = category.RootElement.GetProperty("query").GetProperty("pages").EnumerateArray().ToArray();
        Require(pages.Length == 10, "SagajeongCommonsCategoryCountChanged");

        var collectedAt = TrimToMicrosecond(DateTimeOffset.UtcNow);
        var downloaded = new List<object>();
        foreach (var candidate in Candidates)
        {
            var page = pages.SingleOrDefault(item => item.GetProperty("title").GetString() == candidate.Title);
            Require(page.ValueKind != JsonValueKind.Undefined, "SagajeongCommonsCandidateMissing:" + candidate.FileName);
            var imageInfo = page.GetProperty("imageinfo")[0];
            var metadata = imageInfo.GetProperty("extmetadata");
            var license = metadata.GetProperty("LicenseShortName").GetProperty("value").GetString();
            Require(license == candidate.LicenseCode, "SagajeongCommonsLicenseChanged:" + candidate.FileName);
            Require(imageInfo.GetProperty("mime").GetString() == "image/jpeg", "SagajeongCommonsMimeChanged:" + candidate.FileName);
            var sourceUrl = imageInfo.GetProperty("url").GetString()!;
            Require(new Uri(sourceUrl).Host == "upload.wikimedia.org", "SagajeongCommonsFileHostChanged");
            var bytes = await ReadBoundedAsync(client, sourceUrl, 8 * 1024 * 1024);
            var path = Path.Combine(folder, candidate.FileName);
            await File.WriteAllBytesAsync(path, bytes);
            downloaded.Add(new
            {
                candidate.Title,
                candidate.FileName,
                candidate.Author,
                candidate.PhotoDate,
                sourcePage = "https://commons.wikimedia.org/wiki/" + Uri.EscapeDataString(candidate.Title.Replace(' ', '_')),
                sourceUrl,
                licenseCode = candidate.LicenseCode,
                licenseUrl = candidate.LicenseUrl,
                licenseCommercialUseAllowed = true,
                licenseModificationAllowed = true,
                candidate.LicenseProfileAccepted,
                candidate.SubjectCode,
                candidate.EvidenceClass,
                contentReviewStatus = "PendingHumanReview",
                modelDerivationAllowed = false,
                gameDistributionAllowed = false,
                sha256 = Hash(bytes),
                byteLength = bytes.Length,
                mime = "image/jpeg",
            });
            await Task.Delay(1000);
        }

        var searchResults = new List<object>();
        foreach (var query in MarketQueries)
        {
            var url = "https://commons.wikimedia.org/w/api.php?action=query&list=search&srnamespace=6&srlimit=20&format=json&formatversion=2&srsearch=" + Uri.EscapeDataString(query);
            var bytes = await ReadBoundedAsync(client, url, 1024 * 1024);
            using var search = JsonDocument.Parse(bytes);
            var totalHits = search.RootElement.GetProperty("query").GetProperty("searchinfo").GetProperty("totalhits").GetInt32();
            var titles = search.RootElement.GetProperty("query").GetProperty("search").EnumerateArray()
                .Select(item => item.GetProperty("title").GetString()).ToArray();
            searchResults.Add(new { query, totalHits, titles });
            await Task.Delay(500);
        }
        var searchBytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = "ssalddel.public-photo-search.v1",
            source = "Wikimedia Commons MediaWiki API",
            searchedAtUtc = collectedAt,
            results = searchResults,
        }, JsonOptions);
        await File.WriteAllBytesAsync(Path.Combine(folder, "commons-market-search.json"), searchBytes);

        var receiptBytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = "ssalddel.station-public-photo-acquisition.v1",
            revision = Revision,
            collectedAtUtc = collectedAt,
            scope = new
            {
                stationStableId = "station:kr:kric:s1107:0722",
                marketCandidateStableId = "landmark-candidate:kr:seoul:jungnang:sagajeong-market.r1",
                privateReviewOnly = true,
                unityRuntimeAuthorized = false,
                blenderWorkAuthorized = false,
                distributionApproved = false,
            },
            rawMetadata = new[]
            {
                new { fileName = "commons-category.json", sourceUrl = CommonsCategoryApi, sha256 = Hash(categoryBytes), byteLength = categoryBytes.Length },
                new { fileName = "commons-market-search.json", sourceUrl = "Wikimedia Commons MediaWiki API search", sha256 = Hash(searchBytes), byteLength = searchBytes.Length },
            },
            files = downloaded,
            exactMarketSearch = new
            {
                queries = MarketQueries,
                exactEligibleImageCount = 0,
                resultCode = "ExactMarketPhotoUnavailable",
                fallbackGenerated = false,
            },
            koglMetadataOnly = new[]
            {
                new { stableId = "kogl:recommend:139170", title = "중랑구 전경", provider = "서울특별시 중랑구", capturedYear = (int?)null, licenseCode = "KOGL-Type4", sourcePage = "https://www.kogl.or.kr/recommend/recommendDivView.do?atcUrl=personal&division=img&recommendIdx=139170", evidenceClass = "StyleResearchOnly", decision = "MetadataOnlyRejectedForModificationAndCommercialProfile" },
                new { stableId = "kogl:recommend:178072", title = "서울시 중랑구 전경", provider = "서울특별시 중랑구", capturedYear = (int?)2026, licenseCode = "KOGL-Type4", sourcePage = "https://www.kogl.or.kr/recommend/recommendDivView.do?atcUrl=personal&division=img&recommendIdx=178072", evidenceClass = "StyleResearchOnly", decision = "MetadataOnlyRejectedForModificationAndCommercialProfile" },
                new { stableId = "kogl:recommend:177646", title = "중랑구 옹기문화마당", provider = "서울특별시 중랑구", capturedYear = (int?)2026, licenseCode = "KOGL-Type4", sourcePage = "https://www.kogl.or.kr/recommend/recommendDivView.do?atcUrl=personal&division=img&recommendIdx=177646", evidenceClass = "StyleResearchOnly", decision = "MetadataOnlyRejectedForModificationAndCommercialProfile" },
            },
            attributionBoundary = new
            {
                publicDomainFilesRequireNoLicenseAttributionButSourceReceiptIsRetained = true,
                ccBySaFilesRequireAttributionLicenseLinkChangeNoticeAndShareAlike = true,
                ccBySaProjectProfileDecision = "Pending",
                koglType4FilesDownloaded = false,
            },
        }, JsonOptions);
        await File.WriteAllBytesAsync(Path.Combine(folder, "acquisition.json"), receiptBytes);

        result["mode"] = "acquire";
        result["folder"] = RelativeFolder;
        result["downloadedImages"] = downloaded.Count;
        result["exactMarketImages"] = 0;
        result["rightsBlockedKoglItems"] = 3;
        result["receiptSha256"] = Hash(receiptBytes);
    }

    private static DateTimeOffset ValidateReceiptAndFiles(JsonElement receipt, string folder)
    {
        Require(receipt.GetProperty("schemaVersion").GetString() == "ssalddel.station-public-photo-acquisition.v1", "SagajeongPhotoReceiptSchemaChanged");
        Require(receipt.GetProperty("revision").GetString() == Revision, "SagajeongPhotoReceiptRevisionChanged");
        var scope = receipt.GetProperty("scope");
        Require(scope.GetProperty("privateReviewOnly").GetBoolean()
            && !scope.GetProperty("unityRuntimeAuthorized").GetBoolean()
            && !scope.GetProperty("blenderWorkAuthorized").GetBoolean()
            && !scope.GetProperty("distributionApproved").GetBoolean(), "SagajeongPhotoAuthorityBoundaryChanged");
        var files = receipt.GetProperty("files").EnumerateArray().ToArray();
        Require(files.Length == Candidates.Length, "SagajeongPhotoReceiptFileCountChanged");
        foreach (var candidate in Candidates)
        {
            var item = files.Single(value => value.GetProperty("FileName").GetString() == candidate.FileName);
            Require(item.GetProperty("licenseCode").GetString() == candidate.LicenseCode, "SagajeongPhotoReceiptLicenseChanged");
            Require(!item.GetProperty("modelDerivationAllowed").GetBoolean() && !item.GetProperty("gameDistributionAllowed").GetBoolean(), "SagajeongPhotoPrematurePromotion");
            var bytes = File.ReadAllBytes(Path.Combine(folder, candidate.FileName));
            Require(Hash(bytes) == item.GetProperty("sha256").GetString(), "SagajeongPhotoFileHashChanged:" + candidate.FileName);
            Require(bytes.Length == item.GetProperty("byteLength").GetInt32(), "SagajeongPhotoFileLengthChanged:" + candidate.FileName);
        }
        var searches = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "commons-market-search.json"))).RootElement.GetProperty("results").EnumerateArray().ToArray();
        Require(searches.Length == MarketQueries.Length && searches.All(item => item.GetProperty("totalHits").GetInt32() == 0), "SagajeongMarketSearchResultChanged");
        Require(receipt.GetProperty("koglMetadataOnly").GetArrayLength() == 3
            && receipt.GetProperty("koglMetadataOnly").EnumerateArray().All(item => item.GetProperty("licenseCode").GetString() == "KOGL-Type4"), "SagajeongKoglRightsBoundaryChanged");
        return receipt.GetProperty("collectedAtUtc").GetDateTimeOffset();
    }

    private static List<외부데이터정규화Record> BuildRecords(JsonElement receipt, DateTimeOffset collectedAt)
    {
        var output = new List<외부데이터정규화Record>();
        foreach (var item in receipt.GetProperty("files").EnumerateArray())
        {
            var fileName = item.GetProperty("FileName").GetString()!;
            var candidate = Candidates.Single(value => value.FileName == fileName);
            var evidenceAt = DateTimeOffset.Parse(candidate.PhotoDate + "T00:00:00Z");
            var text = JsonSerializer.Serialize(new
            {
                title = candidate.Title,
                fileName,
                sourcePage = item.GetProperty("sourcePage").GetString(),
                sha256 = item.GetProperty("sha256").GetString(),
                candidate.Author,
                candidate.PhotoDate,
                candidate.LicenseCode,
                candidate.LicenseUrl,
                candidate.SubjectCode,
                candidate.EvidenceClass,
                candidate.LicenseProfileAccepted,
                contentReviewStatus = "PendingHumanReview",
                modelDerivationAllowed = false,
                gameDistributionAllowed = false,
            });
            output.Add(Record(
                "visual-evidence:commons:sagajeong-station:" + fileName,
                SourceId,
                DatasetId,
                Region,
                "visual-evidence-candidate",
                evidenceAt,
                "file=" + fileName,
                text,
                "exact-station-category",
                "photo-date",
                "PendingHumanReview",
                candidate.LicenseProfileAccepted
                    ? "PrivateReviewOnly;ExactStationNotExteriorBuildingReplacement;ContentReviewPending;NoBlender;NoUnity;NoDistribution"
                    : "PrivateReviewOnly;CCBYSAProjectProfilePending;ContentReviewPending;NoBlender;NoUnity;NoDistribution",
                "commons-file-metadata-observed-2026-09-13",
                collectedAt));
        }

        output.Add(Record(
            "visual-evidence-search:commons:sagajeong-market:20260913",
            SearchSourceId,
            SearchDatasetId,
            Region,
            "visual-evidence-search-miss",
            collectedAt,
            "target=sagajeong-market;provider=commons",
            JsonSerializer.Serialize(new { queries = MarketQueries, exactEligibleImageCount = 0, resultCode = "ExactMarketPhotoUnavailable", fallbackGenerated = false }),
            "query-only",
            "collection-instant",
            "Unavailable",
            "ExactMarketImageNotFound;NoStreetViewFallback;NoGeneratedFacadeSubstitution;NoBlender;NoUnity",
            "official-search-observed-2026-09-13",
            collectedAt));

        foreach (var item in receipt.GetProperty("koglMetadataOnly").EnumerateArray())
        {
            var stableId = item.GetProperty("stableId").GetString()!;
            output.Add(Record(
                "visual-evidence:" + stableId,
                SearchSourceId,
                SearchDatasetId,
                "region:kr:sig:11260",
                "visual-evidence-rights-blocked",
                collectedAt,
                "source=" + stableId,
                item.GetRawText(),
                "sigungu-image",
                "collection-instant",
                "RejectedByRightsPolicy",
                "KOGLType4;CommercialUseForbidden;ModificationForbidden;MetadataOnly;FileNotDownloaded;NoBlender;NoUnity",
                "official-search-observed-2026-09-13",
                collectedAt));
        }
        return output;
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

    private static async Task<byte[]> ReadBoundedAsync(HttpClient client, string url, int maximumBytes)
    {
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        Require(response.IsSuccessStatusCode, "SagajeongPhotoHttpStatus:" + (int)response.StatusCode);
        if (response.Content.Headers.ContentLength is long length)
            Require(length <= maximumBytes, "SagajeongPhotoResponseTooLarge");
        await using var input = await response.Content.ReadAsStreamAsync();
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        while (await input.ReadAsync(buffer) is var read && read > 0)
        {
            Require(output.Length + read <= maximumBytes, "SagajeongPhotoResponseTooLarge");
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
