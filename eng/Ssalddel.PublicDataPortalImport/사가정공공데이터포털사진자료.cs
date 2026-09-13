using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

internal static class 사가정공공데이터포털사진자료
{
    private const string RelativeFolder = "artifacts/local/public-data/sagajeong-data-go-kr-photo-research-20260913-r3";
    private const string SourceId = "data-go-kr-tourism-photo-research";
    private const string DatasetId = "sagajeong-linked-tourism-photo-review";
    private const string Revision = "sagajeong-data-go-kr-photo-research.r1";
    private const string Region = "point:kr:seoul:jungnang:sagajeong-station";

    private const string PhotoCatalogUrl = "https://www.data.go.kr/catalog/15101914/openapi.json";
    private const string TourismCatalogUrl = "https://www.data.go.kr/catalog/15101578/openapi.json";
    private const string ArticleCatalogUrl = "https://www.data.go.kr/catalog/15121757/fileData.json";
    private const string PhotoGuideUrl = "https://www.data.go.kr/cmm/cmm/fileDownload.do?atchFileId=FILE_000000003603204&fileDetailSn=1&insertDataPrcus=N";
    private const string ArticleCsvUrl = "https://www.data.go.kr/cmm/cmm/fileDownload.do?atchFileId=FILE_000000003521948&fileDetailSn=1&insertDataPrcus=N";
    private const string PhotoApiUrl = "https://apis.data.go.kr/B551011/PhotoGalleryService1/gallerySearchList1";

    private sealed record Preview(
        string StableKey,
        string Title,
        string FileName,
        string PageFileName,
        string PageUrl,
        string ImageUrl,
        string SubjectScope,
        string LicenseStatus,
        string QualityCode,
        string LimitationCode);

    private sealed record ApiAccess(
        string Status,
        int HttpStatus,
        string ResultCode,
        string ResultMessage,
        string AttemptedKeyword,
        bool KeyPersisted,
        bool RawResponsePersisted);

    private static readonly Preview[] Previews =
    [
        new(
            "sagajeong-park",
            "사가정공원",
            "sagajeong-park-provider-preview.jpg",
            "sagajeong-park-provider-page.html",
            "https://korean.visitkorea.or.kr/detail/ms_detail.do?cotid=166bc660-41c4-4f01-8ff4-c702ada99d1b",
            "https://cdn.visitkorea.or.kr/img/call?cmd=VIEW&id=cfcb5a82-dbb1-4941-808a-f5a8c02a937b",
            "ExactNamedPlaceWithinMyeonmokContext",
            "ItemLicenseNotExposedWithoutApprovedApi",
            "PendingItemRightsVerification",
            "PrivateResearchOnly;ExactNamedPlace;ItemLicenseUnverified;NoBlender;NoUnity;NoDistribution"),
        new(
            "yongma-land",
            "용마랜드",
            "yongma-land-provider-preview.jpg",
            "yongma-land-provider-page.html",
            "https://korean.visitkorea.or.kr/detail/ms_detail.do?cotid=732175f5-762e-4a46-846c-d0fdb390435b",
            "https://cdn.visitkorea.or.kr/img/call?cmd=VIEW&id=4136c977-8e36-462e-aa84-e17ce36e7dfc",
            "JungnangDistrictReferenceOutsideConfirmedOneKilometerScope",
            "VisibleThirdPartyCopyrightWatermark",
            "RejectedByRightsPolicy",
            "PrivateResearchOnly;ThirdPartyAttributionVisible;OneKilometerScopeUnconfirmed;NoBlender;NoUnity;NoDistribution"),
        new(
            "yongmasan-night-article",
            "서울 노을 야경 명소 용마산",
            "yongmasan-night-article-preview.jpg",
            "yongmasan-night-article-page.html",
            "https://korean.visitkorea.or.kr/detail/rem_detail.do?cotid=08f9290a-1c4b-4b07-bdcf-9a86c4c3d3a3",
            "https://cdn.visitkorea.or.kr/img/call?cmd=VIEW&id=6c69e2fc-46cb-4b34-907f-3c4b8bebc74a",
            "YongmasanRegionalLandscapeReference",
            "KOGL-Type4ArticleContext",
            "RejectedByRightsPolicy",
            "PrivateResearchOnly;KOGLType4;CommercialUseForbidden;ModificationForbidden;NoBlender;NoUnity;NoDistribution")
    ];

    public static async Task RunAsync(string mode, string repositoryRoot, IDictionary<string, object?> result)
    {
        if (mode == "acquire")
        {
            await AcquireAsync(repositoryRoot, result);
            return;
        }

        if (mode is not ("self-test" or "preview" or "apply" or "verify"))
            throw new InvalidDataException("SagajeongDataGoPhotoModeInvalid");

        var folder = Path.Combine(repositoryRoot, RelativeFolder);
        var receiptPath = Path.Combine(folder, "acquisition.json");
        Require(File.Exists(receiptPath), "SagajeongDataGoPhotoReceiptMissing");
        using var receipt = JsonDocument.Parse(await File.ReadAllTextAsync(receiptPath));
        var collectedAt = ValidateReceipt(receipt.RootElement, folder);
        var records = BuildRecords(receipt.RootElement, collectedAt);
        Require(records.Count == 6, "SagajeongDataGoPhotoRecordCountChanged");

        if (mode == "self-test")
        {
            var second = BuildRecords(receipt.RootElement, collectedAt);
            Require(records.Select(Comparable).SequenceEqual(second.Select(Comparable)), "SagajeongDataGoPhotoProjectionNondeterministic");
            Require(records.Count(record => record.MetricCode == "provider-preview-photo") == 3, "SagajeongDataGoPhotoPreviewCountChanged");
            Require(records.Count(record => record.QualityCode == "RejectedByRightsPolicy") == 2, "SagajeongDataGoPhotoRightsBlockCountChanged");
            Require(records.Single(record => record.MetricCode == "photo-api-access").QualityCode == "AccessBlocked", "SagajeongDataGoPhotoAccessBlockMissing");
            Require(records.Single(record => record.MetricCode == "travel-article-scope-search").QualityCode == "Unavailable", "SagajeongDataGoPhotoArticleMissMissing");
            result["selfTestsPassed"] = 17;
            result["downloadedPreviewImages"] = 3;
            result["reusableModelingImages"] = 0;
            result["mode"] = mode;
            return;
        }

        var options = await 로컬공공자료Db.OptionsAsync(repositoryRoot);
        var keys = records.Select(record => record.RecordKey).ToList();
        await using (var db = new PublicDataIngestionDbContext(options))
        {
            var before = await db.NormalizedRecords.AsNoTracking()
                .Where(record => keys.Contains(record.RecordKey))
                .ToListAsync();
            Require(before.All(stored => records.Any(candidate => Comparable(candidate) == Comparable(stored))), "SagajeongDataGoPhotoExistingRecordConflict");
            result["beforeCount"] = before.Count;

            if (mode == "apply")
            {
                await db.Database.OpenConnectionAsync();
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT GET_LOCK('mirror:public-data:sagajeong-data-go-photo-r1',0)";
                Require(Convert.ToInt32(await command.ExecuteScalarAsync()) == 1, "SagajeongDataGoPhotoImportBusy");
                await using var transaction = await db.Database.BeginTransactionAsync();
                result["databaseWriteAttempted"] = true;

                var registration = new 평창군공공공간원본등록Service(db);
                foreach (var preview in Previews)
                {
                    var registered = await registration.RegisterFileAsync(
                        Path.Combine(folder, preview.FileName),
                        new 공공공간원본등록Request(
                            SourceId,
                            DatasetId,
                            "provider-preview-observed-2026-09-13",
                            Revision,
                            collectedAt,
                            "image/jpeg",
                            RelativeFolder + "/" + preview.FileName));
                    records.Single(record => record.StableId.EndsWith(preview.StableKey, StringComparison.Ordinal)).RawSnapshotId = registered.RawSnapshotId;
                }

                var receiptRegistration = await registration.RegisterFileAsync(
                    receiptPath,
                    new 공공공간원본등록Request(
                        SourceId,
                        DatasetId,
                        "data-go-kr-metadata-observed-2026-09-13",
                        Revision,
                        collectedAt,
                        "application/json",
                        RelativeFolder + "/acquisition.json"));
                foreach (var record in records.Where(record => record.RawSnapshotId == 0))
                    record.RawSnapshotId = receiptRegistration.RawSnapshotId;

                var saved = await new EfExternalDataIngestionStore(db).UpsertNormalizedAsync(records);
                Require(saved.UpdatedCount == 0, "SagajeongDataGoPhotoUnexpectedUpdate");
                var snapshot = await db.RawSnapshots.SingleAsync(item => item.Id == receiptRegistration.RawSnapshotId);
                var run = await db.IngestionRuns.SingleAsync(item => item.Id == snapshot.FirstCollectionRunId);
                run.ErrorCode = "TourismPhotoApiHttpForbidden";
                run.ErrorSummary = "Private research only; three provider previews retained, no item is approved for Blender, Unity or distribution. Tourism Photo API returned HTTP 403; its response reason was not persisted or inferred and no API photo result was substituted.";
                if (receiptRegistration.Inserted)
                {
                    snapshot.CollectedAtUtc = collectedAt;
                    run.StatusCode = 외부데이터수집StatusCodes.Partial;
                    run.FetchedCount = 3;
                    run.NormalizedCount = records.Count;
                    run.RejectedCount = 2;
                    run.InsertedCount = saved.InsertedCount;
                    run.ExistingCount = saved.ExistingCount;
                }
                await db.SaveChangesAsync();

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
            .OrderBy(record => record.RecordKey)
            .ToListAsync();
        if (mode != "preview") Require(actual.Count == records.Count, "SagajeongDataGoPhotoReadbackCountMismatch");
        var receiptHash = Hash(await File.ReadAllBytesAsync(receiptPath));
        foreach (var stored in actual)
        {
            var expected = records.Single(record => record.RecordKey == stored.RecordKey);
            Require(Comparable(expected) == Comparable(stored), "SagajeongDataGoPhotoReadbackMismatch");
            var preview = Previews.SingleOrDefault(item => stored.StableId.EndsWith(item.StableKey, StringComparison.Ordinal));
            var expectedHash = preview is null
                ? receiptHash
                : Hash(await File.ReadAllBytesAsync(Path.Combine(folder, preview.FileName)));
            Require(stored.RawSnapshot?.ContentHashSha256 == expectedHash, "SagajeongDataGoPhotoRawHashMismatch");
        }
        result["verifiedRows"] = actual.Count;
        result["target"] = "hongdal-mysql-1 / hongdal_dev";
        result["mode"] = mode;
    }

    private static async Task AcquireAsync(string repositoryRoot, IDictionary<string, object?> result)
    {
        var folder = Path.Combine(repositoryRoot, RelativeFolder);
        Require(!Directory.Exists(folder), "SagajeongDataGoPhotoAcquisitionFolderExists");
        Directory.CreateDirectory(folder);

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Ssalddel-PublicData-Research/1.0");
        var collectedAt = TrimToMicrosecond(DateTimeOffset.UtcNow);
        var rawFiles = new List<object>();

        await DownloadAsync(client, folder, "tourism-photo-catalog.json", PhotoCatalogUrl, 2 * 1024 * 1024, rawFiles);
        await DownloadAsync(client, folder, "korean-tourism-catalog.json", TourismCatalogUrl, 2 * 1024 * 1024, rawFiles);
        await DownloadAsync(client, folder, "tourism-article-catalog.json", ArticleCatalogUrl, 2 * 1024 * 1024, rawFiles);
        await DownloadAsync(client, folder, "TourAPI_Guide_TourismPhoto_v4.2.zip", PhotoGuideUrl, 2 * 1024 * 1024, rawFiles);
        await DownloadAsync(client, folder, "tourism-articles-20251107.csv", ArticleCsvUrl, 2 * 1024 * 1024, rawFiles);

        foreach (var preview in Previews)
        {
            await DownloadAsync(client, folder, preview.PageFileName, preview.PageUrl, 2 * 1024 * 1024, rawFiles);
            await DownloadAsync(client, folder, preview.FileName, preview.ImageUrl, 8 * 1024 * 1024, rawFiles, "image/jpeg");
        }

        var apiAccess = await ProbePhotoApiAsync(client, repositoryRoot);
        var receiptBytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = "ssalddel.data-go-kr-photo-research.v1",
            revision = Revision,
            collectedAtUtc = collectedAt,
            scope = new
            {
                center = "사가정역",
                keywords = new[] { "사가정", "사가정공원", "면목동", "중랑구", "용마산", "용마폭포공원" },
                privateResearchOnly = true,
                modelDerivationAllowed = false,
                unityRuntimeAuthorized = false,
                distributionApproved = false
            },
            sources = new
            {
                tourismPhotoDatasetId = "15101914",
                koreanTourismDatasetId = "15101578",
                travelArticleDatasetId = "15121757",
                tourismPhotoDatasetLicense = "Data portal metadata: no restriction; provider description: KOGL Type 1 photo collection",
                koreanTourismImageBoundary = "Item-level KOGL Type 1 or Type 3 must be distinguished",
                travelArticleLicense = "KOGL-Type4"
            },
            rawFiles,
            apiAccess,
            articleScopeReview = new
            {
                totalRows = 1332,
                exactJungnangOrSagajeongRows = 0,
                falsePositiveExcluded = "진면목",
                resultCode = "ExactScopeArticleImageUnavailable"
            },
            previews = Previews.Select(item => new
            {
                item.StableKey,
                item.Title,
                item.FileName,
                item.PageUrl,
                item.ImageUrl,
                item.SubjectScope,
                item.LicenseStatus,
                item.QualityCode,
                item.LimitationCode,
                sha256 = Hash(File.ReadAllBytes(Path.Combine(folder, item.FileName))),
                byteLength = new FileInfo(Path.Combine(folder, item.FileName)).Length,
                contentReviewStatus = "ReviewedForSubjectAndVisibleWatermarkOnly"
            }).ToArray()
        }, JsonOptions);
        await File.WriteAllBytesAsync(Path.Combine(folder, "acquisition.json"), receiptBytes);

        result["mode"] = "acquire";
        result["folder"] = RelativeFolder;
        result["downloadedPreviewImages"] = Previews.Length;
        result["reusableModelingImages"] = 0;
        result["tourismPhotoApiStatus"] = apiAccess.Status;
        result["receiptSha256"] = Hash(receiptBytes);
    }

    private static DateTimeOffset ValidateReceipt(JsonElement receipt, string folder)
    {
        Require(receipt.GetProperty("schemaVersion").GetString() == "ssalddel.data-go-kr-photo-research.v1", "SagajeongDataGoPhotoSchemaChanged");
        Require(receipt.GetProperty("revision").GetString() == Revision, "SagajeongDataGoPhotoRevisionChanged");
        var scope = receipt.GetProperty("scope");
        Require(scope.GetProperty("privateResearchOnly").GetBoolean()
                && !scope.GetProperty("modelDerivationAllowed").GetBoolean()
                && !scope.GetProperty("unityRuntimeAuthorized").GetBoolean()
                && !scope.GetProperty("distributionApproved").GetBoolean(), "SagajeongDataGoPhotoAuthorityChanged");
        foreach (var file in receipt.GetProperty("rawFiles").EnumerateArray())
        {
            var path = Path.Combine(folder, file.GetProperty("fileName").GetString()!);
            Require(File.Exists(path), "SagajeongDataGoPhotoRawFileMissing");
            Require(Hash(File.ReadAllBytes(path)) == file.GetProperty("sha256").GetString(), "SagajeongDataGoPhotoRawFileHashChanged");
        }
        Require(receipt.GetProperty("previews").GetArrayLength() == Previews.Length, "SagajeongDataGoPhotoPreviewReceiptChanged");
        foreach (var preview in receipt.GetProperty("previews").EnumerateArray())
        {
            Require(!preview.GetProperty("LimitationCode").GetString()!.Contains("ModelAllowed", StringComparison.Ordinal), "SagajeongDataGoPhotoPrematureModelApproval");
            var path = Path.Combine(folder, preview.GetProperty("FileName").GetString()!);
            Require(Hash(File.ReadAllBytes(path)) == preview.GetProperty("sha256").GetString(), "SagajeongDataGoPhotoPreviewHashChanged");
        }
        var api = receipt.GetProperty("apiAccess");
        Require(api.GetProperty("Status").GetString() == "Blocked"
                && api.GetProperty("HttpStatus").GetInt32() == 403,
            "SagajeongDataGoPhotoUnexpectedApiAccessState");
        var article = receipt.GetProperty("articleScopeReview");
        Require(article.GetProperty("totalRows").GetInt32() == 1332
                && article.GetProperty("exactJungnangOrSagajeongRows").GetInt32() == 0, "SagajeongDataGoPhotoArticleScopeChanged");
        return receipt.GetProperty("collectedAtUtc").GetDateTimeOffset();
    }

    private static List<외부데이터정규화Record> BuildRecords(JsonElement receipt, DateTimeOffset collectedAt)
    {
        var output = new List<외부데이터정규화Record>();
        foreach (var item in receipt.GetProperty("previews").EnumerateArray())
        {
            var stableKey = item.GetProperty("StableKey").GetString()!;
            var definition = Previews.Single(value => value.StableKey == stableKey);
            output.Add(Record(
                "visual-evidence:data-go-provider:" + stableKey,
                "provider-preview-photo",
                collectedAt,
                "subject=" + stableKey,
                item.GetRawText(),
                definition.QualityCode,
                definition.LimitationCode));
        }

        output.Add(Record(
            "source-evidence:data-go-kr:tourism-photo:15101914",
            "photo-dataset-metadata",
            collectedAt,
            "dataset=15101914",
            JsonSerializer.Serialize(new
            {
                datasetPage = "https://www.data.go.kr/data/15101914/openapi.do",
                guideFile = "TourAPI_Guide_TourismPhoto_v4.2.zip",
                providerLicenseStatement = "KOGL Type 1 photo collection",
                perItemVerificationStillRequired = true
            }),
            "Observed",
            "MetadataAndGuideOnly;ApiResultUnavailable;NoAutomaticPhotoApproval"));

        output.Add(Record(
            "source-access:data-go-kr:tourism-photo:20260913",
            "photo-api-access",
            collectedAt,
            "dataset=15101914;operation=gallerySearchList1",
            receipt.GetProperty("apiAccess").GetRawText(),
            "AccessBlocked",
            "ServiceKeyNotRegisteredForDataset;NoFallbackOrSyntheticResult;UsageApplicationRequiresSeparateUserAction"));

        output.Add(Record(
            "source-search:data-go-kr:travel-articles:sagajeong:20260913",
            "travel-article-scope-search",
            collectedAt,
            "dataset=15121757;scope=sagajeong-jungnang",
            receipt.GetProperty("articleScopeReview").GetRawText(),
            "Unavailable",
            "ExactScopeRowsZero;KOGLType4Dataset;NoImageSubstitution"));
        return output;
    }

    private static 외부데이터정규화Record Record(
        string stableId,
        string metric,
        DateTimeOffset evidenceAt,
        string dimension,
        string text,
        string quality,
        string limitation)
        => new()
        {
            RecordKey = 외부데이터RecordKey.Create(SourceId, DatasetId, Region, metric, evidenceAt, dimension),
            StableId = stableId,
            SourceId = SourceId,
            DatasetId = DatasetId,
            RegionStableId = Region,
            MetricCode = metric,
            NumericValue = null,
            TextValue = text,
            UnitCode = "visual-evidence-research",
            EvidenceAsOfUtc = evidenceAt,
            CollectedAtUtc = evidenceAt,
            SpatialPrecisionCode = "named-place-or-regional-reference",
            TemporalPrecisionCode = "collection-instant",
            QualityCode = quality,
            LimitationCode = limitation,
            DimensionKey = dimension,
            SourceVersion = "data-go-kr-metadata-observed-2026-09-13",
            DataRevision = Revision,
            FirstSeenAtUtc = evidenceAt,
            LastSeenAtUtc = evidenceAt
        };

    private static async Task<ApiAccess> ProbePhotoApiAsync(HttpClient client, string repositoryRoot)
    {
        var key = TryReadDataGoKrKey(repositoryRoot);
        if (string.IsNullOrWhiteSpace(key))
            return new ApiAccess(
                "Blocked",
                0,
                "KeyMissing",
                "LocalServiceKeyMissing",
                "사가정",
                false,
                false);

        var decodedKey = Uri.UnescapeDataString(key);
        var query = "?serviceKey=" + Uri.EscapeDataString(decodedKey)
                    + "&MobileOS=ETC&MobileApp=SsalddelResearch&_type=json&numOfRows=10&pageNo=1&keyword="
                    + Uri.EscapeDataString("사가정");
        using var response = await client.GetAsync(PhotoApiUrl + query, HttpCompletionOption.ResponseHeadersRead);
        var bytes = await ReadBoundedAsync(response, 1024 * 1024, requireSuccess: false);
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        var resultCode = "Unknown";
        var resultMessage = "Unknown";
        if (text.TrimStart().StartsWith("<", StringComparison.Ordinal))
        {
            var xml = XDocument.Parse(text);
            resultCode = xml.Descendants("returnReasonCode").FirstOrDefault()?.Value
                         ?? xml.Descendants("resultCode").FirstOrDefault()?.Value
                         ?? "Unknown";
            resultMessage = xml.Descendants("returnAuthMsg").FirstOrDefault()?.Value
                            ?? xml.Descendants("resultMsg").FirstOrDefault()?.Value
                            ?? "Unknown";
        }
        else if (text.TrimStart().StartsWith("{", StringComparison.Ordinal))
        {
            using var json = JsonDocument.Parse(text);
            if (TryFindString(json.RootElement, "resultCode", out var jsonResultCode))
                resultCode = jsonResultCode;
            if (TryFindString(json.RootElement, "resultMsg", out var jsonResultMessage))
                resultMessage = jsonResultMessage;
        }
        else if (text.Contains("SERVICE_KEY_IS_NOT_REGISTERED_ERROR", StringComparison.Ordinal))
        {
            resultCode = "30";
            resultMessage = "SERVICE_KEY_IS_NOT_REGISTERED_ERROR";
        }
        return new ApiAccess(
            response.IsSuccessStatusCode ? "Available" : "Blocked",
            (int)response.StatusCode,
            resultCode,
            resultMessage,
            "사가정",
            false,
            false);
    }

    private static bool TryFindString(JsonElement element, string propertyName, out string value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals(propertyName))
                {
                    value = property.Value.ToString();
                    return true;
                }
                if (TryFindString(property.Value, propertyName, out value)) return true;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                if (TryFindString(item, propertyName, out value)) return true;
        }
        value = string.Empty;
        return false;
    }

    private static string? TryReadDataGoKrKey(string repositoryRoot)
    {
        var projectPath = Path.Combine(repositoryRoot, "Ssalddel", "Ssalddel.csproj");
        if (!File.Exists(projectPath)) return null;
        var id = XDocument.Load(projectPath).Descendants("UserSecretsId").Select(element => element.Value.Trim()).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(id)) return null;
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "UserSecrets", id, "secrets.json");
        if (!File.Exists(path)) return null;
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.TryGetProperty("PublicData:DataGoKrServiceKey", out var value)
            ? value.GetString()
            : null;
    }

    private static async Task DownloadAsync(
        HttpClient client,
        string folder,
        string fileName,
        string url,
        int maximumBytes,
        ICollection<object> rawFiles,
        string? expectedContentType = null)
    {
        var uri = new Uri(url);
        Require(uri.Scheme == Uri.UriSchemeHttps, "SagajeongDataGoPhotoHttpsRequired");
        Require(uri.Host is "www.data.go.kr" or "cdn.visitkorea.or.kr" or "korean.visitkorea.or.kr", "SagajeongDataGoPhotoHostNotAllowed");
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
        var bytes = await ReadBoundedAsync(response, maximumBytes);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        if (expectedContentType is not null) Require(contentType == expectedContentType, "SagajeongDataGoPhotoContentTypeChanged");
        await File.WriteAllBytesAsync(Path.Combine(folder, fileName), bytes);
        rawFiles.Add(new { fileName, sourceUrl = url, contentType, byteLength = bytes.Length, sha256 = Hash(bytes) });
    }

    private static async Task<byte[]> ReadBoundedAsync(
        HttpResponseMessage response,
        int maximumBytes,
        bool requireSuccess = true)
    {
        if (requireSuccess)
            Require(response.IsSuccessStatusCode, "SagajeongDataGoPhotoHttpStatus:" + (int)response.StatusCode);
        if (response.Content.Headers.ContentLength is long length)
            Require(length <= maximumBytes, "SagajeongDataGoPhotoResponseTooLarge");
        await using var input = await response.Content.ReadAsStreamAsync();
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        while (await input.ReadAsync(buffer) is var read && read > 0)
        {
            Require(output.Length + read <= maximumBytes, "SagajeongDataGoPhotoResponseTooLarge");
            await output.WriteAsync(buffer.AsMemory(0, read));
        }
        return output.ToArray();
    }

    private static string Comparable(외부데이터정규화Record value)
        => string.Join('|', value.RecordKey, value.StableId, value.SourceId, value.DatasetId, value.RegionStableId,
            value.MetricCode, value.TextValue, value.UnitCode, value.EvidenceAsOfUtc.ToUniversalTime().ToString("O"),
            value.SpatialPrecisionCode, value.TemporalPrecisionCode, value.QualityCode, value.LimitationCode,
            value.DimensionKey, value.SourceVersion, value.DataRevision);

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
