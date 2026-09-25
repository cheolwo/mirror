using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

internal static class 무역소매대표상품Import
{
    private const string TradeSourceId = "un-comtrade-public";
    private const string TradeDatasetId = "kr-imports-2025-h6";
    private const string ReferenceSourceId = "un-comtrade-reference";
    private const string HsDatasetId = "hs-2022-h6-selected";
    private const string PartnerDatasetId = "partner-areas-selected";
    private const string ProductSourceId = "manual-marketplace-observation";
    private const string ProductDatasetId = "representative-products-2026-09-21";
    private static readonly DateTimeOffset TradeEvidenceAt = new(2025, 12, 31, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset HsEvidenceAt = new(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Dictionary<string, (decimal Value, decimal Weight)> ExpectedTotals = new(StringComparer.Ordinal)
    {
        ["030617"] = (545729404m, 68588541.626m),
        ["080390"] = (315897628m, 360823989.254m),
        ["090121"] = (361173321m, 17179143.041m),
    };

    public static async Task RunAsync(string mode, string root, Dictionary<string, object?> result)
    {
        Require(mode is "self-test" or "preview" or "apply" or "verify", "TradeRetailModeInvalid");
        var folder = Path.Combine(root, 무역소매대표상품Acquisition.Relative);
        var receiptPath = Path.Combine(folder, 무역소매대표상품Acquisition.ReceiptFile);
        Require(File.Exists(receiptPath), "TradeRetailAcquisitionMissing");
        using var receipt = JsonDocument.Parse(await File.ReadAllTextAsync(receiptPath));
        var collectedAt = receipt.RootElement.GetProperty("collectedAtUtc").GetDateTimeOffset();
        foreach (var file in receipt.RootElement.GetProperty("files").EnumerateArray())
        {
            var path = Path.Combine(folder, file.GetProperty("fileName").GetString()!);
            Require(File.Exists(path), "TradeRetailInputMissing");
            var actual = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path))).ToLowerInvariant();
            Require(actual == file.GetProperty("sha256").GetString(), "TradeRetailInputHashChanged");
        }

        var tradePath = Path.Combine(folder, 무역소매대표상품Acquisition.TradeFile);
        var hsPath = Path.Combine(folder, 무역소매대표상품Acquisition.HsFile);
        var partnerPath = Path.Combine(folder, 무역소매대표상품Acquisition.PartnerFile);
        var partnerHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(partnerPath))).ToLowerInvariant();
        var productPath = Path.Combine(root, 무역소매대표상품Acquisition.ProductRelative);
        Require(File.Exists(productPath), "TradeRetailProductObservationMissing");

        var hs = ParseReference(hsPath).ToDictionary(x => x.Id, x => x.Text, StringComparer.Ordinal);
        var partners = ParseReference(partnerPath).ToDictionary(x => int.Parse(x.Id, CultureInfo.InvariantCulture), x => x.Text);
        var tradeRows = ParseTrade(tradePath, collectedAt, hs, partners);
        var classificationRows = ParseClassifications(hsPath, collectedAt);
        var productRows = ParseProducts(productPath);
        Require(tradeRows.Count == 36 && classificationRows.Count == 3 && productRows.Count == 6, "TradeRetailNormalizedCountChanged");
        var allRows = tradeRows.Concat(classificationRows).Concat(productRows).ToList();
        Require(allRows.Select(x => x.RecordKey).Distinct(StringComparer.Ordinal).Count() == 45, "TradeRetailRecordKeyCollision");

        if (mode == "self-test")
        {
            Require(tradeRows.Count(x => x.MetricCode == "trade.import.value.usd") == 18, "TradeValueCountChanged");
            Require(tradeRows.Count(x => x.MetricCode == "trade.import.net-weight.kg") == 18, "TradeWeightCountChanged");
            Require(productRows.All(x => x.QualityCode == "ObservedManualPaidPlacement" && x.LimitationCode.Contains("PendingHsBinding", StringComparison.Ordinal)), "ProductBoundaryChanged");
            result["selfTestsPassed"] = 9;
            result["normalizedRows"] = allRows.Count;
            result["tradeRows"] = tradeRows.Count;
            result["classificationRows"] = classificationRows.Count;
            result["productRows"] = productRows.Count;
            return;
        }

        var options = await 로컬공공자료Db.OptionsAsync(root);
        var keys = allRows.Select(x => x.RecordKey).ToList();
        await using (var db = new PublicDataIngestionDbContext(options))
        {
            var existing = await db.NormalizedRecords.AsNoTracking().Where(x => keys.Contains(x.RecordKey)).ToListAsync();
            Require(existing.All(x => allRows.Any(y => Same(x, y))), "TradeRetailExistingRecordConflict");
            result["beforeCount"] = existing.Count;
            if (mode == "apply")
            {
                await db.Database.OpenConnectionAsync();
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT GET_LOCK('mirror:public-data:trade-retail-2025-h6-r1',0)";
                Require(Convert.ToInt32(await command.ExecuteScalarAsync()) == 1, "TradeRetailImportBusy");
                await using var transaction = await db.Database.BeginTransactionAsync();
                result["databaseWriteAttempted"] = true;
                var registrar = new 평창군공공공간원본등록Service(db);
                var tradeSource = await registrar.RegisterFileAsync(tradePath, new 공공공간원본등록Request(
                    TradeSourceId, TradeDatasetId, "UN Comtrade H6 annual 2025", "kr-imports-2025-h6-r1",
                    TradeEvidenceAt, "application/json", 무역소매대표상품Acquisition.Relative + "/" + 무역소매대표상품Acquisition.TradeFile));
                var hsSource = await registrar.RegisterFileAsync(hsPath, new 공공공간원본등록Request(
                    ReferenceSourceId, HsDatasetId, "H6 (HS 2022)", "un-comtrade-h6-selected-r1",
                    HsEvidenceAt, "application/json", 무역소매대표상품Acquisition.Relative + "/" + 무역소매대표상품Acquisition.HsFile));
                var partnerSource = await registrar.RegisterFileAsync(partnerPath, new 공공공간원본등록Request(
                    ReferenceSourceId, PartnerDatasetId, "UN M49 partner areas", "un-comtrade-partners-selected-r1",
                    TradeEvidenceAt, "application/json", 무역소매대표상품Acquisition.Relative + "/" + 무역소매대표상품Acquisition.PartnerFile));
                var productSource = await registrar.RegisterFileAsync(productPath, new 공공공간원본등록Request(
                    ProductSourceId, ProductDatasetId, "2026-09-21", "manual-browser-observation-r1",
                    productRows[0].EvidenceAsOfUtc, "application/json", 무역소매대표상품Acquisition.ProductRelative));

                tradeRows.ForEach(x => x.RawSnapshotId = tradeSource.RawSnapshotId);
                classificationRows.ForEach(x => x.RawSnapshotId = hsSource.RawSnapshotId);
                productRows.ForEach(x => x.RawSnapshotId = productSource.RawSnapshotId);
                var store = new EfExternalDataIngestionStore(db);
                var tradeSaved = await store.UpsertNormalizedAsync(tradeRows);
                var classificationSaved = await store.UpsertNormalizedAsync(classificationRows);
                var productSaved = await store.UpsertNormalizedAsync(productRows);
                Require(tradeSaved.UpdatedCount + classificationSaved.UpdatedCount + productSaved.UpdatedCount == 0, "TradeRetailUnexpectedUpdate");
                await UpdateRunAsync(db, tradeSource, 131, tradeRows.Count, tradeSaved.InsertedCount, "Top five partners plus world totals; public preview aggregate; may be revised; not product provenance.");
                await UpdateRunAsync(db, hsSource, 3, classificationRows.Count, classificationSaved.InsertedCount, "Selected H6 categories only; category is not an individual product classification decision.");
                await UpdateRunAsync(db, partnerSource, 13, 0, 0, "Selected M49 partner labels used only to decode stored partner codes.");
                await UpdateRunAsync(db, productSource, 6, productRows.Count, productSaved.InsertedCount, "Manual paid-placement observations; private review; no popularity, inventory, origin, publication or affiliate claim.");
                await transaction.CommitAsync();
                result["committed"] = true;
                result["inserted"] = tradeSaved.InsertedCount + classificationSaved.InsertedCount + productSaved.InsertedCount;
                result["existing"] = tradeSaved.ExistingCount + classificationSaved.ExistingCount + productSaved.ExistingCount;
            }
        }

        await using var verify = new PublicDataIngestionDbContext(options);
        var stored = await verify.NormalizedRecords.AsNoTracking()
            .Where(x => keys.Contains(x.RecordKey)).Include(x => x.RawSnapshot).ToListAsync();
        var partnerReference = await verify.RawSnapshots.AsNoTracking().SingleOrDefaultAsync(x =>
            x.SourceId == ReferenceSourceId && x.DatasetId == PartnerDatasetId && x.ContentHashSha256 == partnerHash);
        if (mode != "preview") Require(stored.Count == 45, "TradeRetailReadbackCountMismatch");
        Require(stored.All(x => allRows.Any(y => Same(x, y)) && x.RawSnapshot != null) && (mode == "preview" || partnerReference != null), "TradeRetailReadbackMismatch");
        result["mode"] = mode;
        result["verifiedRows"] = stored.Count;
        result["tradeRows"] = stored.Count(x => x.SourceId == TradeSourceId);
        result["classificationRows"] = stored.Count(x => x.DatasetId == HsDatasetId);
        result["productRows"] = stored.Count(x => x.SourceId == ProductSourceId);
        result["target"] = "hongdal-mysql-1 / hongdal_dev";
        result["sourceHashes"] = stored.Select(x => x.RawSnapshot!.ContentHashSha256).Append(partnerHash).Distinct().OrderBy(x => x).ToArray();
    }

    private static List<외부데이터정규화Record> ParseTrade(
        string path,
        DateTimeOffset collectedAt,
        IReadOnlyDictionary<string, string> hs,
        IReadOnlyDictionary<int, string> partners)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        Require(root.GetProperty("count").GetInt32() == 131 && root.GetProperty("data").GetArrayLength() == 131, "TradeSourceCountChanged");
        var sourceRows = root.GetProperty("data").EnumerateArray().Select(x => new TradeRow(
            x.GetProperty("cmdCode").GetString()!,
            x.GetProperty("partnerCode").GetInt32(),
            x.GetProperty("primaryValue").GetDecimal(),
            x.GetProperty("netWgt").GetDecimal(),
            x.GetProperty("period").GetString()!,
            x.GetProperty("reporterCode").GetInt32(),
            x.GetProperty("flowCode").GetString()!,
            x.GetProperty("classificationCode").GetString()!,
            x.GetProperty("isReported").GetBoolean(),
            x.GetProperty("isAggregate").GetBoolean())).ToList();
        Require(sourceRows.All(x => x.Period == "2025" && x.ReporterCode == 410 && x.FlowCode == "M" && x.Classification == "H6"
            && (x.PartnerCode == 0 ? x.IsAggregate && !x.IsReported : !x.IsAggregate && x.IsReported)), "TradeScopeChanged");
        foreach (var expected in ExpectedTotals)
        {
            var total = sourceRows.Single(x => x.HsCode == expected.Key && x.PartnerCode == 0);
            Require(total.PrimaryValue == expected.Value.Value && total.NetWeight == expected.Value.Weight, "TradeFrozenTotalChanged");
        }

        var selected = sourceRows.GroupBy(x => x.HsCode, StringComparer.Ordinal)
            .SelectMany(group => group.Where(x => x.PartnerCode == 0)
                .Concat(group.Where(x => x.PartnerCode != 0).OrderByDescending(x => x.PrimaryValue).Take(5)))
            .OrderBy(x => x.HsCode, StringComparer.Ordinal).ThenBy(x => x.PartnerCode).ToList();
        Require(selected.Count == 18 && selected.All(x => hs.ContainsKey(x.HsCode) && partners.ContainsKey(x.PartnerCode)), "TradeSelectionChanged");
        var rows = new List<외부데이터정규화Record>();
        foreach (var item in selected)
        {
            var partnerName = partners[item.PartnerCode];
            var dimension = $"hs6={item.HsCode};partnerM49={item.PartnerCode};partner={partnerName};flow=import;period=2025;classification=H6";
            rows.Add(CreateNumeric(item, "trade.import.value.usd", item.PrimaryValue, "USD", dimension, collectedAt, partnerName));
            rows.Add(CreateNumeric(item, "trade.import.net-weight.kg", item.NetWeight, "kg", dimension, collectedAt, partnerName));
        }
        return rows;
    }

    private static 외부데이터정규화Record CreateNumeric(
        TradeRow source,
        string metric,
        decimal value,
        string unit,
        string dimension,
        DateTimeOffset collectedAt,
        string partnerName)
    {
        var key = 외부데이터RecordKey.Create(TradeSourceId, TradeDatasetId, "country:kr", metric, TradeEvidenceAt, dimension);
        return new 외부데이터정규화Record
        {
            RecordKey = key,
            StableId = $"trade:kr:import:2025:hs6:{source.HsCode}:partner:{source.PartnerCode}:{(unit == "USD" ? "value" : "weight")}",
            SourceId = TradeSourceId,
            DatasetId = TradeDatasetId,
            RegionStableId = "country:kr",
            MetricCode = metric,
            NumericValue = value,
            TextValue = partnerName,
            UnitCode = unit,
            EvidenceAsOfUtc = TradeEvidenceAt,
            CollectedAtUtc = collectedAt,
            SpatialPrecisionCode = "reporter-country-partner-country",
            TemporalPrecisionCode = "year",
            QualityCode = source.PartnerCode == 0 ? "ObservedOfficialAggregate" : "ObservedOfficialReported",
            LimitationCode = source.PartnerCode == 0
                ? "PublicPreview;WorldAggregate;MayBeRevised;NotIndividualProductEvidence"
                : "PublicPreview;ReportedPartnerValue;MayBeRevised;NotIndividualProductEvidence",
            DimensionKey = dimension,
            SourceVersion = "UN Comtrade H6 annual 2025",
            DataRevision = "kr-imports-2025-h6-r1",
            FirstSeenAtUtc = collectedAt,
            LastSeenAtUtc = collectedAt,
        };
    }

    private static List<외부데이터정규화Record> ParseClassifications(string path, DateTimeOffset collectedAt)
        => ParseReference(path).Select(item =>
        {
            var dimension = $"hs6={item.Id};binding=CategoryOnly";
            return new 외부데이터정규화Record
            {
                RecordKey = 외부데이터RecordKey.Create(ReferenceSourceId, HsDatasetId, "country:kr", "trade.hs6.category", HsEvidenceAt, dimension),
                StableId = $"classification:hs:h6:{item.Id}",
                SourceId = ReferenceSourceId,
                DatasetId = HsDatasetId,
                RegionStableId = "country:kr",
                MetricCode = "trade.hs6.category",
                TextValue = item.Text,
                UnitCode = "category",
                EvidenceAsOfUtc = HsEvidenceAt,
                CollectedAtUtc = collectedAt,
                SpatialPrecisionCode = "global-classification",
                TemporalPrecisionCode = "classification-revision",
                QualityCode = "OfficialReference",
                LimitationCode = "CategoryOnly;NotIndividualProductClassification",
                DimensionKey = dimension,
                SourceVersion = "H6 (HS 2022)",
                DataRevision = "un-comtrade-h6-selected-r1",
                FirstSeenAtUtc = collectedAt,
                LastSeenAtUtc = collectedAt,
            };
        }).ToList();

    private static List<외부데이터정규화Record> ParseProducts(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        Require(root.GetProperty("schemaVersion").GetString() == "representative-product-observation.v1" && !root.GetProperty("distributionApproved").GetBoolean(), "ProductObservationSchemaChanged");
        var observedAt = root.GetProperty("observedAt").GetDateTimeOffset();
        return root.GetProperty("items").EnumerateArray().Select(item =>
        {
            var platform = item.GetProperty("platformCode").GetString()!;
            var query = item.GetProperty("searchQuery").GetString()!;
            var position = item.GetProperty("resultPosition").GetInt32();
            var hsCandidate = item.GetProperty("hs6Candidate").GetString()!;
            Require(position == 1 && item.GetProperty("placementCode").GetString() == "PaidPlacement", "ProductPlacementChanged");
            Require(item.GetProperty("hsBindingStatus").GetString() == "PendingHumanReview" && !item.GetProperty("affiliate").GetBoolean() && !item.GetProperty("distributionApproved").GetBoolean(), "ProductApprovalBoundaryChanged");
            var url = new Uri(item.GetProperty("canonicalExternalUrl").GetString()!, UriKind.Absolute);
            Require(url.Scheme == Uri.UriSchemeHttps && url.Host is "smartstore.naver.com" or "8dogam.com" or "brand.naver.com" or "www.coupang.com", "ProductExternalUrlRejected");
            var dimension = $"platform={platform};query={query};position={position};hs6Candidate={hsCandidate};binding=PendingHumanReview";
            return new 외부데이터정규화Record
            {
                RecordKey = 외부데이터RecordKey.Create(ProductSourceId, ProductDatasetId, "country:kr", "retail.product.observation", observedAt, dimension),
                StableId = item.GetProperty("observationStableId").GetString()!,
                SourceId = ProductSourceId,
                DatasetId = ProductDatasetId,
                RegionStableId = "country:kr",
                MetricCode = "retail.product.observation",
                NumericValue = item.GetProperty("observedPriceKrw").GetDecimal(),
                TextValue = item.GetRawText(),
                UnitCode = "KRW",
                EvidenceAsOfUtc = observedAt,
                CollectedAtUtc = observedAt,
                SpatialPrecisionCode = "country-marketplace",
                TemporalPrecisionCode = "observed-date",
                QualityCode = "ObservedManualPaidPlacement",
                LimitationCode = "PaidPlacement;NotPopularity;NotCurrentPrice;PendingHsBinding;NoOriginProof;PrivateReview",
                DimensionKey = dimension,
                SourceVersion = "2026-09-21",
                DataRevision = "manual-browser-observation-r1",
                FirstSeenAtUtc = observedAt,
                LastSeenAtUtc = observedAt,
            };
        }).ToList();
    }

    private static List<ReferenceRow> ParseReference(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("results").EnumerateArray()
            .Select(item => new ReferenceRow(item.GetProperty("id").ToString(), item.GetProperty("text").GetString()!))
            .ToList();
    }

    private static async Task UpdateRunAsync(PublicDataIngestionDbContext db, 공공공간원본등록Result source, int fetched, int normalized, int inserted, string summary)
    {
        if (!source.Inserted) return;
        var snapshot = await db.RawSnapshots.SingleAsync(x => x.Id == source.RawSnapshotId);
        var run = await db.IngestionRuns.SingleAsync(x => x.Id == snapshot.FirstCollectionRunId);
        run.StatusCode = 외부데이터수집StatusCodes.Partial;
        run.FetchedCount = fetched;
        run.NormalizedCount = normalized;
        run.InsertedCount = inserted;
        run.ErrorCode = "PrivateReviewSubset";
        run.ErrorSummary = summary;
        await db.SaveChangesAsync();
    }

    private static bool Same(외부데이터정규화Record x, 외부데이터정규화Record y)
        => x.RecordKey == y.RecordKey && x.StableId == y.StableId && x.SourceId == y.SourceId && x.DatasetId == y.DatasetId
           && x.RegionStableId == y.RegionStableId && x.MetricCode == y.MetricCode && x.NumericValue == y.NumericValue
           && x.TextValue == y.TextValue && x.UnitCode == y.UnitCode && x.EvidenceAsOfUtc == y.EvidenceAsOfUtc
           && x.SpatialPrecisionCode == y.SpatialPrecisionCode && x.TemporalPrecisionCode == y.TemporalPrecisionCode
           && x.QualityCode == y.QualityCode && x.LimitationCode == y.LimitationCode && x.DimensionKey == y.DimensionKey
           && x.SourceVersion == y.SourceVersion && x.DataRevision == y.DataRevision;

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }

    private sealed record ReferenceRow(string Id, string Text);
    private sealed record TradeRow(string HsCode, int PartnerCode, decimal PrimaryValue, decimal NetWeight, string Period, int ReporterCode, string FlowCode, string Classification, bool IsReported, bool IsAggregate);
}
