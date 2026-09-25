using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

internal static class 무역소매Batch
{
    private const string TradeSourceId = "un-comtrade-public";
    private const string ReferenceSourceId = "un-comtrade-reference";
    private const string CategorySourceId = "manual-hs-retail-category-crosswalk";
    private const string ProductSourceId = "manual-marketplace-observation";
    private const string HsUrl = "https://comtradeapi.un.org/files/v1/app/reference/H6.json";
    private const string PartnerUrl = "https://comtradeapi.un.org/files/v1/app/reference/partnerAreas.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    private static readonly DateTimeOffset HsEvidenceAt = new(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static async Task AcquireAsync(string root, string batchId, Dictionary<string, object?> result)
    {
        var manifestPath = ManifestPath(root, batchId);
        var manifest = await ReadManifestAsync(manifestPath, batchId);
        var productPath = Path.Combine(root, manifest.ProductObservationRelativePath);
        Require(File.Exists(productPath), "TradeRetailBatchProductObservationMissing");

        var folderRelative = FolderRelative(batchId);
        var folder = Path.Combine(root, folderRelative);
        Directory.CreateDirectory(folder);
        var codes = manifest.HsEntries.Select(x => x.Hs6).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var tradeUrl = "https://comtradeapi.un.org/public/v1/preview/C/A/HS"
            + $"?period={manifest.Period}&reporterCode={manifest.ReporterCode}"
            + $"&cmdCode={Uri.EscapeDataString(string.Join(',', codes))}&flowCode={manifest.FlowCode}"
            + "&partner2Code=0&customsCode=C00&motCode=0&maxRecords=500&typeCode=C&freqCode=A&clCode=H6";

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Ssalddel-PublicDataResearch/1.0");
        var collectedAt = DateTimeOffset.UtcNow;
        var tradeBytes = await http.GetByteArrayAsync(tradeUrl);
        var h6Bytes = await http.GetByteArrayAsync(HsUrl);
        var partnerBytes = await http.GetByteArrayAsync(PartnerUrl);
        var partnerCodes = SelectPartnerCodes(tradeBytes, codes);

        var tradePath = Path.Combine(folder, "un-comtrade-trade.json");
        var hsPath = Path.Combine(folder, "un-comtrade-h6-selected.json");
        var partnerPath = Path.Combine(folder, "un-comtrade-partners-selected.json");
        await File.WriteAllBytesAsync(tradePath, tradeBytes);
        await File.WriteAllBytesAsync(hsPath, SelectReferenceRows(h6Bytes, codes, HsUrl));
        await File.WriteAllBytesAsync(partnerPath, SelectReferenceRows(partnerBytes, partnerCodes, PartnerUrl));

        var files = new[]
        {
            ReceiptEntry("un-comtrade-trade.json", tradeUrl, tradePath, collectedAt),
            ReceiptEntry("un-comtrade-h6-selected.json", HsUrl, hsPath, collectedAt),
            ReceiptEntry("un-comtrade-partners-selected.json", PartnerUrl, partnerPath, collectedAt),
        };
        var receipt = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = "trade-retail-batch-acquisition.v1",
            batchId = manifest.BatchId,
            collectedAtUtc = collectedAt,
            manifestRelativePath = Path.GetRelativePath(root, manifestPath).Replace('\\', '/'),
            manifestSha256 = Hash(manifestPath),
            productObservationRelativePath = manifest.ProductObservationRelativePath.Replace('\\', '/'),
            productObservationSha256 = Hash(productPath),
            scope = $"Korea annual imports {manifest.Period}, H6 {string.Join('/', codes)} and reviewed Coupang retail outcomes",
            accessMethod = manifest.SchemaVersion == "trade-retail-batch.v1"
                ? "UN Comtrade public API plus human-reviewed indexed Coupang detail observations"
                : "UN Comtrade public API plus official marketplace policy review and optional indexed detail observations",
            limitations = manifest.Limitations,
            files,
        }, JsonOptions);
        await File.WriteAllBytesAsync(Path.Combine(folder, "acquisition.json"), receipt);

        result["mode"] = "trade-retail-batch-acquire";
        result["batchId"] = batchId;
        result["collectedAtUtc"] = collectedAt;
        result["sourceRows"] = JsonDocument.Parse(tradeBytes).RootElement.GetProperty("data").GetArrayLength();
        result["files"] = files;
    }

    public static async Task ImportAsync(string mode, string root, string batchId, Dictionary<string, object?> result)
    {
        Require(mode is "self-test" or "preview" or "apply" or "verify", "TradeRetailBatchModeInvalid");
        var manifestPath = ManifestPath(root, batchId);
        var manifest = await ReadManifestAsync(manifestPath, batchId);
        var folderRelative = FolderRelative(batchId);
        var folder = Path.Combine(root, folderRelative);
        var receiptPath = Path.Combine(folder, "acquisition.json");
        Require(File.Exists(receiptPath), "TradeRetailBatchAcquisitionMissing");
        using var receipt = JsonDocument.Parse(await File.ReadAllTextAsync(receiptPath));
        Require(receipt.RootElement.GetProperty("schemaVersion").GetString() == "trade-retail-batch-acquisition.v1"
            && receipt.RootElement.GetProperty("batchId").GetString() == batchId, "TradeRetailBatchReceiptChanged");
        Require(Hash(manifestPath) == receipt.RootElement.GetProperty("manifestSha256").GetString(), "TradeRetailBatchManifestHashChanged");
        var productPath = Path.Combine(root, manifest.ProductObservationRelativePath);
        Require(File.Exists(productPath) && Hash(productPath) == receipt.RootElement.GetProperty("productObservationSha256").GetString(), "TradeRetailBatchProductHashChanged");
        foreach (var file in receipt.RootElement.GetProperty("files").EnumerateArray())
        {
            var path = Path.Combine(folder, file.GetProperty("fileName").GetString()!);
            Require(File.Exists(path) && Hash(path) == file.GetProperty("sha256").GetString(), "TradeRetailBatchInputHashChanged");
        }

        var collectedAt = receipt.RootElement.GetProperty("collectedAtUtc").GetDateTimeOffset();
        var tradePath = Path.Combine(folder, "un-comtrade-trade.json");
        var hsPath = Path.Combine(folder, "un-comtrade-h6-selected.json");
        var partnerPath = Path.Combine(folder, "un-comtrade-partners-selected.json");
        var hs = ParseReference(hsPath).ToDictionary(x => x.Id, x => x.Text, StringComparer.Ordinal);
        var partners = ParseReference(partnerPath).ToDictionary(x => int.Parse(x.Id, CultureInfo.InvariantCulture), x => x.Text);
        var tradeRows = ParseTrade(tradePath, manifest, collectedAt, hs, partners);
        var classificationRows = ParseClassifications(hsPath, manifest, collectedAt);
        var categoryRows = ParseCategoryBindings(manifest, collectedAt);
        var productDocument = ParseProductDocument(productPath, manifest);
        var productRows = productDocument.ObservationRows;
        var productReviewRows = productDocument.ReviewOutcomeRows;
        var productDataRows = productRows.Concat(productReviewRows).ToList();
        var allRows = tradeRows.Concat(classificationRows).Concat(categoryRows).Concat(productDataRows).ToList();
        var codeCount = manifest.HsEntries.Count;
        Require(classificationRows.Count == codeCount && categoryRows.Count == codeCount,
            "TradeRetailBatchNormalizedCountChanged");
        if (manifest.SchemaVersion == "trade-retail-batch.v1")
        {
            Require(tradeRows.Count == codeCount * 12 && productRows.Count == codeCount && productReviewRows.Count == 0,
                "TradeRetailBatchNormalizedCountChanged");
        }
        else
        {
            Require(tradeRows.Count(x => x.MetricCode == "trade.import.observation-status") == codeCount
                && productReviewRows.Count == codeCount
                && productRows.Count == manifest.HsEntries.Sum(x => x.ExpectedProductObservationCount),
                "TradeRetailBatchNormalizedCountChanged");
        }
        Require(allRows.Select(x => x.RecordKey).Distinct(StringComparer.Ordinal).Count() == allRows.Count, "TradeRetailBatchRecordKeyCollision");

        if (mode == "self-test")
        {
            if (manifest.SchemaVersion == "trade-retail-batch.v1")
            {
                Require(tradeRows.Count(x => x.MetricCode == "trade.import.value.usd") == codeCount * 6, "TradeRetailBatchValueCountChanged");
                Require(tradeRows.Count(x => x.MetricCode == "trade.import.net-weight.kg") == codeCount * 6, "TradeRetailBatchWeightCountChanged");
                Require(categoryRows.All(x => x.QualityCode == "CandidateCrosswalk"), "TradeRetailBatchCategoryBoundaryChanged");
                Require(productRows.All(x => x.LimitationCode.Contains("PendingHsBinding", StringComparison.Ordinal)), "TradeRetailBatchProductBoundaryChanged");
            }
            else
            {
                Require(productReviewRows.All(x => x.LimitationCode.Contains("PrivateReview", StringComparison.Ordinal)),
                    "TradeRetailBatchProductReviewBoundaryChanged");
            }
            result["selfTestsPassed"] = 12;
            result["normalizedRows"] = allRows.Count;
            result["tradeRows"] = tradeRows.Count;
            result["classificationRows"] = classificationRows.Count;
            result["categoryBindingRows"] = categoryRows.Count;
            result["productRows"] = productRows.Count;
            result["productReviewOutcomeRows"] = productReviewRows.Count;
            return;
        }

        var options = await 로컬공공자료Db.OptionsAsync(root);
        var keys = allRows.Select(x => x.RecordKey).ToList();
        await using (var db = new PublicDataIngestionDbContext(options))
        {
            var existing = await db.NormalizedRecords.AsNoTracking().Where(x => keys.Contains(x.RecordKey)).ToListAsync();
            Require(existing.All(x => allRows.Any(y => Same(x, y))), "TradeRetailBatchExistingRecordConflict");
            result["beforeCount"] = existing.Count;
            if (mode == "apply")
            {
                await db.Database.OpenConnectionAsync();
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = $"SELECT GET_LOCK('{ImportLockName(batchId)}',0)";
                Require(Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 1, "TradeRetailBatchImportBusy");
                await using var transaction = await db.Database.BeginTransactionAsync();
                result["databaseWriteAttempted"] = true;
                var registrar = new 평창군공공공간원본등록Service(db);
                var tradeDatasetId = TradeDatasetId(manifest);
                var hsDatasetId = HsDatasetId(batchId);
                var partnerDatasetId = PartnerDatasetId(batchId);
                var categoryDatasetId = CategoryDatasetId(batchId);
                var productDatasetId = ProductDatasetId(manifest);
                var tradeEvidenceAt = TradeEvidenceAt(manifest);
                var tradeSource = await registrar.RegisterFileAsync(tradePath, new 공공공간원본등록Request(
                    TradeSourceId, tradeDatasetId, $"UN Comtrade H6 annual {manifest.Period}", $"{batchId}-trade-r1",
                    tradeEvidenceAt, "application/json", folderRelative + "/un-comtrade-trade.json"));
                var hsSource = await registrar.RegisterFileAsync(hsPath, new 공공공간원본등록Request(
                    ReferenceSourceId, hsDatasetId, "H6 (HS 2022)", $"{batchId}-hs-r1",
                    HsEvidenceAt, "application/json", folderRelative + "/un-comtrade-h6-selected.json"));
                var partnerSource = await registrar.RegisterFileAsync(partnerPath, new 공공공간원본등록Request(
                    ReferenceSourceId, partnerDatasetId, "UN M49 partner areas", $"{batchId}-partners-r1",
                    tradeEvidenceAt, "application/json", folderRelative + "/un-comtrade-partners-selected.json"));
                var categorySource = await registrar.RegisterFileAsync(manifestPath, new 공공공간원본등록Request(
                    CategorySourceId, categoryDatasetId, manifest.ObservedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), $"{batchId}-category-r1",
                    manifest.ObservedAt, "application/json", Path.GetRelativePath(root, manifestPath).Replace('\\', '/')));
                var productSource = await registrar.RegisterFileAsync(productPath, new 공공공간원본등록Request(
                    ProductSourceId, productDatasetId, manifest.ObservedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), $"{batchId}-product-r1",
                    manifest.ObservedAt, "application/json", manifest.ProductObservationRelativePath.Replace('\\', '/')));

                tradeRows.ForEach(x => x.RawSnapshotId = tradeSource.RawSnapshotId);
                classificationRows.ForEach(x => x.RawSnapshotId = hsSource.RawSnapshotId);
                categoryRows.ForEach(x => x.RawSnapshotId = categorySource.RawSnapshotId);
                productDataRows.ForEach(x => x.RawSnapshotId = productSource.RawSnapshotId);
                var store = new EfExternalDataIngestionStore(db);
                var tradeSaved = await store.UpsertNormalizedAsync(tradeRows);
                var hsSaved = await store.UpsertNormalizedAsync(classificationRows);
                var categorySaved = await store.UpsertNormalizedAsync(categoryRows);
                var productSaved = await store.UpsertNormalizedAsync(productDataRows);
                Require(tradeSaved.UpdatedCount + hsSaved.UpdatedCount + categorySaved.UpdatedCount + productSaved.UpdatedCount == 0, "TradeRetailBatchUnexpectedUpdate");
                await UpdateRunAsync(db, tradeSource, manifest.ExpectedSourceRows, tradeRows.Count, tradeSaved.InsertedCount, "World totals and top five partners; public preview aggregate; may be revised; not product provenance.");
                await UpdateRunAsync(db, hsSource, codeCount, classificationRows.Count, hsSaved.InsertedCount, "Selected H6 categories only; not an individual product classification decision.");
                await UpdateRunAsync(db, partnerSource, partners.Count, 0, 0, "Selected M49 partner labels used only to decode stored partner codes.");
                await UpdateRunAsync(db, categorySource, codeCount, categoryRows.Count, categorySaved.InsertedCount, "HS/HKS/internal retail/Coupang leaf category candidates; seller display category code is not verified.");
                await UpdateRunAsync(db, productSource, productDataRows.Count, productDataRows.Count, productSaved.InsertedCount,
                    "Marketplace review outcomes and optional indexed observations; private review; no popularity, live inventory, origin, publication or affiliate claim.");
                await transaction.CommitAsync();
                result["committed"] = true;
                result["inserted"] = tradeSaved.InsertedCount + hsSaved.InsertedCount + categorySaved.InsertedCount + productSaved.InsertedCount;
                result["existing"] = tradeSaved.ExistingCount + hsSaved.ExistingCount + categorySaved.ExistingCount + productSaved.ExistingCount;
            }
        }

        await using var verify = new PublicDataIngestionDbContext(options);
        var stored = await verify.NormalizedRecords.AsNoTracking().Where(x => keys.Contains(x.RecordKey)).Include(x => x.RawSnapshot).ToListAsync();
        var partnerHash = Hash(partnerPath);
        var partnerReference = await verify.RawSnapshots.AsNoTracking().SingleOrDefaultAsync(x =>
            x.SourceId == ReferenceSourceId && x.DatasetId == PartnerDatasetId(batchId) && x.ContentHashSha256 == partnerHash);
        if (mode != "preview") Require(stored.Count == allRows.Count, "TradeRetailBatchReadbackCountMismatch");
        Require(stored.All(x => allRows.Any(y => Same(x, y)) && x.RawSnapshot != null)
            && (mode == "preview" || partnerReference != null), "TradeRetailBatchReadbackMismatch");
        result["mode"] = mode;
        result["batchId"] = batchId;
        result["verifiedRows"] = stored.Count;
        result["tradeRows"] = stored.Count(x => x.SourceId == TradeSourceId);
        result["classificationRows"] = stored.Count(x => x.DatasetId == HsDatasetId(batchId));
        result["categoryBindingRows"] = stored.Count(x => x.SourceId == CategorySourceId);
        result["productRows"] = stored.Count(x => x.SourceId == ProductSourceId && x.DatasetId == ProductDatasetId(manifest)
            && x.MetricCode == "retail.product.observation");
        result["productReviewOutcomeRows"] = stored.Count(x => x.SourceId == ProductSourceId && x.DatasetId == ProductDatasetId(manifest)
            && x.MetricCode == "retail.product.review-outcome");
        result["target"] = "hongdal-mysql-1 / hongdal_dev";
        result["sourceHashes"] = stored.Select(x => x.RawSnapshot!.ContentHashSha256).Append(partnerHash).Distinct().OrderBy(x => x).ToArray();
    }

    private static List<외부데이터정규화Record> ParseTrade(string path, BatchManifest manifest, DateTimeOffset collectedAt,
        IReadOnlyDictionary<string, string> hs, IReadOnlyDictionary<int, string> partners)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        Require(root.GetProperty("count").GetInt32() == manifest.ExpectedSourceRows
            && root.GetProperty("data").GetArrayLength() == manifest.ExpectedSourceRows, "TradeRetailBatchSourceCountChanged");
        var sourceRows = root.GetProperty("data").EnumerateArray().Select(x => new TradeRow(
            x.GetProperty("cmdCode").GetString()!, x.GetProperty("partnerCode").GetInt32(),
            x.GetProperty("primaryValue").GetDecimal(), ReadNullableDecimal(x.GetProperty("netWgt")),
            x.GetProperty("period").GetString()!, x.GetProperty("reporterCode").GetInt32(),
            x.GetProperty("flowCode").GetString()!, x.GetProperty("classificationCode").GetString()!,
            x.GetProperty("isReported").GetBoolean(), x.GetProperty("isAggregate").GetBoolean())).ToList();
        var allowedCodes = manifest.HsEntries.Select(x => x.Hs6).ToHashSet(StringComparer.Ordinal);
        Require(sourceRows.All(x => x.Period == manifest.Period && x.ReporterCode == manifest.ReporterCode
            && x.FlowCode == manifest.FlowCode && x.Classification == manifest.ClassificationCode
            && allowedCodes.Contains(x.HsCode)
            && (x.PartnerCode == 0 ? x.IsAggregate && !x.IsReported : !x.IsAggregate && x.IsReported))
            && sourceRows.Select(x => $"{x.HsCode}:{x.PartnerCode}").Distinct(StringComparer.Ordinal).Count() == sourceRows.Count,
            "TradeRetailBatchScopeChanged");
        foreach (var entry in manifest.HsEntries)
        {
            var codeRows = sourceRows.Where(x => x.HsCode == entry.Hs6).ToArray();
            if (manifest.SchemaVersion == "trade-retail-batch.v1" || entry.TradeObservationStatus == "Observed")
            {
                var total = codeRows.Single(x => x.PartnerCode == 0);
                Require(entry.ExpectedWorldValueUsd.HasValue
                    && total.PrimaryValue == entry.ExpectedWorldValueUsd.Value
                    && total.NetWeight == entry.ExpectedWorldNetWeightKg,
                    "TradeRetailBatchFrozenTotalChanged");
            }
            else
            {
                Require(entry.TradeObservationStatus == "NoReportedRows" && codeRows.Length == 0
                    && entry.ExpectedWorldValueUsd is null && entry.ExpectedWorldNetWeightKg is null,
                    "TradeRetailBatchFrozenTotalChanged");
            }
        }
        var selected = sourceRows.GroupBy(x => x.HsCode, StringComparer.Ordinal)
            .SelectMany(group => group.Where(x => x.PartnerCode == 0)
                .Concat(group.Where(x => x.PartnerCode != 0).OrderByDescending(x => x.PrimaryValue).Take(5)))
            .OrderBy(x => x.HsCode, StringComparer.Ordinal).ThenBy(x => x.PartnerCode).ToList();
        Require((manifest.SchemaVersion != "trade-retail-batch.v1" || selected.Count == manifest.HsEntries.Count * 6)
            && selected.All(x => hs.ContainsKey(x.HsCode) && partners.ContainsKey(x.PartnerCode)), "TradeRetailBatchSelectionChanged");
        var rows = new List<외부데이터정규화Record>();
        foreach (var item in selected)
        {
            var partnerName = partners[item.PartnerCode];
            var dimension = $"batch={manifest.BatchId};hs6={item.HsCode};partnerM49={item.PartnerCode};partner={partnerName};flow=import;period={manifest.Period};classification=H6";
            rows.Add(CreateNumeric(item, manifest, "trade.import.value.usd", item.PrimaryValue, "USD", dimension, collectedAt, partnerName));
            rows.Add(CreateNumeric(item, manifest, "trade.import.net-weight.kg", item.NetWeight, "kg", dimension, collectedAt, partnerName));
        }
        if (manifest.SchemaVersion == "trade-retail-batch.v2")
        {
            rows.AddRange(manifest.HsEntries.Select(entry => CreateTradeAvailability(entry, manifest, collectedAt)));
        }
        return rows;
    }

    private static 외부데이터정규화Record CreateNumeric(TradeRow source, BatchManifest manifest, string metric,
        decimal? value, string unit, string dimension, DateTimeOffset collectedAt, string partnerName)
    {
        var evidenceAt = TradeEvidenceAt(manifest);
        var measureUnavailable = value is null;
        return new 외부데이터정규화Record
        {
            RecordKey = 외부데이터RecordKey.Create(TradeSourceId, TradeDatasetId(manifest), "country:kr", metric, evidenceAt, dimension),
            StableId = $"trade:kr:import:{manifest.Period}:hs6:{source.HsCode}:partner:{source.PartnerCode}:{(unit == "USD" ? "value" : "weight")}",
            SourceId = TradeSourceId, DatasetId = TradeDatasetId(manifest), RegionStableId = "country:kr", MetricCode = metric,
            NumericValue = value, TextValue = partnerName, UnitCode = unit, EvidenceAsOfUtc = evidenceAt, CollectedAtUtc = collectedAt,
            SpatialPrecisionCode = "reporter-country-partner-country", TemporalPrecisionCode = "year",
            QualityCode = measureUnavailable ? "OfficialMeasureUnavailable"
                : source.PartnerCode == 0 ? "ObservedOfficialAggregate" : "ObservedOfficialReported",
            LimitationCode = (source.PartnerCode == 0 ? "PublicPreview;WorldAggregate;MayBeRevised;NotIndividualProductEvidence" : "PublicPreview;ReportedPartnerValue;MayBeRevised;NotIndividualProductEvidence")
                + (measureUnavailable ? ";OfficialMeasureUnavailable;NullIsNotZero" : string.Empty),
            DimensionKey = dimension, SourceVersion = $"UN Comtrade H6 annual {manifest.Period}", DataRevision = $"{manifest.BatchId}-trade-r1",
            FirstSeenAtUtc = collectedAt, LastSeenAtUtc = collectedAt,
        };
    }

    private static 외부데이터정규화Record CreateTradeAvailability(HsEntry entry, BatchManifest manifest, DateTimeOffset collectedAt)
    {
        var evidenceAt = TradeEvidenceAt(manifest);
        var dimension = $"batch={manifest.BatchId};hs6={entry.Hs6};flow=import;period={manifest.Period};classification=H6";
        var observed = entry.TradeObservationStatus == "Observed";
        return new 외부데이터정규화Record
        {
            RecordKey = 외부데이터RecordKey.Create(TradeSourceId, TradeDatasetId(manifest), "country:kr", "trade.import.observation-status", evidenceAt, dimension),
            StableId = $"trade:kr:import:{manifest.Period}:hs6:{entry.Hs6}:observation-status",
            SourceId = TradeSourceId,
            DatasetId = TradeDatasetId(manifest),
            RegionStableId = "country:kr",
            MetricCode = "trade.import.observation-status",
            TextValue = entry.TradeObservationStatus,
            UnitCode = "status",
            EvidenceAsOfUtc = evidenceAt,
            CollectedAtUtc = collectedAt,
            SpatialPrecisionCode = "reporter-country",
            TemporalPrecisionCode = "year",
            QualityCode = observed ? "ObservedOfficialAggregate" : "OfficialQueryNoReportedRows",
            LimitationCode = observed
                ? "PublicPreview;WorldAggregatePresent;MayBeRevised;NotIndividualProductEvidence"
                : "PublicPreview;NoReportedRowsForRequestedHs6;AbsenceInResponseIsNotProofOfNoTrade;NullIsNotZero",
            DimensionKey = dimension,
            SourceVersion = $"UN Comtrade H6 annual {manifest.Period}",
            DataRevision = $"{manifest.BatchId}-trade-r1",
            FirstSeenAtUtc = collectedAt,
            LastSeenAtUtc = collectedAt,
        };
    }

    private static List<외부데이터정규화Record> ParseClassifications(string path, BatchManifest manifest, DateTimeOffset collectedAt)
        => ParseReference(path).Select(item =>
        {
            var dimension = $"batch={manifest.BatchId};hs6={item.Id};binding=CategoryOnly";
            return new 외부데이터정규화Record
            {
                RecordKey = 외부데이터RecordKey.Create(ReferenceSourceId, HsDatasetId(manifest.BatchId), "country:kr", "trade.hs6.category", HsEvidenceAt, dimension),
                StableId = $"classification:hs:h6:{item.Id}", SourceId = ReferenceSourceId, DatasetId = HsDatasetId(manifest.BatchId),
                RegionStableId = "country:kr", MetricCode = "trade.hs6.category", TextValue = item.Text, UnitCode = "category",
                EvidenceAsOfUtc = HsEvidenceAt, CollectedAtUtc = collectedAt, SpatialPrecisionCode = "global-classification",
                TemporalPrecisionCode = "classification-revision", QualityCode = "OfficialReference",
                LimitationCode = "CategoryOnly;NotIndividualProductClassification", DimensionKey = dimension,
                SourceVersion = "H6 (HS 2022)", DataRevision = $"{manifest.BatchId}-hs-r1", FirstSeenAtUtc = collectedAt, LastSeenAtUtc = collectedAt,
            };
        }).ToList();

    private static List<외부데이터정규화Record> ParseCategoryBindings(BatchManifest manifest, DateTimeOffset collectedAt)
        => manifest.HsEntries.Select(entry => manifest.SchemaVersion == "trade-retail-batch.v1"
            ? CreateCategoryBinding(entry, manifest, collectedAt)
            : CreateRetailChannelReview(entry, manifest, collectedAt)).ToList();

    private static 외부데이터정규화Record CreateCategoryBinding(HsEntry entry, BatchManifest manifest, DateTimeOffset collectedAt)
    {
        Require(entry.CategoryBindingStatus == "PendingSellerCategoryVerification" && entry.CoupangDisplayCategoryCode is null, "TradeRetailBatchCategoryApprovalChanged");
        var dimension = $"batch={manifest.BatchId};hs6={entry.Hs6};hsk10={entry.Hsk10};retail={entry.RetailCategoryStableId};platform=coupang";
        var textValue = JsonSerializer.Serialize(new
        {
            entry.Hs6, entry.Hsk10, entry.KoreanName, entry.RetailCategoryStableId, entry.RetailCategoryName,
            entry.CoupangLeafCategoryCandidate, entry.CoupangDisplayCategoryCode, entry.CategoryBindingStatus, entry.SearchQuery,
        });
        return new 외부데이터정규화Record
        {
            RecordKey = 외부데이터RecordKey.Create(CategorySourceId, CategoryDatasetId(manifest.BatchId), "country:kr", "trade.retail-category.binding-candidate", manifest.ObservedAt, dimension),
            StableId = $"category-binding:hs6:{entry.Hs6}:coupang:{entry.RetailCategoryStableId["retail-category:".Length..]}",
            SourceId = CategorySourceId, DatasetId = CategoryDatasetId(manifest.BatchId), RegionStableId = "country:kr",
            MetricCode = "trade.retail-category.binding-candidate", TextValue = textValue, UnitCode = "candidate-relation",
            EvidenceAsOfUtc = manifest.ObservedAt, CollectedAtUtc = collectedAt, SpatialPrecisionCode = "country-marketplace",
            TemporalPrecisionCode = "observed-date", QualityCode = "CandidateCrosswalk",
            LimitationCode = "PendingSellerCategoryVerification;ManyToMany;NotAuthoritativeProductClassification;PrivateReview",
            DimensionKey = dimension, SourceVersion = manifest.ObservedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DataRevision = $"{manifest.BatchId}-category-r1", FirstSeenAtUtc = collectedAt, LastSeenAtUtc = collectedAt,
        };
    }

    private static 외부데이터정규화Record CreateRetailChannelReview(HsEntry entry, BatchManifest manifest, DateTimeOffset collectedAt)
    {
        var dimension = $"batch={manifest.BatchId};hs6={entry.Hs6};platform=coupang;triage={entry.RetailTriageStatus}";
        var fullTextValue = JsonSerializer.Serialize(new
        {
            entry.Hs6,
            hsk10Entries = entry.Hsk10Entries,
            entry.RetailTriageStatus,
            entry.RetailTriageReasonCode,
            entry.PolicyEvidenceUrl,
            entry.SearchQuery,
            entry.ExpectedProductObservationCount,
        });
        var textValue = fullTextValue.Length <= 2000
            ? fullTextValue
            : JsonSerializer.Serialize(new
            {
                entry.Hs6,
                hsk10EntryCount = entry.Hsk10Entries.Count,
                hsk10Codes = entry.Hsk10Entries.Select(x => x.Hsk10).ToArray(),
                hsk10EntriesSha256 = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(fullTextValue))).ToLowerInvariant(),
                entry.RetailTriageStatus,
                entry.RetailTriageReasonCode,
                entry.PolicyEvidenceUrl,
                entry.SearchQuery,
                entry.ExpectedProductObservationCount,
                storageNote = "Full HSK10 entries remain in the immutable batch manifest.",
            });
        Require(textValue.Length <= 2000, "TradeRetailBatchChannelReviewTextTooLong");
        return new 외부데이터정규화Record
        {
            RecordKey = 외부데이터RecordKey.Create(CategorySourceId, CategoryDatasetId(manifest.BatchId), "country:kr", "trade.retail.channel-review-outcome", manifest.ObservedAt, dimension),
            StableId = $"retail-channel-review:coupang:hs6:{entry.Hs6}:{manifest.ObservedAt:yyyyMMdd}",
            SourceId = CategorySourceId, DatasetId = CategoryDatasetId(manifest.BatchId), RegionStableId = "country:kr",
            MetricCode = "trade.retail.channel-review-outcome", NumericValue = entry.ExpectedProductObservationCount,
            TextValue = textValue, UnitCode = "candidate-count",
            EvidenceAsOfUtc = manifest.ObservedAt, CollectedAtUtc = collectedAt, SpatialPrecisionCode = "country-marketplace",
            TemporalPrecisionCode = "observed-date", QualityCode = entry.RetailTriageStatus,
            LimitationCode = "PrivateReview;ChannelDecisionOnly;NotAuthoritativeProductClassification;NoPopularityClaim;DistributionNotApproved",
            DimensionKey = dimension, SourceVersion = manifest.ObservedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DataRevision = $"{manifest.BatchId}-category-r2", FirstSeenAtUtc = collectedAt, LastSeenAtUtc = collectedAt,
        };
    }

    private static ProductParseResult ParseProductDocument(string path, BatchManifest manifest)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var schemaVersion = root.GetProperty("schemaVersion").GetString();
        Require(schemaVersion is "representative-product-observation.v2" or "representative-product-observation.v3"
            && root.GetProperty("batchId").GetString() == manifest.BatchId
            && !root.GetProperty("distributionApproved").GetBoolean(), "TradeRetailBatchProductSchemaChanged");
        var observedAt = root.GetProperty("observedAt").GetDateTimeOffset();
        Require(observedAt == manifest.ObservedAt, "TradeRetailBatchObservationTimeChanged");
        var entries = manifest.HsEntries.ToDictionary(x => x.Hs6, StringComparer.Ordinal);

        if (schemaVersion == "representative-product-observation.v2")
        {
            Require(manifest.SchemaVersion == "trade-retail-batch.v1"
                && root.GetProperty("directMarketplaceSearchStatus").GetString() == "Blocked403", "TradeRetailBatchProductSchemaChanged");
            var seenCodes = new HashSet<string>(StringComparer.Ordinal);
            var rows = root.GetProperty("items").EnumerateArray().Select(item =>
            {
                var hsCandidate = item.GetProperty("hs6Candidate").GetString()!;
                Require(seenCodes.Add(hsCandidate), "TradeRetailBatchProductHsChanged");
                if (!entries.TryGetValue(hsCandidate, out var entry)) throw new InvalidDataException("TradeRetailBatchProductHsChanged");
                Require(item.GetProperty("resultPosition").GetInt32() == 1
                    && item.GetProperty("marketplaceCategoryCandidate").GetString() == entry.CoupangLeafCategoryCandidate
                    && item.GetProperty("retailCategoryStableId").GetString() == entry.RetailCategoryStableId
                    && item.GetProperty("hsk10Candidate").GetString() == entry.Hsk10,
                    "TradeRetailBatchProductCategoryChanged");
                return CreateProductObservation(item, entry, manifest, observedAt, "r1");
            }).ToList();
            Require(seenCodes.SetEquals(entries.Keys), "TradeRetailBatchProductCoverageChanged");
            return new ProductParseResult(rows, []);
        }

        Require(manifest.SchemaVersion == "trade-retail-batch.v2"
            && root.GetProperty("directMarketplaceSearchStatus").GetString() is "Blocked403" or "PolicyRestricted" or "CompletedViaIndexedSearch",
            "TradeRetailBatchProductSchemaChanged");
        var items = root.GetProperty("items").EnumerateArray().ToArray();
        var itemGroups = items.GroupBy(x => x.GetProperty("hs6Candidate").GetString()!, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);
        Require(items.Select(x => x.GetProperty("observationStableId").GetString()).Distinct(StringComparer.Ordinal).Count() == items.Length,
            "TradeRetailBatchProductObservationIdChanged");
        var observationRows = new List<외부데이터정규화Record>();
        foreach (var entry in manifest.HsEntries)
        {
            itemGroups.TryGetValue(entry.Hs6, out var codeItems);
            codeItems ??= [];
            Require(codeItems.Length == entry.ExpectedProductObservationCount && codeItems.Length <= 3
                && codeItems.Select(x => x.GetProperty("resultPosition").GetInt32()).Distinct().Count() == codeItems.Length,
                "TradeRetailBatchProductCountChanged");
            foreach (var item in codeItems)
            {
                var hsk10 = item.GetProperty("hsk10Candidate").GetString()!;
                Require(item.GetProperty("resultPosition").GetInt32() is >= 1 and <= 3
                    && entry.Hsk10Entries.Any(x => x.Hsk10 == hsk10), "TradeRetailBatchProductCategoryChanged");
                observationRows.Add(CreateProductObservation(item, entry, manifest, observedAt, "r2"));
            }
        }
        Require(itemGroups.Keys.All(entries.ContainsKey), "TradeRetailBatchProductHsChanged");

        var reviews = root.GetProperty("codeReviews").EnumerateArray().ToArray();
        Require(reviews.Length == entries.Count
            && reviews.Select(x => x.GetProperty("hs6").GetString()).Distinct(StringComparer.Ordinal).Count() == entries.Count,
            "TradeRetailBatchProductReviewCoverageChanged");
        var reviewRows = reviews.Select(review =>
        {
            var hs6 = review.GetProperty("hs6").GetString()!;
            if (!entries.TryGetValue(hs6, out var entry)) throw new InvalidDataException("TradeRetailBatchProductReviewCoverageChanged");
            var candidateCount = review.GetProperty("candidateCount").GetInt32();
            var triage = review.GetProperty("retailTriageStatus").GetString()!;
            var searchStatus = review.GetProperty("searchStatus").GetString()!;
            var productStatus = review.GetProperty("representativeProductStatus").GetString()!;
            var reasonCode = review.GetProperty("reasonCode").GetString()!;
            var evidenceUrls = review.GetProperty("evidenceUrls").EnumerateArray().Select(x => x.GetString()!).ToArray();
            Require(candidateCount == entry.ExpectedProductObservationCount
                && triage == entry.RetailTriageStatus && reasonCode == entry.RetailTriageReasonCode
                && review.GetProperty("searchQuery").GetString() == entry.SearchQuery
                && !review.GetProperty("distributionApproved").GetBoolean()
                && IsValidReviewState(triage, searchStatus, productStatus, candidateCount)
                && (triage != "RestrictedOrSensitive" || evidenceUrls.Contains(entry.PolicyEvidenceUrl, StringComparer.Ordinal)),
                "TradeRetailBatchProductReviewChanged");
            foreach (var rawUrl in evidenceUrls)
            {
                var url = new Uri(rawUrl, UriKind.Absolute);
                Require(url.Scheme == Uri.UriSchemeHttps, "TradeRetailBatchProductReviewEvidenceRejected");
            }
            var dimension = $"batch={manifest.BatchId};hs6={hs6};platform=coupang;triage={triage}";
            return new 외부데이터정규화Record
            {
                RecordKey = 외부데이터RecordKey.Create(ProductSourceId, ProductDatasetId(manifest), "country:kr", "retail.product.review-outcome", observedAt, dimension),
                StableId = $"product-review:coupang:hs6:{hs6}:{observedAt:yyyyMMdd}",
                SourceId = ProductSourceId, DatasetId = ProductDatasetId(manifest), RegionStableId = "country:kr",
                MetricCode = "retail.product.review-outcome", NumericValue = candidateCount, TextValue = review.GetRawText(), UnitCode = "candidate-count",
                EvidenceAsOfUtc = observedAt, CollectedAtUtc = observedAt, SpatialPrecisionCode = "country-marketplace",
                TemporalPrecisionCode = "observed-date", QualityCode = triage,
                LimitationCode = "PrivateReview;NoPopularityClaim;NoLiveInventoryClaim;NoHsProductBindingClaim;DistributionNotApproved",
                DimensionKey = dimension, SourceVersion = observedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                DataRevision = $"{manifest.BatchId}-product-r2", FirstSeenAtUtc = observedAt, LastSeenAtUtc = observedAt,
            };
        }).ToList();
        return new ProductParseResult(observationRows, reviewRows);
    }

    private static 외부데이터정규화Record CreateProductObservation(JsonElement item, HsEntry entry, BatchManifest manifest,
        DateTimeOffset observedAt, string revision)
    {
        var hsCandidate = item.GetProperty("hs6Candidate").GetString()!;
        var platform = item.GetProperty("platformCode").GetString()!;
        var query = item.GetProperty("searchQuery").GetString()!;
        var position = item.GetProperty("resultPosition").GetInt32();
        var placement = item.GetProperty("placementCode").GetString()!;
        Require(hsCandidate == entry.Hs6 && platform == "coupang" && query == entry.SearchQuery
            && placement is "OrganicIndexedResult" or "PaidPlacementIndexed", "TradeRetailBatchProductPlacementChanged");
        Require(item.GetProperty("hsBindingStatus").GetString() == "PendingHumanReview"
            && !item.GetProperty("affiliate").GetBoolean() && !item.GetProperty("distributionApproved").GetBoolean(), "TradeRetailBatchProductApprovalChanged");
        var url = new Uri(item.GetProperty("canonicalExternalUrl").GetString()!, UriKind.Absolute);
        Require(url.Scheme == Uri.UriSchemeHttps && url.Host == "www.coupang.com" && url.AbsolutePath.StartsWith("/vp/products/", StringComparison.Ordinal), "TradeRetailBatchProductUrlRejected");
        var stableId = item.GetProperty("observationStableId").GetString()!;
        var dimension = $"batch={manifest.BatchId};observation={stableId};platform={platform};query={query};position={position};hs6Candidate={hsCandidate};binding=PendingHumanReview";
        var paid = placement == "PaidPlacementIndexed";
        return new 외부데이터정규화Record
        {
            RecordKey = 외부데이터RecordKey.Create(ProductSourceId, ProductDatasetId(manifest), "country:kr", "retail.product.observation", observedAt, dimension),
            StableId = stableId, SourceId = ProductSourceId, DatasetId = ProductDatasetId(manifest), RegionStableId = "country:kr",
            MetricCode = "retail.product.observation", NumericValue = item.GetProperty("observedPriceKrw").GetDecimal(), TextValue = item.GetRawText(), UnitCode = "KRW",
            EvidenceAsOfUtc = observedAt, CollectedAtUtc = observedAt, SpatialPrecisionCode = "country-marketplace",
            TemporalPrecisionCode = "indexed-observation-date", QualityCode = paid ? "ObservedIndexedPaidPlacement" : "ObservedIndexedMarketplaceResult",
            LimitationCode = (paid ? "PaidPlacement;" : string.Empty) + "SearchEngineRankNotMarketplaceRank;NotCurrentPrice;AvailabilityMayDiffer;PendingHsBinding;OriginClaimUnverified;PrivateReview",
            DimensionKey = dimension, SourceVersion = observedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DataRevision = $"{manifest.BatchId}-product-{revision}", FirstSeenAtUtc = observedAt, LastSeenAtUtc = observedAt,
        };
    }

    private static bool IsValidReviewState(string triage, string searchStatus, string productStatus, int candidateCount)
        => (triage == "ProductCandidateObserved" && searchStatus == "IndexedCandidateObserved" && productStatus == "PendingHumanReview" && candidateCount is >= 1 and <= 3)
           || (triage == "SearchNoCandidate" && searchStatus == "NoIndexedCandidateObserved" && productStatus == "NoCandidateObserved" && candidateCount == 0)
           || (triage == "ChannelInapplicable" && searchStatus == "NotApplicable" && productStatus == "NotApplicable" && candidateCount == 0)
           || (triage == "RestrictedOrSensitive" && searchStatus == "PolicyReviewedRestricted" && productStatus == "NotApplicable" && candidateCount == 0);

    private static string[] SelectPartnerCodes(byte[] tradeBytes, IReadOnlyCollection<string> codes)
    {
        using var document = JsonDocument.Parse(tradeBytes);
        var rows = document.RootElement.GetProperty("data").EnumerateArray()
            .Where(x => codes.Contains(x.GetProperty("cmdCode").GetString()!, StringComparer.Ordinal))
            .Select(x => new { Hs = x.GetProperty("cmdCode").GetString()!, Partner = x.GetProperty("partnerCode").GetInt32(), Value = x.GetProperty("primaryValue").GetDecimal() })
            .ToList();
        var selected = rows.GroupBy(x => x.Hs, StringComparer.Ordinal)
            .SelectMany(group => group.Where(x => x.Partner == 0).Concat(group.Where(x => x.Partner != 0).OrderByDescending(x => x.Value).Take(5)))
            .Select(x => x.Partner.ToString(CultureInfo.InvariantCulture)).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Require(selected.Length == 0 || selected.Contains("0", StringComparer.Ordinal), "TradeRetailBatchWorldPartnerMissing");
        return selected;
    }

    private static byte[] SelectReferenceRows(byte[] bytes, IReadOnlyCollection<string> ids, string sourceUrl)
    {
        using var document = JsonDocument.Parse(bytes);
        var selected = document.RootElement.GetProperty("results").EnumerateArray()
            .Where(row => ids.Contains(row.GetProperty("id").ToString(), StringComparer.Ordinal))
            .OrderBy(row => row.GetProperty("id").ToString(), StringComparer.Ordinal)
            .Select(row => JsonSerializer.Deserialize<Dictionary<string, object?>>(row.GetRawText())!).ToArray();
        Require(selected.Length == ids.Count, "TradeRetailBatchReferenceSelectionCountChanged");
        return JsonSerializer.SerializeToUtf8Bytes(new { sourceUrl, selectedAtUtc = DateTimeOffset.UtcNow, results = selected }, JsonOptions);
    }

    private static List<ReferenceRow> ParseReference(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("results").EnumerateArray()
            .Select(item => new ReferenceRow(item.GetProperty("id").ToString(), item.GetProperty("text").GetString()!)).ToList();
    }

    private static async Task<BatchManifest> ReadManifestAsync(string path, string batchId)
    {
        Require(File.Exists(path), "TradeRetailBatchManifestMissing");
        var manifest = JsonSerializer.Deserialize<BatchManifest>(await File.ReadAllTextAsync(path), JsonOptions)
            ?? throw new InvalidDataException("TradeRetailBatchManifestInvalid");
        Require(manifest.SchemaVersion is "trade-retail-batch.v1" or "trade-retail-batch.v2"
            && manifest.BatchId == batchId && !manifest.DistributionApproved
            && manifest.Period.All(char.IsDigit) && manifest.Period.Length == 4 && manifest.ReporterCode == 410
            && manifest.FlowCode == "M" && manifest.ClassificationCode == "H6"
            && (manifest.SchemaVersion == "trade-retail-batch.v1" ? manifest.ExpectedSourceRows > 0 : manifest.ExpectedSourceRows >= 0)
            && manifest.HsEntries.Count > 0 && manifest.HsEntries.Select(x => x.Hs6).Distinct(StringComparer.Ordinal).Count() == manifest.HsEntries.Count,
            "TradeRetailBatchManifestBoundaryChanged");
        if (manifest.SchemaVersion == "trade-retail-batch.v1")
        {
            Require(manifest.HsEntries.All(x => IsDigits(x.Hs6, 6) && IsDigits(x.Hsk10, 10)
                && x.Hsk10.StartsWith(x.Hs6, StringComparison.Ordinal)
                && x.ExpectedWorldValueUsd.HasValue && x.ExpectedWorldNetWeightKg.HasValue
                && x.RetailCategoryStableId.StartsWith("retail-category:", StringComparison.Ordinal)), "TradeRetailBatchHsEntryInvalid");
        }
        else
        {
            Require(manifest.HsEntries.All(x => IsDigits(x.Hs6, 6)
                && x.Hsk10Entries.Count > 0
                && x.Hsk10Entries.Select(y => y.Hsk10).Distinct(StringComparer.Ordinal).Count() == x.Hsk10Entries.Count
                && x.Hsk10Entries.All(y => IsDigits(y.Hsk10, 10) && y.Hsk10.StartsWith(x.Hs6, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(y.KoreanName))
                && x.TradeObservationStatus is "Observed" or "NoReportedRows"
                && (x.TradeObservationStatus == "Observed" ? x.ExpectedWorldValueUsd.HasValue : x.ExpectedWorldValueUsd is null && x.ExpectedWorldNetWeightKg is null)
                && x.RetailTriageStatus is "ProductCandidateObserved" or "SearchNoCandidate" or "ChannelInapplicable" or "RestrictedOrSensitive"
                && x.ExpectedProductObservationCount is >= 0 and <= 3
                && (x.RetailTriageStatus == "ProductCandidateObserved" ? x.ExpectedProductObservationCount > 0 : x.ExpectedProductObservationCount == 0)
                && !string.IsNullOrWhiteSpace(x.RetailTriageReasonCode) && !string.IsNullOrWhiteSpace(x.SearchQuery)
                && (x.RetailTriageStatus != "RestrictedOrSensitive" || IsHttpsUrl(x.PolicyEvidenceUrl))),
                "TradeRetailBatchHsEntryInvalid");
        }
        return manifest;
    }

    private static async Task UpdateRunAsync(PublicDataIngestionDbContext db, 공공공간원본등록Result source, int fetched, int normalized, int inserted, string summary)
    {
        if (!source.Inserted) return;
        var snapshot = await db.RawSnapshots.SingleAsync(x => x.Id == source.RawSnapshotId);
        var run = await db.IngestionRuns.SingleAsync(x => x.Id == snapshot.FirstCollectionRunId);
        run.StatusCode = 외부데이터수집StatusCodes.Partial;
        run.FetchedCount = fetched; run.NormalizedCount = normalized; run.InsertedCount = inserted;
        run.ErrorCode = "PrivateReviewSubset"; run.ErrorSummary = summary;
        await db.SaveChangesAsync();
    }

    private static object ReceiptEntry(string fileName, string sourceUrl, string filePath, DateTimeOffset collectedAt)
    {
        var bytes = File.ReadAllBytes(filePath);
        return new { fileName, sourceUrl, collectedAtUtc = collectedAt, contentLength = bytes.LongLength, sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() };
    }

    private static bool Same(외부데이터정규화Record x, 외부데이터정규화Record y)
        => x.RecordKey == y.RecordKey && x.StableId == y.StableId && x.SourceId == y.SourceId && x.DatasetId == y.DatasetId
           && x.RegionStableId == y.RegionStableId && x.MetricCode == y.MetricCode && x.NumericValue == y.NumericValue
           && x.TextValue == y.TextValue && x.UnitCode == y.UnitCode && x.EvidenceAsOfUtc == y.EvidenceAsOfUtc
           && x.SpatialPrecisionCode == y.SpatialPrecisionCode && x.TemporalPrecisionCode == y.TemporalPrecisionCode
           && x.QualityCode == y.QualityCode && x.LimitationCode == y.LimitationCode && x.DimensionKey == y.DimensionKey
           && x.SourceVersion == y.SourceVersion && x.DataRevision == y.DataRevision;

    private static decimal? ReadNullableDecimal(JsonElement value) => value.ValueKind == JsonValueKind.Null ? null : value.GetDecimal();
    private static bool IsDigits(string? value, int length) => value?.Length == length && value.All(char.IsAsciiDigit);
    private static bool IsHttpsUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
    private static string ImportLockName(string batchId)
    {
        var digest = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(batchId))).ToLowerInvariant();
        return "mirror:public-data:trade-retail:" + digest[..24];
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static string ManifestPath(string root, string batchId) => Path.Combine(root, "eng", "public-data", "trade-retail", "batches", batchId + ".manifest.json");
    private static string FolderRelative(string batchId) => $"artifacts/local/public-data/trade-retail/batches/{batchId}";
    private static DateTimeOffset TradeEvidenceAt(BatchManifest manifest) => new(int.Parse(manifest.Period, CultureInfo.InvariantCulture), 12, 31, 0, 0, 0, TimeSpan.Zero);
    private static string TradeDatasetId(BatchManifest manifest) => $"kr-imports-{manifest.Period}-h6-{manifest.BatchId}";
    private static string HsDatasetId(string batchId) => $"hs-2022-h6-{batchId}";
    private static string PartnerDatasetId(string batchId) => $"partner-areas-{batchId}";
    private static string CategoryDatasetId(string batchId) => $"hs-retail-category-candidates-{batchId}";
    private static string ProductDatasetId(BatchManifest manifest) => manifest.SchemaVersion == "trade-retail-batch.v1"
        ? $"coupang-products-{manifest.BatchId}-20260921"
        : $"coupang-retail-review-{manifest.BatchId}-{manifest.ObservedAt:yyyyMMdd}";
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidDataException(code); }

    private sealed class BatchManifest
    {
        public string SchemaVersion { get; init; } = string.Empty;
        public string BatchId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string Period { get; init; } = string.Empty;
        public int ReporterCode { get; init; }
        public string FlowCode { get; init; } = string.Empty;
        public string ClassificationCode { get; init; } = string.Empty;
        public int ExpectedSourceRows { get; init; }
        public DateTimeOffset ObservedAt { get; init; }
        public bool DistributionApproved { get; init; }
        public string ProductObservationRelativePath { get; init; } = string.Empty;
        public List<HsEntry> HsEntries { get; init; } = [];
        public List<string> Limitations { get; init; } = [];
    }

    private sealed class HsEntry
    {
        public string Hs6 { get; init; } = string.Empty;
        public string Hsk10 { get; init; } = string.Empty;
        public string KoreanName { get; init; } = string.Empty;
        public decimal? ExpectedWorldValueUsd { get; init; }
        public decimal? ExpectedWorldNetWeightKg { get; init; }
        public string RetailCategoryStableId { get; init; } = string.Empty;
        public string RetailCategoryName { get; init; } = string.Empty;
        public string CoupangLeafCategoryCandidate { get; init; } = string.Empty;
        public long? CoupangDisplayCategoryCode { get; init; }
        public string CategoryBindingStatus { get; init; } = string.Empty;
        public string SearchQuery { get; init; } = string.Empty;
        public List<Hsk10Entry> Hsk10Entries { get; init; } = [];
        public string TradeObservationStatus { get; init; } = string.Empty;
        public string RetailTriageStatus { get; init; } = string.Empty;
        public string RetailTriageReasonCode { get; init; } = string.Empty;
        public string PolicyEvidenceUrl { get; init; } = string.Empty;
        public int ExpectedProductObservationCount { get; init; }
    }

    private sealed class Hsk10Entry
    {
        public string Hsk10 { get; init; } = string.Empty;
        public string KoreanName { get; init; } = string.Empty;
        public string NatureGroupCode { get; init; } = string.Empty;
        public string NatureGroupName { get; init; } = string.Empty;
    }

    private sealed record ProductParseResult(List<외부데이터정규화Record> ObservationRows, List<외부데이터정규화Record> ReviewOutcomeRows);
    private sealed record ReferenceRow(string Id, string Text);
    private sealed record TradeRow(string HsCode, int PartnerCode, decimal PrimaryValue, decimal? NetWeight, string Period, int ReporterCode, string FlowCode, string Classification, bool IsReported, bool IsAggregate);
}
