using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

internal static class 무역소매Hs전수조사
{
    private const string ReferenceSourceId = "un-comtrade-reference";
    private const string ReviewSourceId = "manual-hs-retail-census-review";
    private const string ClassificationDatasetId = "hs-2022-h6-retail-census";
    private const string ReviewDatasetId = "hs-2022-h6-retail-census-review";
    private const string HsUrl = "https://comtradeapi.un.org/files/v1/app/reference/H6.json";
    private const string PolicyRelativePath = "eng/public-data/trade-retail/census-policy.v1.json";
    private const string FolderRelativePath = "artifacts/local/public-data/trade-retail/census/hs-2022-h6";
    private const string ClassificationRevision = "hs-2022-h6-retail-census-r1";
    private const string ReviewRevision = "hs-2022-h6-retail-census-review-r1";
    private static readonly DateTimeOffset ClassificationEvidenceAt = new(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public static async Task AcquireAsync(string root, Dictionary<string, object?> result)
    {
        var policyPath = Path.Combine(root, PolicyRelativePath);
        var policy = await ReadPolicyAsync(policyPath);
        var folder = Path.Combine(root, FolderRelativePath);
        Directory.CreateDirectory(folder);

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Ssalddel-PublicDataResearch/1.0");
        var collectedAt = DateTimeOffset.UtcNow;
        var referenceBytes = await http.GetByteArrayAsync(HsUrl);
        var referenceRows = ParseReference(referenceBytes, policy);
        var reviewed = await ReadObservedBatchReviewsAsync(root, referenceRows.Select(x => x.Id).ToHashSet(StringComparer.Ordinal));
        var statisticalSpecialCodes = policy.StatisticalSpecialCodes.ToHashSet(StringComparer.Ordinal);
        var reviewSeedBytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = "trade-retail-hs-census-review-seed.v1",
            policyObservedAt = policy.ObservedAt,
            classificationCode = policy.ClassificationCode,
            classificationVintage = policy.ClassificationVintage,
            codes = referenceRows.Select(row =>
            {
                reviewed.TryGetValue(row.Id, out var review);
                var batchIds = review?.BatchIds.OrderBy(x => x, StringComparer.Ordinal).ToArray() ?? [];
                var isStatisticalSpecial = statisticalSpecialCodes.Contains(row.Id);
                return new
                {
                    hs6 = row.Id,
                    retailTriageStatus = isStatisticalSpecial
                        ? "NonConsumerOrChannelInapplicable"
                        : review?.RetailTriageStatus ?? "PendingRetailTriage",
                    searchStatus = isStatisticalSpecial
                        ? "NotApplicable"
                        : review?.SearchStatus ?? "NotAttempted",
                    representativeProductStatus = isStatisticalSpecial
                        ? "NotApplicable"
                        : review?.RepresentativeProductStatus ?? "NotReviewed",
                    candidateCount = isStatisticalSpecial ? 0 : review?.CandidateCount ?? 0,
                    batchIds = isStatisticalSpecial ? [] : batchIds,
                    distributionApproved = false,
                };
            }).ToArray(),
        }, JsonOptions);

        var referencePath = Path.Combine(folder, "un-comtrade-h6.json");
        var reviewSeedPath = Path.Combine(folder, "review-queue.seed.json");
        await File.WriteAllBytesAsync(referencePath, referenceBytes);
        await File.WriteAllBytesAsync(reviewSeedPath, reviewSeedBytes);
        var receipt = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = "trade-retail-hs-census-acquisition.v1",
            collectedAtUtc = collectedAt,
            sourceUrl = HsUrl,
            policyRelativePath = PolicyRelativePath,
            policySha256 = Hash(policyPath),
            totalReferenceRows = policy.ExpectedReferenceRows,
            leafHs6Count = policy.ExpectedLeafHs6Count,
            legalHs6Count = policy.ExpectedLegalHs6Count,
            statisticalSpecialCodeCount = policy.StatisticalSpecialCodes.Count,
            reviewedCodeCount = reviewed.Count,
            observedCandidateCodeCount = reviewed.Count(x => x.Value.RetailTriageStatus == "ProductCandidateObserved"),
            files = new[]
            {
                ReceiptEntry("un-comtrade-h6.json", referencePath),
                ReceiptEntry("review-queue.seed.json", reviewSeedPath),
            },
        }, JsonOptions);
        await File.WriteAllBytesAsync(Path.Combine(folder, "acquisition.json"), receipt);

        result["mode"] = "trade-retail-census-acquire";
        result["collectedAtUtc"] = collectedAt;
        result["referenceRows"] = policy.ExpectedReferenceRows;
        result["leafHs6Codes"] = policy.ExpectedLeafHs6Count;
        result["legalHs6Codes"] = policy.ExpectedLegalHs6Count;
        result["statisticalSpecialCodes"] = policy.StatisticalSpecialCodes.Count;
        result["reviewedCodes"] = reviewed.Count;
        result["observedCandidateCodes"] = reviewed.Count(x => x.Value.RetailTriageStatus == "ProductCandidateObserved");
        result["restrictedCodes"] = reviewed.Count(x => x.Value.RetailTriageStatus == "RestrictedOrSensitive");
        result["pendingCodes"] = policy.ExpectedLegalHs6Count - reviewed.Keys.Count(x => !statisticalSpecialCodes.Contains(x));
        result["files"] = JsonDocument.Parse(receipt).RootElement.GetProperty("files").Clone();
    }

    public static async Task ImportAsync(string mode, string root, Dictionary<string, object?> result)
    {
        Require(mode is "self-test" or "preview" or "apply" or "verify", "TradeRetailCensusModeInvalid");
        var policyPath = Path.Combine(root, PolicyRelativePath);
        var policy = await ReadPolicyAsync(policyPath);
        var folder = Path.Combine(root, FolderRelativePath);
        var receiptPath = Path.Combine(folder, "acquisition.json");
        var referencePath = Path.Combine(folder, "un-comtrade-h6.json");
        var reviewSeedPath = Path.Combine(folder, "review-queue.seed.json");
        Require(File.Exists(receiptPath) && File.Exists(referencePath) && File.Exists(reviewSeedPath), "TradeRetailCensusAcquisitionMissing");
        using var receipt = JsonDocument.Parse(await File.ReadAllTextAsync(receiptPath));
        var receiptRoot = receipt.RootElement;
        Require(receiptRoot.GetProperty("schemaVersion").GetString() == "trade-retail-hs-census-acquisition.v1", "TradeRetailCensusReceiptChanged");
        Require(receiptRoot.GetProperty("policySha256").GetString() == Hash(policyPath), "TradeRetailCensusPolicyHashChanged");
        foreach (var file in receiptRoot.GetProperty("files").EnumerateArray())
        {
            var path = Path.Combine(folder, file.GetProperty("fileName").GetString()!);
            Require(File.Exists(path) && Hash(path) == file.GetProperty("sha256").GetString(), "TradeRetailCensusInputHashChanged");
        }

        var collectedAt = receiptRoot.GetProperty("collectedAtUtc").GetDateTimeOffset();
        var classificationRows = ParseClassificationRows(referencePath, policy, collectedAt);
        var reviewRows = ParseReviewRows(reviewSeedPath, policy, collectedAt);
        var allRows = classificationRows.Concat(reviewRows).ToArray();
        Require(allRows.Select(x => x.RecordKey).Distinct(StringComparer.Ordinal).Count() == allRows.Length, "TradeRetailCensusRecordKeyCollision");

        if (mode == "self-test")
        {
            Require(classificationRows.Count == policy.ExpectedLeafHs6Count && reviewRows.Count == policy.ExpectedLeafHs6Count, "TradeRetailCensusNormalizedCountChanged");
            Require(classificationRows.Count(x => x.QualityCode == "OfficialReference") == policy.ExpectedLegalHs6Count
                && classificationRows.Count(x => x.QualityCode == "StatisticalSpecialCode") == policy.StatisticalSpecialCodes.Count,
                "TradeRetailCensusClassificationBoundaryChanged");
            Require(reviewRows.Count(x => !policy.StatisticalSpecialCodes.Contains(x.DimensionKey["hs6=".Length..], StringComparer.Ordinal)) == policy.ExpectedLegalHs6Count
                && reviewRows.Count(x => policy.StatisticalSpecialCodes.Contains(x.DimensionKey["hs6=".Length..], StringComparer.Ordinal)
                    && x.QualityCode == "NotApplicable") == policy.StatisticalSpecialCodes.Count
                && reviewRows.All(x => x.LimitationCode.Contains("PrivateReviewOnly", StringComparison.Ordinal)),
                "TradeRetailCensusReviewBoundaryChanged");
            result["selfTestsPassed"] = 10;
            result["normalizedRows"] = allRows.Length;
            result["classificationRows"] = classificationRows.Count;
            result["reviewQueueRows"] = reviewRows.Count;
            result["legalHs6Codes"] = policy.ExpectedLegalHs6Count;
            result["statisticalSpecialCodes"] = policy.StatisticalSpecialCodes.Count;
            result["observedCandidateCodes"] = reviewRows.Count(x => x.TextValue.Contains("ProductCandidateObserved", StringComparison.Ordinal));
            result["restrictedCodes"] = reviewRows.Count(x => x.TextValue.Contains("RestrictedOrSensitive", StringComparison.Ordinal));
            result["pendingCodes"] = reviewRows.Count(x => x.TextValue.Contains("PendingRetailTriage", StringComparison.Ordinal));
            return;
        }

        var options = await 로컬공공자료Db.OptionsAsync(root);
        await using (var db = new PublicDataIngestionDbContext(options))
        {
            var existing = await LoadScopeAsync(db);
            var expectedByKey = allRows.ToDictionary(x => x.RecordKey, StringComparer.Ordinal);
            Require(existing.All(x => expectedByKey.ContainsKey(x.RecordKey)), "TradeRetailCensusExistingRecordOutsideExpectedScope");
            if (mode != "apply")
                Require(existing.All(x => expectedByKey.TryGetValue(x.RecordKey, out var expected) && Same(x, expected)), "TradeRetailCensusExistingRecordConflict");
            result["beforeCount"] = existing.Count;

            if (mode == "apply")
            {
                await db.Database.OpenConnectionAsync();
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT GET_LOCK('mirror:public-data:trade-retail-census',0)";
                Require(Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 1, "TradeRetailCensusImportBusy");
                await using var transaction = await db.Database.BeginTransactionAsync();
                result["databaseWriteAttempted"] = true;
                var registrar = new 평창군공공공간원본등록Service(db);
                var classificationSource = await registrar.RegisterFileAsync(referencePath, new 공공공간원본등록Request(
                    ReferenceSourceId, ClassificationDatasetId, policy.ClassificationVintage, ClassificationRevision,
                    ClassificationEvidenceAt, "application/json", FolderRelativePath + "/un-comtrade-h6.json"));
                var reviewSource = await registrar.RegisterFileAsync(reviewSeedPath, new 공공공간원본등록Request(
                    ReviewSourceId, ReviewDatasetId, policy.SchemaVersion, ReviewRevision,
                    policy.ObservedAt, "application/json", FolderRelativePath + "/review-queue.seed.json"));
                classificationRows.ForEach(x => x.RawSnapshotId = classificationSource.RawSnapshotId);
                reviewRows.ForEach(x => x.RawSnapshotId = reviewSource.RawSnapshotId);
                var classificationSaved = await SaveChunksAsync(db, classificationRows);
                var reviewSaved = await SaveChunksAsync(db, reviewRows);
                await UpdateRunAsync(db, classificationSource, policy.ExpectedReferenceRows, classificationRows.Count,
                    classificationSaved.Inserted, classificationSaved.Updated,
                    "Complete official H6 leaf classification census; not an individual product classification decision.");
                await UpdateRunAsync(db, reviewSource, reviewRows.Count, reviewRows.Count,
                    reviewSaved.Inserted, reviewSaved.Updated,
                    "Private retail review queue; product candidates may be absent, channel-inapplicable or pending human review.");
                await transaction.CommitAsync();
                result["committed"] = true;
                result["inserted"] = classificationSaved.Inserted + reviewSaved.Inserted;
                result["updated"] = classificationSaved.Updated + reviewSaved.Updated;
                result["existing"] = classificationSaved.Existing + reviewSaved.Existing;
            }
        }

        await using var verify = new PublicDataIngestionDbContext(options);
        var stored = await LoadScopeAsync(verify, includeRawSnapshot: true);
        var expected = allRows.ToDictionary(x => x.RecordKey, StringComparer.Ordinal);
        if (mode != "preview") Require(stored.Count == allRows.Length, "TradeRetailCensusReadbackCountMismatch");
        Require(stored.All(x => expected.TryGetValue(x.RecordKey, out var row) && Same(x, row) && (mode == "preview" || x.RawSnapshot != null)), "TradeRetailCensusReadbackMismatch");
        result["mode"] = mode;
        result["verifiedRows"] = stored.Count;
        result["classificationRows"] = stored.Count(x => x.DatasetId == ClassificationDatasetId);
        result["reviewQueueRows"] = stored.Count(x => x.DatasetId == ReviewDatasetId);
        result["legalHs6Codes"] = stored.Count(x => x.DatasetId == ClassificationDatasetId && x.QualityCode == "OfficialReference");
        result["statisticalSpecialCodes"] = stored.Count(x => x.DatasetId == ClassificationDatasetId && x.QualityCode == "StatisticalSpecialCode");
        result["observedCandidateCodes"] = stored.Count(x => x.DatasetId == ReviewDatasetId && x.TextValue.Contains("ProductCandidateObserved", StringComparison.Ordinal));
        result["restrictedCodes"] = stored.Count(x => x.DatasetId == ReviewDatasetId && x.TextValue.Contains("RestrictedOrSensitive", StringComparison.Ordinal));
        result["pendingCodes"] = stored.Count(x => x.DatasetId == ReviewDatasetId && x.TextValue.Contains("PendingRetailTriage", StringComparison.Ordinal));
        result["target"] = "hongdal-mysql-1 / hongdal_dev";
        result["sourceHashes"] = stored.Where(x => x.RawSnapshot != null).Select(x => x.RawSnapshot!.ContentHashSha256).Distinct().OrderBy(x => x).ToArray();
    }

    private static List<ReferenceRow> ParseReference(byte[] bytes, CensusPolicy policy)
    {
        using var document = JsonDocument.Parse(bytes);
        var rows = document.RootElement.GetProperty("results").EnumerateArray().ToArray();
        Require(rows.Length == policy.ExpectedReferenceRows, "TradeRetailCensusReferenceCountChanged");
        var leaves = rows.Where(x => x.GetProperty("aggrlevel").GetInt32() == 6
                && x.GetProperty("isLeaf").GetString() == "1"
                && IsDigits(x.GetProperty("id").GetString(), 6))
            .Select(x => new ReferenceRow(
                x.GetProperty("id").GetString()!,
                x.GetProperty("text").GetString()!,
                x.GetProperty("parent").GetString()!,
                x.GetProperty("standardUnitAbbr").GetString()!))
            .OrderBy(x => x.Id, StringComparer.Ordinal)
            .ToList();
        Require(leaves.Count == policy.ExpectedLeafHs6Count && leaves.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() == leaves.Count, "TradeRetailCensusLeafCountChanged");
        Require(leaves.All(x => x.Parent == x.Id[..4] && x.Text.StartsWith(x.Id + " - ", StringComparison.Ordinal)), "TradeRetailCensusHierarchyChanged");
        var specialCodes = leaves.Where(x => policy.StatisticalSpecialCodes.Contains(x.Id, StringComparer.Ordinal)).Select(x => x.Id).ToArray();
        Require(specialCodes.Length == policy.StatisticalSpecialCodes.Count
            && specialCodes.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(policy.StatisticalSpecialCodes.OrderBy(x => x, StringComparer.Ordinal), StringComparer.Ordinal)
            && leaves.Count - specialCodes.Length == policy.ExpectedLegalHs6Count,
            "TradeRetailCensusLegalAndSpecialCodeBoundaryChanged");
        return leaves;
    }

    private static List<외부데이터정규화Record> ParseClassificationRows(string path, CensusPolicy policy, DateTimeOffset collectedAt)
    {
        var leaves = ParseReference(File.ReadAllBytes(path), policy);
        return leaves.Select(row =>
        {
            var dimension = $"hs6={row.Id}|parentHs4={row.Parent}|chapter={row.Id[..2]}";
            var description = row.Text[(row.Id.Length + 3)..];
            var isStatisticalSpecial = policy.StatisticalSpecialCodes.Contains(row.Id, StringComparer.Ordinal);
            return NewRecord(ReferenceSourceId, ClassificationDatasetId, $"classification:hs2022:h6:{row.Id}",
                "classification.hs6.description", description, row.StandardUnit, ClassificationEvidenceAt, collectedAt,
                isStatisticalSpecial ? "StatisticalSpecialCode" : "OfficialReference",
                isStatisticalSpecial ? "UnComtradeStatisticalSpecialCode;NotLegalHs6Commodity" : "NotAnIndividualProductClassification",
                dimension, policy.ClassificationVintage, ClassificationRevision);
        }).ToList();
    }

    private static List<외부데이터정규화Record> ParseReviewRows(string path, CensusPolicy policy, DateTimeOffset collectedAt)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        Require(root.GetProperty("schemaVersion").GetString() == "trade-retail-hs-census-review-seed.v1", "TradeRetailCensusReviewSeedChanged");
        var codes = root.GetProperty("codes").EnumerateArray().ToArray();
        Require(codes.Length == policy.ExpectedLeafHs6Count, "TradeRetailCensusReviewSeedCountChanged");
        return codes.Select(item =>
        {
            var hs6 = item.GetProperty("hs6").GetString()!;
            Require(IsDigits(hs6, 6), "TradeRetailCensusReviewHs6Invalid");
            var isStatisticalSpecial = policy.StatisticalSpecialCodes.Contains(hs6, StringComparer.Ordinal);
            var triageStatus = item.GetProperty("retailTriageStatus").GetString()!;
            var searchStatus = item.GetProperty("searchStatus").GetString()!;
            var representativeProductStatus = item.GetProperty("representativeProductStatus").GetString()!;
            var candidateCount = item.GetProperty("candidateCount").GetInt32();
            Require(policy.RetailTriageStatusCodes.Contains(triageStatus, StringComparer.Ordinal)
                && policy.SearchStatusCodes.Contains(searchStatus, StringComparer.Ordinal)
                && candidateCount is >= 0 and <= 3
                && (triageStatus == "ProductCandidateObserved" ? candidateCount > 0 : candidateCount == 0),
                "TradeRetailCensusReviewStateInvalid");
            var text = JsonSerializer.Serialize(new
            {
                retailTriageStatus = triageStatus,
                searchStatus,
                representativeProductStatus,
                candidateCount,
                batchIds = item.GetProperty("batchIds").EnumerateArray().Select(x => x.GetString()).ToArray(),
                distributionApproved = false,
            });
            var quality = isStatisticalSpecial ? "NotApplicable" : triageStatus switch
            {
                "RestrictedOrSensitive" => "ReviewedRestricted",
                "ChannelInapplicable" => "NotApplicable",
                "SearchNoCandidate" => "ReviewedNoCandidate",
                _ => "PendingHumanReview",
            };
            var limitation = isStatisticalSpecial
                ? "PrivateReviewOnly;UNComtradeStatisticalSpecialCode;RetailObservationNotApplicable"
                : triageStatus == "RestrictedOrSensitive"
                    ? "PrivateReviewOnly;OfficialMarketplacePolicyRestriction;NoProductOrPopularityClaim"
                    : "PrivateReviewOnly;NoProductOrPopularityClaim";
            return NewRecord(ReviewSourceId, ReviewDatasetId, $"review:trade-retail:hs2022:h6:{hs6}",
                "trade.retail.census.review-status", text, "record", policy.ObservedAt, collectedAt,
                quality,
                limitation,
                $"hs6={hs6}", policy.SchemaVersion, ReviewRevision);
        }).OrderBy(x => x.DimensionKey, StringComparer.Ordinal).ToList();
    }

    private static 외부데이터정규화Record NewRecord(string sourceId, string datasetId, string stableId,
        string metricCode, string textValue, string unitCode, DateTimeOffset evidenceAt, DateTimeOffset collectedAt,
        string qualityCode, string limitationCode, string dimensionKey, string sourceVersion, string dataRevision)
        => new()
        {
            RecordKey = 외부데이터RecordKey.Create(sourceId, datasetId, "country:global", metricCode, evidenceAt, dimensionKey),
            StableId = stableId,
            SourceId = sourceId,
            DatasetId = datasetId,
            RegionStableId = "country:global",
            MetricCode = metricCode,
            TextValue = textValue,
            UnitCode = unitCode,
            EvidenceAsOfUtc = evidenceAt,
            CollectedAtUtc = collectedAt,
            SpatialPrecisionCode = "global-classification",
            TemporalPrecisionCode = "classification-vintage",
            QualityCode = qualityCode,
            LimitationCode = limitationCode,
            DimensionKey = dimensionKey,
            SourceVersion = sourceVersion,
            DataRevision = dataRevision,
            FirstSeenAtUtc = collectedAt,
            LastSeenAtUtc = collectedAt,
        };

    private static async Task<Dictionary<string, RetailReviewState>> ReadObservedBatchReviewsAsync(string root, HashSet<string> validCodes)
    {
        var result = new Dictionary<string, RetailReviewState>(StringComparer.Ordinal);
        var folder = Path.Combine(root, "eng", "public-data", "trade-retail", "batches");
        if (!Directory.Exists(folder)) return result;
        foreach (var manifestPath in Directory.EnumerateFiles(folder, "*.manifest.json").OrderBy(x => x, StringComparer.Ordinal))
        {
            using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
            var rootElement = manifest.RootElement;
            var manifestSchema = rootElement.GetProperty("schemaVersion").GetString();
            if (manifestSchema is not ("trade-retail-batch.v1" or "trade-retail-batch.v2")) continue;
            var batchId = rootElement.GetProperty("batchId").GetString()!;
            var observedAt = rootElement.GetProperty("observedAt").GetDateTimeOffset();
            var productRelativePath = rootElement.GetProperty("productObservationRelativePath").GetString()!;
            var productPath = Path.Combine(root, productRelativePath);
            if (!File.Exists(productPath)) continue;
            using var products = JsonDocument.Parse(await File.ReadAllTextAsync(productPath));
            var productRoot = products.RootElement;
            var productSchema = productRoot.GetProperty("schemaVersion").GetString();
            if (productSchema == "representative-product-observation.v2")
            {
                foreach (var group in productRoot.GetProperty("items").EnumerateArray()
                             .GroupBy(x => x.GetProperty("hs6Candidate").GetString()!, StringComparer.Ordinal))
                {
                    if (!validCodes.Contains(group.Key)) continue;
                    UpsertReview(result, group.Key, batchId, observedAt, "ProductCandidateObserved",
                        "IndexedCandidateObserved", "PendingHumanReview", group.Count());
                }
                continue;
            }

            if (productSchema != "representative-product-observation.v3") continue;
            foreach (var review in productRoot.GetProperty("codeReviews").EnumerateArray())
            {
                var hs6 = review.GetProperty("hs6").GetString()!;
                if (!validCodes.Contains(hs6)) continue;
                UpsertReview(result, hs6, batchId, observedAt,
                    review.GetProperty("retailTriageStatus").GetString()!,
                    review.GetProperty("searchStatus").GetString()!,
                    review.GetProperty("representativeProductStatus").GetString()!,
                    review.GetProperty("candidateCount").GetInt32());
            }
        }
        return result;
    }

    private static void UpsertReview(Dictionary<string, RetailReviewState> reviews, string hs6, string batchId,
        DateTimeOffset observedAt, string triageStatus, string searchStatus, string productStatus, int candidateCount)
    {
        if (!reviews.TryGetValue(hs6, out var current))
        {
            current = new RetailReviewState();
            reviews[hs6] = current;
        }
        current.BatchIds.Add(batchId);
        if (current.SelectedBatchId.Length > 0
            && (observedAt < current.ObservedAt
                || observedAt == current.ObservedAt && string.CompareOrdinal(batchId, current.SelectedBatchId) <= 0)) return;
        current.SelectedBatchId = batchId;
        current.ObservedAt = observedAt;
        current.RetailTriageStatus = triageStatus;
        current.SearchStatus = searchStatus;
        current.RepresentativeProductStatus = productStatus;
        current.CandidateCount = candidateCount;
    }

    private static async Task<CensusPolicy> ReadPolicyAsync(string path)
    {
        Require(File.Exists(path), "TradeRetailCensusPolicyMissing");
        var policy = JsonSerializer.Deserialize<CensusPolicy>(await File.ReadAllTextAsync(path), JsonOptions)
            ?? throw new InvalidDataException("TradeRetailCensusPolicyInvalid");
        Require(policy.SchemaVersion == "trade-retail-hs-census-policy.v1"
            && policy.ClassificationCode == "H6" && policy.ExpectedReferenceRows > 0
            && policy.ExpectedLeafHs6Count > 0 && policy.ExpectedLegalHs6Count > 0
            && policy.ExpectedLegalHs6Count + policy.StatisticalSpecialCodes.Count == policy.ExpectedLeafHs6Count
            && policy.StatisticalSpecialCodes.Count > 0
            && policy.RepresentativeProductTarget.Minimum == 0 && policy.RepresentativeProductTarget.Maximum == 3
            && new[] { "PendingRetailTriage", "ProductCandidateObserved", "SearchNoCandidate", "ChannelInapplicable", "RestrictedOrSensitive" }
                .All(x => policy.RetailTriageStatusCodes.Contains(x, StringComparer.Ordinal))
            && new[] { "NotAttempted", "IndexedCandidateObserved", "NoIndexedCandidateObserved", "NotApplicable", "PolicyReviewedRestricted" }
                .All(x => policy.SearchStatusCodes.Contains(x, StringComparer.Ordinal))
            && !policy.DistributionApproved, "TradeRetailCensusPolicyChanged");
        return policy;
    }

    private static async Task<List<외부데이터정규화Record>> LoadScopeAsync(PublicDataIngestionDbContext db, bool includeRawSnapshot = false)
    {
        var query = db.NormalizedRecords.AsNoTracking().Where(x =>
            (x.SourceId == ReferenceSourceId && x.DatasetId == ClassificationDatasetId)
            || (x.SourceId == ReviewSourceId && x.DatasetId == ReviewDatasetId));
        if (includeRawSnapshot) query = query.Include(x => x.RawSnapshot);
        return await query.ToListAsync();
    }

    private static async Task<(int Inserted, int Updated, int Existing)> SaveChunksAsync(PublicDataIngestionDbContext db, List<외부데이터정규화Record> rows)
    {
        var inserted = 0; var updated = 0; var existing = 0;
        var store = new EfExternalDataIngestionStore(db);
        for (var start = 0; start < rows.Count; start += 400)
        {
            var saved = await store.UpsertNormalizedAsync(rows.Skip(start).Take(400).ToArray());
            inserted += saved.InsertedCount; updated += saved.UpdatedCount; existing += saved.ExistingCount;
        }
        return (inserted, updated, existing);
    }

    private static async Task UpdateRunAsync(PublicDataIngestionDbContext db, 공공공간원본등록Result source,
        int fetched, int normalized, int inserted, int updated, string summary)
    {
        if (!source.Inserted) return;
        var snapshot = await db.RawSnapshots.SingleAsync(x => x.Id == source.RawSnapshotId);
        var run = await db.IngestionRuns.SingleAsync(x => x.Id == snapshot.FirstCollectionRunId);
        run.StatusCode = 외부데이터수집StatusCodes.Partial;
        run.FetchedCount = fetched;
        run.NormalizedCount = normalized;
        run.InsertedCount = inserted;
        run.UpdatedCount = updated;
        run.ErrorCode = "PrivateReviewPending";
        run.ErrorSummary = summary;
        await db.SaveChangesAsync();
    }

    private static object ReceiptEntry(string fileName, string path)
    {
        var info = new FileInfo(path);
        return new { fileName, contentLength = info.Length, sha256 = Hash(path) };
    }

    private static bool Same(외부데이터정규화Record x, 외부데이터정규화Record y)
        => x.RecordKey == y.RecordKey && x.StableId == y.StableId && x.SourceId == y.SourceId && x.DatasetId == y.DatasetId
           && x.RegionStableId == y.RegionStableId && x.MetricCode == y.MetricCode && x.NumericValue == y.NumericValue
           && x.TextValue == y.TextValue && x.UnitCode == y.UnitCode && x.EvidenceAsOfUtc == y.EvidenceAsOfUtc
           && x.SpatialPrecisionCode == y.SpatialPrecisionCode && x.TemporalPrecisionCode == y.TemporalPrecisionCode
           && x.QualityCode == y.QualityCode && x.LimitationCode == y.LimitationCode && x.DimensionKey == y.DimensionKey
           && x.SourceVersion == y.SourceVersion && x.DataRevision == y.DataRevision;

    private static bool IsDigits(string? value, int length) => value?.Length == length && value.All(char.IsAsciiDigit);
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidDataException(code); }

    private sealed record ReferenceRow(string Id, string Text, string Parent, string StandardUnit);

    private sealed class RetailReviewState
    {
        public string SelectedBatchId { get; set; } = string.Empty;
        public DateTimeOffset ObservedAt { get; set; }
        public string RetailTriageStatus { get; set; } = string.Empty;
        public string SearchStatus { get; set; } = string.Empty;
        public string RepresentativeProductStatus { get; set; } = string.Empty;
        public int CandidateCount { get; set; }
        public HashSet<string> BatchIds { get; } = new(StringComparer.Ordinal);
    }

    private sealed class RepresentativeProductTarget
    {
        public int Minimum { get; init; }
        public int Maximum { get; init; }
    }

    private sealed class CensusPolicy
    {
        public string SchemaVersion { get; init; } = string.Empty;
        public string ClassificationCode { get; init; } = string.Empty;
        public string ClassificationVintage { get; init; } = string.Empty;
        public int ExpectedReferenceRows { get; init; }
        public int ExpectedLeafHs6Count { get; init; }
        public int ExpectedLegalHs6Count { get; init; }
        public List<string> StatisticalSpecialCodes { get; init; } = [];
        public DateTimeOffset ObservedAt { get; init; }
        public RepresentativeProductTarget RepresentativeProductTarget { get; init; } = new();
        public List<string> RetailTriageStatusCodes { get; init; } = [];
        public List<string> SearchStatusCodes { get; init; } = [];
        public bool DistributionApproved { get; init; }
    }
}
