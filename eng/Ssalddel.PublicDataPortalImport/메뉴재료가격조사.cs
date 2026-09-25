using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

// 별도 업무 원장/시세 서비스가 아니다. 기존 수집 저장소에 검토용 사본만 추가한다.
internal static class 메뉴재료가격조사
{
    const string Relative = "artifacts/local/public-data/menu-ingredient-prices/20260924-r1";
    const string ScopePath = "eng/public-data/menu-ingredient-prices/scope.r1.json";
    const string Dataset = "menu-ingredient-prices-20260924-r1";
    const string TradeEndpoint = "https://comtradeapi.un.org/public/v1/preview/C/A/HS";
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    internal sealed record Ingredient(string Id, string Name, string Basis, string[] Hs6, string[] KamisCodes, string Constraint);
    internal sealed record FileReceipt(string Name, string Kind, string Source, string Url, string Sha256, DateTimeOffset Collected, string Status, string Context);
    internal sealed record TradeObservation(string Hs6, string Direction, decimal? Value, decimal? Weight, string Description, string Lineage, bool? EstimatedWeight);
    internal sealed record Batch(FileReceipt File, List<외부데이터정규화Record> Rows);

    public static async Task RunAsync(string mode, string root, Dictionary<string, object?> result)
    {
        Require(mode is "acquire" or "retry-exports" or "preview" or "apply" or "verify" or "self-test", "MenuPriceModeInvalid");
        if (mode == "self-test") { result["selfTestsPassed"] = SelfTest(); return; }
        var scopeBytes = await File.ReadAllBytesAsync(Path.Combine(root, ScopePath));
        using var scope = JsonDocument.Parse(scopeBytes);
        var ingredients = scope.RootElement.GetProperty("ingredients").Deserialize<Ingredient[]>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Require(ingredients.Length == 12 && ingredients.Select(x => x.Id).Distinct().Count() == 12, "ScopeChanged");
        var codes = ingredients.SelectMany(x => x.Hs6).Order().ToArray();
        Require(codes.Length == 16 && codes.Distinct().Count() == 16, "HsScopeChanged");
        var folder = Path.Combine(root, Relative);
        if (mode is "acquire" or "retry-exports") { await Acquire(root, folder, scopeBytes, codes, result, mode == "retry-exports"); return; }
        var receipts = JsonSerializer.Deserialize<FileReceipt[]>(await File.ReadAllTextAsync(Path.Combine(folder, "receipt.json")), Json)!;
        Require(receipts.Length > 0 && receipts.Select(x => x.Name).Distinct().Count() == receipts.Length, "ReceiptInvalid");
        var batches = new List<Batch>();
        foreach (var file in receipts)
        {
            result["stage"] = "parse:" + file.Name;
            Require(Path.GetFileName(file.Name) == file.Name, "ReceiptPathRejected");
            var bytes = await File.ReadAllBytesAsync(Path.Combine(folder, file.Name));
            Require(Hash(bytes) == file.Sha256, "InputHashMismatch");
            var rows = new List<외부데이터정규화Record>();
            if (file.Kind == "scope")
            {
                Require(bytes.SequenceEqual(scopeBytes), "ScopeHashMismatch");
                foreach (var item in ingredients)
                    rows.Add(Row(file, "ingredient.hs.candidate", item.Id, null, "candidate", new DateTimeOffset(2026, 9, 24, 0, 0, 0, TimeSpan.Zero),
                        JsonSerializer.Serialize(item, Json), "country:kr", "date-only"));
            }
            else if (file.Status == "Success" && file.Kind == "imports")
            {
                var observations = JsonSerializer.Deserialize<TradeObservation[]>(bytes, Json)!;
                foreach (var observation in observations) rows.AddRange(TradeRows(file, observation));
            }
            else if (file.Status == "Success" && file.Kind == "exports")
            {
                using var doc = JsonDocument.Parse(bytes);
                var observations = ParseExports(doc.RootElement, file.Context.Split(','));
                foreach (var observation in observations) rows.AddRange(TradeRows(file, observation));
                foreach (var missing in file.Context.Split(',').Except(observations.Select(x => x.Hs6)))
                    rows.Add(Row(file, "trade.export.coverage", $"hs6={missing};flow=X;period=2025", null, "status", Year(), "NoReturnedRecord;NotZeroTrade", "country:kr", "year"));
            }
            else if (file.Status == "Success" && file.Kind == "kamis") rows.AddRange(ParseKamis(file, Encoding.UTF8.GetString(bytes), ingredients));
            if (rows.Count == 0)
                rows.Add(Row(file, "research.source.status", file.Name, null, "status", file.Collected, file.Status == "Success" ? "NoSelectedRows" : file.Status, "country:kr", "collection-time-only"));
            Require(rows.Select(x => x.RecordKey).Distinct().Count() == rows.Count, "DuplicateObservation");
            batches.Add(new(file, rows));
        }
        var all = batches.SelectMany(x => x.Rows).ToList();
        Require(all.Select(x => x.RecordKey).Distinct().Count() == all.Count, "CrossSourceCollision");
        // .NET 10 배열 Contains의 Span 오버로드가 EF 식 트리에 들어가지 않게 List를 사용한다.
        var keys = all.Select(x => x.RecordKey).ToList();
        result["stage"] = "local-db-options";
        var options = await 로컬공공자료Db.OptionsAsync(root);
        await using (var db = new PublicDataIngestionDbContext(options))
        {
            if (mode == "apply")
            {
                await db.Database.OpenConnectionAsync();
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT GET_LOCK('mirror:menu-ingredient-prices:r1',0)";
                Require(Convert.ToInt32(await command.ExecuteScalarAsync()) == 1, "MenuPriceImportBusy");
                await using var transaction = await db.Database.BeginTransactionAsync();
                var existing = await db.NormalizedRecords.AsNoTracking().Where(x => keys.Contains(x.RecordKey)).ToListAsync();
                Require(existing.All(x => all.Any(y => Same(x, y))), "ExistingObservationConflict");
                result["databaseWriteAttempted"] = true;
                var inserted = 0; var unchanged = 0;
                foreach (var batch in batches)
                {
                    var registered = await new 평창군공공공간원본등록Service(db).RegisterFileAsync(Path.Combine(folder, batch.File.Name),
                        new 공공공간원본등록Request(batch.File.Source, Dataset, batch.File.Kind + ":" + batch.File.Context, batch.File.Sha256,
                            batch.Rows[0].EvidenceAsOfUtc, "application/json", Relative + "/" + batch.File.Name));
                    foreach (var row in batch.Rows) row.RawSnapshotId = registered.RawSnapshotId;
                    var saved = await new EfExternalDataIngestionStore(db).UpsertNormalizedAsync(batch.Rows);
                    Require(saved.UpdatedCount == 0, "UnexpectedObservationUpdate");
                    inserted += saved.InsertedCount; unchanged += saved.ExistingCount;
                    if (registered.Inserted)
                    {
                        var raw = await db.RawSnapshots.SingleAsync(x => x.Id == registered.RawSnapshotId);
                        raw.CollectedAtUtc = batch.File.Collected;
                        var run = await db.IngestionRuns.SingleAsync(x => x.Id == raw.FirstCollectionRunId);
                        run.StatusCode = 외부데이터수집StatusCodes.Partial;
                        run.FetchedCount = batch.Rows.Count; run.NormalizedCount = batch.Rows.Count; run.InsertedCount = saved.InsertedCount;
                        run.ErrorCode = "PendingHumanReview";
                        run.ErrorSummary = "Private research only; no publication, restaurant cost, customs decision or runtime. Receipt: " + Relative + "/receipt.json";
                        await db.SaveChangesAsync();
                    }
                }
                await transaction.CommitAsync();
                result["committed"] = true; result["inserted"] = inserted; result["unchanged"] = unchanged;
            }
        }
        // 새 context에서 값·단위·판본·원본 hash까지 독립 재조회한다.
        result["stage"] = "independent-readback";
        await using var verify = new PublicDataIngestionDbContext(options);
        var stored = await verify.NormalizedRecords.AsNoTracking().Include(x => x.RawSnapshot).Where(x => keys.Contains(x.RecordKey)).ToListAsync();
        Require(mode == "preview" || stored.Count == all.Count, "ReadbackCountMismatch");
        Require(stored.All(x => all.Any(y => Same(x, y)) && x.RawSnapshot != null && batches.Any(b => b.Rows.Any(y => y.RecordKey == x.RecordKey) && b.File.Sha256 == x.RawSnapshot.ContentHashSha256)), "ReadbackMismatch");
        result["mode"] = mode; result["selectedRows"] = all.Count; result["verifiedRows"] = stored.Count;
        result["byMetric"] = all.GroupBy(x => x.MetricCode).ToDictionary(x => x.Key, x => x.Count());
        result["target"] = "hongdal-mysql-1 / hongdal_dev";
        result["receiptSha256"] = Hash(await File.ReadAllBytesAsync(Path.Combine(folder, "receipt.json")));
        result["stage"] = "completed";
        await File.WriteAllTextAsync(Path.Combine(folder, "observations.json"), JsonSerializer.Serialize(all.Select(x => new {
            x.RecordKey, x.SourceId, x.MetricCode, x.NumericValue, x.UnitCode, x.TextValue, x.DimensionKey, x.EvidenceAsOfUtc, x.CollectedAtUtc, x.QualityCode, x.DataRevision
        }), Json), new UTF8Encoding(false));
    }

    static async Task Acquire(string root, string folder, byte[] scopeBytes, string[] codes, Dictionary<string, object?> result, bool retryExports)
    {
        Directory.CreateDirectory(folder);
        var receiptPath = Path.Combine(folder, "receipt.json");
        var receipts = File.Exists(receiptPath) ? JsonSerializer.Deserialize<List<FileReceipt>>(await File.ReadAllTextAsync(receiptPath), Json)! : new List<FileReceipt>();
        foreach (var previous in receipts)
            Require(Path.GetFileName(previous.Name) == previous.Name && Hash(await File.ReadAllBytesAsync(Path.Combine(folder, previous.Name))) == previous.Sha256, "ResumeHashMismatch");
        async Task Save(string name, string kind, string source, string url, byte[] bytes, string status, string context)
        {
            var previous = receipts.SingleOrDefault(x => x.Name == name);
            if (previous != null) { Require(previous.Sha256 == Hash(bytes) && previous.Kind == kind && previous.Context == context, "ResumeContentChanged"); return; }
            await using (var file = new FileStream(Path.Combine(folder, name), FileMode.CreateNew)) await file.WriteAsync(bytes);
            var now = DateTimeOffset.UtcNow;
            receipts.Add(new(name, kind, source, url, Hash(bytes), new DateTimeOffset(now.Ticks - now.Ticks % 10, TimeSpan.Zero), status, context));
            await File.WriteAllTextAsync(Path.Combine(folder, "receipt.json"), JsonSerializer.Serialize(receipts, Json), new UTF8Encoding(false));
        }
        await Save("scope.json", "scope", "manual-menu-ingredient-research", ScopePath, scopeBytes, "Success", "CandidateOnly");
        // 수입 통계와 HS 이름은 이미 저장된 공식 사본 재사용. 판본이 다른 두 수치를 임의 결합하지 않는다.
        var options = await 로컬공공자료Db.OptionsAsync(root);
        await using (var db = new PublicDataIngestionDbContext(options))
        {
            var records = await db.NormalizedRecords.AsNoTracking().Include(x => x.RawSnapshot)
                .Where(x => x.SourceId == "un-comtrade-public" && x.DimensionKey.Contains("partnerM49=0;") && x.DimensionKey.Contains("period=2025;") && x.DimensionKey.Contains("classification=H6")
                    && (x.MetricCode == "trade.import.value.usd" || x.MetricCode == "trade.import.net-weight.kg")).ToListAsync();
            var references = await db.NormalizedRecords.AsNoTracking().Where(x => x.SourceId == "un-comtrade-reference" && x.MetricCode == "trade.hs6.category").ToListAsync();
            var statuses = await db.NormalizedRecords.AsNoTracking().Include(x => x.RawSnapshot).Where(x => x.SourceId == "un-comtrade-public" && x.MetricCode == "trade.import.observation-status").ToListAsync();
            var observations = new List<TradeObservation>();
            foreach (var code in codes)
            {
                bool Match(외부데이터정규화Record x) => x.DimensionKey.Split(';').Contains("hs6=" + code);
                var candidates = records.Where(Match).GroupBy(x => (x.DatasetId, x.DimensionKey, x.DataRevision, x.RawSnapshotId))
                    .Select(g => new { Value = g.SingleOrDefault(x => x.MetricCode == "trade.import.value.usd"), Weight = g.SingleOrDefault(x => x.MetricCode == "trade.import.net-weight.kg") })
                    .Where(x => x.Value != null && x.Weight != null).OrderBy(x => x.Value!.DatasetId, StringComparer.Ordinal).ToArray();
                var description = references.Where(Match).Select(x => x.TextValue).Distinct().FirstOrDefault();
                Require(!string.IsNullOrWhiteSpace(description), "HsDescriptionMissing:" + code);
                if (candidates.Length == 0)
                {
                    var missing = statuses.Where(Match).FirstOrDefault(x => x.TextValue == "NoReportedRows" && x.DimensionKey.Contains("period=2025;classification=H6"));
                    Require(missing != null && missing.RawSnapshot != null, "ExistingImportMissing:" + code);
                    observations.Add(new(code, "M", null, null, description!, JsonSerializer.Serialize(new { status = "NoReportedRows;NotZeroTrade", missing!.RecordKey, sourceHash = missing.RawSnapshot!.ContentHashSha256 }), null));
                    continue;
                }
                Require(candidates.All(x => x.Value!.NumericValue == candidates[0].Value!.NumericValue && x.Weight!.NumericValue == candidates[0].Weight!.NumericValue), "ImportVersionsDisagree:" + code);
                var chosen = candidates[0]; var value = chosen.Value!; var weight = chosen.Weight!;
                Require(value.UnitCode == "USD" && weight.UnitCode == "kg" && value.RawSnapshot != null, "ImportUnitsInvalid");
                observations.Add(new(code, "M", value.NumericValue, weight.NumericValue, description!,
                    JsonSerializer.Serialize(new { valueRecordKey = value.RecordKey, weightRecordKey = weight.RecordKey, value.DatasetId, value.DataRevision,
                        sourceHash = value.RawSnapshot!.ContentHashSha256, originalCollectedAtUtc = value.CollectedAtUtc, weightEstimateFlag = "NotRetainedByLegacyProjection" }), null));
            }
            await Save("existing-imports.json", "imports", "un-comtrade-local-snapshot", "local:hongdal_dev/public_data_normalized_records", JsonSerializer.SerializeToUtf8Bytes(observations, Json), "Success", "2025;H6;M;World");
        }
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(40) };
        int index = 0;
        foreach (var chunk in codes.Chunk(5))
        {
            var context = string.Join(',', chunk);
            var url = QueryHelpers.AddQueryString(TradeEndpoint, new Dictionary<string, string?> {
                ["period"]="2025", ["reporterCode"]="410", ["cmdCode"]=context, ["flowCode"]="X", ["partnerCode"]="0",
                ["partner2Code"]="0", ["customsCode"]="C00", ["motCode"]="0", ["maxRecords"]="500", ["clCode"]="H6" });
            var name = "exports-" + (++index) + ".json";
            if (retryExports && receipts.Any(x => x.Name == name && x.Status != "Success")) name = $"exports-{index}-retry1.json";
            if (receipts.Any(x => x.Name == name)) continue;
            try
            {
                var bytes = await Get(client, url);
                using var doc = JsonDocument.Parse(bytes);
                _ = ParseExports(doc.RootElement, chunk);
                await Save(name, "exports", "un-comtrade-public", url, bytes, "Success", context);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidDataException)
            {
                // 원문 예외/응답 URL을 로그나 진단에 복사하지 않는다.
                await Save(name, "exports", "un-comtrade-public", url, "{}"u8.ToArray(), ex is InvalidDataException ? ex.Message : ex.GetType().Name, context);
            }
            await Task.Delay(1500);
        }
        string key = "", requester = "";
        try
        {
            using var secrets = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft/UserSecrets/47766019-f542-4cfc-9d4a-14b2fbfeac0e/secrets.json")));
            key = secrets.RootElement.GetProperty("PublicData:Kamis:CertificationKey").GetString()!;
            requester = secrets.RootElement.GetProperty("PublicData:Kamis:RequesterId").GetString()!;
            Require(!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(requester), "CredentialUnavailable");
        }
        catch (Exception ex) when (ex is IOException or KeyNotFoundException or JsonException or InvalidDataException)
        {
            await Save("kamis-unavailable.json", "kamis", "at-kamis", 먹거리가격표본Acquisition.Documentation, "{}"u8.ToArray(), "CredentialUnavailable", "");
        }
        if (key.Length > 0 && requester.Length > 0)
        foreach (var category in new[] { "100", "200", "500" })
        foreach (var cls in new[] { "01", "02" })
        {
            if (receipts.Any(x => x.Name == $"kamis-{category}-{cls}.json")) continue;
            var query = new Dictionary<string, string?> { ["action"]="dailyPriceByCategoryList", ["p_product_cls_code"]=cls, ["p_item_category_code"]=category,
                ["p_country_code"]="1101", ["p_regday"]="2026-09-23", ["p_convert_kg_yn"]="N", ["p_returntype"]="json" };
            var safeUrl = QueryHelpers.AddQueryString(먹거리가격표본Acquisition.Endpoint, query);
            query["p_cert_key"] = key; query["p_cert_id"] = requester;
            var context = $"class={cls};category={category};region=1101;date=2026-09-23;convert=N";
            try
            {
                var raw = await Get(client, QueryHelpers.AddQueryString(먹거리가격표본Acquisition.Endpoint, query));
                var bytes = 먹거리가격표본Acquisition.PreserveData(Encoding.UTF8.GetString(raw), key, requester);
                using var doc = JsonDocument.Parse(bytes);
                var code = doc.RootElement.GetProperty("data").GetProperty("error_code").GetString();
                await Save($"kamis-{category}-{cls}.json", "kamis", "at-kamis", safeUrl, bytes, code == "000" ? "Success" : "ProviderCode:" + code, context);
                if (code == "900") break;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidDataException or InvalidOperationException or KeyNotFoundException)
            {
                await Save($"kamis-{category}-{cls}.json", "kamis", "at-kamis", safeUrl, "{}"u8.ToArray(), ex.GetType().Name, context);
            }
        }
        result["folder"] = Relative; result["acquiredFiles"] = receipts.Count;
        result["sources"] = receipts.Select(x => new { x.Name, x.Status });
    }

    static async Task<byte[]> Get(HttpClient client, string url)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        Require(response.IsSuccessStatusCode, "SourceHttpFailure:" + (int)response.StatusCode);
        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var output = new MemoryStream(); var buffer = new byte[8192]; int n;
        while ((n = await stream.ReadAsync(buffer, timeout.Token)) > 0)
        { Require(output.Length + n <= 4 * 1024 * 1024, "ResponseTooLarge"); output.Write(buffer, 0, n); }
        return output.ToArray();
    }

    internal static List<TradeObservation> ParseExports(JsonElement root, string[] codes)
    {
        var data = root.GetProperty("data");
        Require(data.ValueKind == JsonValueKind.Array && data.GetArrayLength() < 500 && root.GetProperty("count").GetInt32() == data.GetArrayLength(), "TradeTruncatedOrInvalid");
        var seen = new HashSet<string>(); var result = new List<TradeObservation>();
        foreach (var row in data.EnumerateArray())
        {
            var code = row.GetProperty("cmdCode").GetString()!;
            Require(codes.Contains(code) && seen.Add(code) && row.GetProperty("period").GetString() == "2025" && row.GetProperty("reporterCode").GetInt32() == 410
                && row.GetProperty("partnerCode").GetInt32() == 0 && row.GetProperty("partner2Code").GetInt32() == 0 && row.GetProperty("flowCode").GetString() == "X"
                && row.GetProperty("classificationCode").GetString() == "H6" && row.GetProperty("customsCode").GetString() == "C00" && row.GetProperty("motCode").GetInt32() == 0, "TradeScopeMismatch");
            decimal? Read(string name) => row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDecimal() : null;
            var value = Read("primaryValue"); var weight = Read("netWgt");
            Require(value >= 0 && (weight is null || weight >= 0), "TradeMeasureInvalid");
            result.Add(new(code, "X", value, weight, row.GetProperty("cmdDesc").GetString()!,
                JsonSerializer.Serialize(new { cifvalue = Read("cifvalue"), fobvalue = Read("fobvalue"), isReported = row.GetProperty("isReported").GetBoolean(), isAggregate = row.GetProperty("isAggregate").GetBoolean() }),
                row.TryGetProperty("isNetWgtEstimated", out var estimated) && estimated.GetBoolean()));
        }
        return result;
    }

    static IEnumerable<외부데이터정규화Record> TradeRows(FileReceipt file, TradeObservation trade)
    {
        var direction = trade.Direction == "M" ? "import" : "export";
        var dimension = $"hs6={trade.Hs6};flow={trade.Direction};period=2025;reporter=410;partner=0;classification=H6";
        var text = JsonSerializer.Serialize(new { trade.Description, trade.Lineage, trade.EstimatedWeight, unitValueMeaning = "StatisticalValueDividedByNetWeight_NotMarketPrice", lowVolume = trade.Weight < 1000 }, Json);
        if (trade.Value == null)
        {
            yield return Row(file, $"trade.{direction}.coverage", dimension, null, "status", Year(), text, "country:kr", "year");
            yield break;
        }
        yield return Row(file, $"trade.{direction}.value.usd", dimension, trade.Value, "USD", Year(), text, "country:kr", "year");
        yield return Row(file, $"trade.{direction}.net-weight.kg", dimension, trade.Weight, "kg", Year(), text, "country:kr", "year");
        yield return Row(file, $"trade.{direction}.unit-value.usd-kg", dimension, UnitValue(trade.Value, trade.Weight), "USD/kg", Year(), text, "country:kr", "year");
    }
    internal static decimal? UnitValue(decimal? value, decimal? weight) => value >= 0 && weight > 0 ? decimal.Round(value.Value / weight.Value, 10) : null;

    static List<외부데이터정규화Record> ParseKamis(FileReceipt file, string json, Ingredient[] ingredients)
    {
        using var doc = JsonDocument.Parse(json);
        Require(doc.RootElement.EnumerateObject().Count() == 1 && doc.RootElement.TryGetProperty("data", out _), "KamisSanitizedDataRequired");
        var data = doc.RootElement.GetProperty("data");
        Require(data.GetProperty("error_code").GetString() == "000", "KamisNotSuccess");
        var rows = new List<외부데이터정규화Record>();
        foreach (var item in data.GetProperty("item").EnumerateArray())
        {
            string Field(string name) => item.GetProperty(name).GetString()!;
            var ingredient = ingredients.SingleOrDefault(x => x.KamisCodes.Contains(Field("item_code")));
            if (ingredient == null) continue;
            var identity = file.Context + $";ingredient={ingredient.Id};item={Field("item_code")};kind={Field("kind_code")};rank={Field("rank_code")}";
            // day1이 요청일과 다르면 그 날짜를 억지로 붙이지 않는다.
            Require(Field("day1") == "당일 (09/23)", "KamisSurveyDateMismatch");
            var unit = Field("unit"); Require(!string.IsNullOrWhiteSpace(unit), "KamisUnitMissing");
            var raw = Field("dpr1");
            decimal? price = raw is "-" or "" ? null : decimal.Parse(raw, NumberStyles.Number, CultureInfo.InvariantCulture);
            Require(price is null || price >= 0, "KamisPriceInvalid");
            var priceStatus = price == 0 ? "SourceZeroPrice;UnavailableForComparison;NotFree" : price == null ? "MissingPrice" : "ObservedQuote";
            if (price == 0) price = null;
            var livestock = file.Context.Contains("category=500;");
            var text = JsonSerializer.Serialize(new { ingredientId = ingredient.Id, name = Field("item_name"), kind = Field("kind_name"), rank = Field("rank"),
                rawPrice = raw, sourceUnit = unit, day1 = Field("day1"), requestedRegion = "1101", actualCoverage = livestock ? "ProviderLivestockCoverage;SeoulFilterNotVerified" : "SeoulRequested", priceStatus,
                normalization = "None;OriginalSurveyUnit;NoKgConversion" }, Json);
            rows.Add(Row(file, "domestic.kamis.price.krw", identity, price, "KRW/" + unit, new DateTimeOffset(2026, 9, 23, 0, 0, 0, TimeSpan.Zero), text, livestock ? "country:kr" : "region:kr:seoul", "date-only"));
        }
        if (file.Context.Contains("category=500;"))
            rows.Add(Row(file, "research.source.caveat", file.Context, null, "interpretation", new DateTimeOffset(2026, 9, 23, 0, 0, 0, TimeSpan.Zero),
                "KAMIS 축산물 도매가격은 조사 비대상이라는 공식 안내가 있다. 이번 01/02 응답이 동일하므로 class=02 요청을 도매가격으로 해석하거나 두 응답을 독립 가격으로 평균내지 않는다. 축산물은 01 사본만 참고하고 서울 지역 한정 여부도 미확인으로 둔다. https://kamis.or.kr/customer/trend/economic/interest.do",
                "country:kr", "date-only"));
        return rows;
    }

    static 외부데이터정규화Record Row(FileReceipt file, string metric, string dimension, decimal? value, string unit, DateTimeOffset evidence, string text, string region, string temporal)
    {
        // 파일명으로 조회 조건의 충돌을 막고 내용 hash를 판본으로 고정한다. 같은 입력은 불변이다.
        var identity = file.Name + ";" + dimension;
        Require(text.Length <= 2000 && identity.Length <= 500 && unit.Length <= 80, "ObservationFieldTooLong");
        var key = 외부데이터RecordKey.Create(file.Source, Dataset, region, metric, evidence, identity);
        return new() { RecordKey = key, StableId = "menu-price:" + key, SourceId = file.Source, DatasetId = Dataset,
            RegionStableId = region, MetricCode = metric, NumericValue = value, UnitCode = unit, TextValue = text,
            EvidenceAsOfUtc = evidence, CollectedAtUtc = file.Collected, FirstSeenAtUtc = file.Collected, LastSeenAtUtc = file.Collected,
            TemporalPrecisionCode = temporal, SpatialPrecisionCode = region == "country:kr" ? "country" : "requested-city",
            QualityCode = "PendingHumanReview", LimitationCode = "PrivateReviewOnly;NoPublication;NotRestaurantCost;NoCustomsDecision;NoAutomaticCurrencyConversion;MayBeRevised",
            DimensionKey = identity, SourceVersion = file.Kind + ":" + file.Context, DataRevision = file.Sha256 };
    }
    static bool Same(외부데이터정규화Record a, 외부데이터정규화Record b) => a.RecordKey == b.RecordKey && a.StableId == b.StableId && a.SourceId == b.SourceId && a.DatasetId == b.DatasetId
        && a.RegionStableId == b.RegionStableId && a.MetricCode == b.MetricCode && a.NumericValue == b.NumericValue && a.UnitCode == b.UnitCode && a.TextValue == b.TextValue
        && a.DimensionKey == b.DimensionKey && a.SourceVersion == b.SourceVersion && a.DataRevision == b.DataRevision && a.QualityCode == b.QualityCode
        && a.LimitationCode == b.LimitationCode && a.EvidenceAsOfUtc == b.EvidenceAsOfUtc && a.CollectedAtUtc == b.CollectedAtUtc
        && a.TemporalPrecisionCode == b.TemporalPrecisionCode && a.SpatialPrecisionCode == b.SpatialPrecisionCode;
    static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    static DateTimeOffset Year() => new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
    static void Require(bool condition, string code) { if (!condition) throw new InvalidDataException(code); }
    static int SelfTest()
    {
        Require(UnitValue(10, 2) == 5 && UnitValue(10, 0) == null && UnitValue(10, null) == null && UnitValue(null, 2) == null && UnitValue(-1, 2) == null, "UnitValueTest");
        var file = new FileReceipt("sample.json", "exports", "un-comtrade-public", TradeEndpoint, new string('a',64), Year(), "Success", "100630");
        var a = TradeRows(file, new("100630", "X", 10, 2, "Rice", "fixture", false)).ToArray();
        var b = TradeRows(file, new("100630", "M", 10, 2, "Rice", "fixture", false)).ToArray();
        Require(a.Select(x => x.RecordKey).Intersect(b.Select(x => x.RecordKey)).Count() == 0, "FlowCollisionTest");
        Require(a.Zip(TradeRows(file, new("100630", "X", 10, 2, "Rice", "fixture", false))).All(x => Same(x.First,x.Second)), "DeterminismTest");
        var changed = TradeRows(file with { Sha256 = new string('b',64) }, new("100630", "X", 10, 2, "Rice", "fixture", false)).ToArray();
        Require(!Same(a[0], changed[0]), "HashChangeTest");
        Require(TradeRows(file, new("100630", "X", 10, null, "Rice", "fixture", false)).Last().NumericValue == null, "MissingWeightTest");
        var ingredient = new Ingredient("potato", "감자", "fixture", ["070190"], ["152"], "CandidateOnly");
        var kamis = file with { Name = "kamis-100-01.json", Kind = "kamis", Source = "at-kamis", Context = "class=01;category=100;region=1101;date=2026-09-23;convert=N" };
        const string item = "{\"item_code\":\"152\",\"item_name\":\"감자\",\"kind_code\":\"01\",\"kind_name\":\"수미\",\"rank_code\":\"04\",\"rank\":\"상품\",\"unit\":\"100g\",\"day1\":\"당일 (09/23)\",\"dpr1\":\"263\"}";
        var fixture = "{\"data\":{\"error_code\":\"000\",\"item\":[" + item + "]}}";
        var quote = ParseKamis(kamis, fixture, [ingredient]).Single();
        Require(quote.NumericValue == 263 && quote.UnitCode == "KRW/100g", "OriginalUnitTest");
        Require(ParseKamis(kamis, fixture.Replace("263", "0"), [ingredient]).Single().NumericValue == null, "ZeroIsNotFreeTest");
        Require(ParseKamis(kamis, fixture.Replace("263", "-"), [ingredient]).Single().NumericValue == null, "MissingPriceTest");
        Require(ParseKamis(kamis with { Context = kamis.Context.Replace("class=01", "class=02") }, fixture, [ingredient]).Single().RecordKey != quote.RecordKey, "PriceClassCollisionTest");
        foreach (var invalid in new[] { fixture.Replace("09/23", "09/22"), fixture.Replace("263", "-1"), fixture.Replace("100g", ""), fixture.Replace("000", "900") })
        {
            var rejected = false; try { ParseKamis(kamis, invalid, [ingredient]); } catch (InvalidDataException) { rejected = true; }
            Require(rejected, "KamisNegativeTest");
        }
        const string tradeItem = "{\"cmdCode\":\"100630\",\"period\":\"2025\",\"reporterCode\":410,\"partnerCode\":0,\"partner2Code\":0,\"flowCode\":\"X\",\"classificationCode\":\"H6\",\"customsCode\":\"C00\",\"motCode\":0,\"primaryValue\":10,\"netWgt\":2,\"cmdDesc\":null,\"isReported\":false,\"isAggregate\":true,\"isNetWgtEstimated\":true}";
        var tradeFixture = "{\"count\":1,\"data\":[" + tradeItem + "]}";
        using (var doc = JsonDocument.Parse(tradeFixture)) Require(ParseExports(doc.RootElement, ["100630"]).Single().EstimatedWeight == true, "EstimatedWeightPreservedTest");
        foreach (var invalid in new[] { tradeFixture.Replace("H6", "H5"), tradeFixture.Replace("2025", "2024"), tradeFixture.Replace("410", "156"),
            "{\"count\":2,\"data\":[" + tradeItem + "," + tradeItem + "]}", tradeFixture.Replace("\"count\":1", "\"count\":500") })
        {
            using var doc = JsonDocument.Parse(invalid); var rejected = false;
            try { ParseExports(doc.RootElement, ["100630"]); } catch (InvalidDataException) { rejected = true; }
            Require(rejected, "TradeNegativeTest");
        }
        return 23 + 먹거리가격표본Acquisition.SelfTest();
    }
}
