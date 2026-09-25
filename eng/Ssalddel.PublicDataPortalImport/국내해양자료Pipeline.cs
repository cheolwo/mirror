using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

internal static class 국내해양자료Pipeline
{
    private const string Relative = "artifacts/local/public-data/domestic-marine";
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };
    private sealed record Entry(string Id, string SourceId, string DatasetId, string SafeUrl, string MetadataUrl,
        string Month, string HsCode, string Partner, string Status, string ErrorCode, DateTimeOffset CheckedAt,
        string? PayloadHash, string? MetadataHash);
    private sealed record Receipt(string BatchId, Entry[] Entries);

    public static async Task RunAsync(string mode, string root, string[] parameters, Dictionary<string, object?> result)
    {
        if (mode == "acquire")
        {
            Require(parameters.Length == 2, "DomesticMarineMonthAndDateRequired");
            var month = parameters[0]; var date = parameters[1];
            Require(Regex.IsMatch(month, "^[0-9]{6}$") && DateTime.TryParseExact(month, "yyyyMM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var monthValue)
                && monthValue < DateTime.UtcNow.Date, "DomesticMarineMonthInvalid");
            Require(Regex.IsMatch(date, "^[0-9]{8}$") && DateTime.TryParseExact(date, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dayValue)
                && dayValue <= DateTime.UtcNow.Date, "DomesticMarineDateInvalid");
            var batchId = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N")[..8];
            var folder = Path.Combine(root, Relative, batchId);
            Directory.CreateDirectory(folder);
            var entries = new List<Entry>();
            var secretPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft/UserSecrets/47766019-f542-4cfc-9d4a-14b2fbfeac0e/secrets.json");
            using var secrets = JsonDocument.Parse(await File.ReadAllTextAsync(secretPath));
            var key = Uri.UnescapeDataString(secrets.RootElement.GetProperty("PublicData:DataGoKrServiceKey").GetString()!);
            Require(!string.IsNullOrWhiteSpace(key), "DomesticMarineCredentialMissing");
            using var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(25), MaxResponseContentBufferSize = 2 * 1024 * 1024 };
            var jobs = new[] { ("030354", "NO"), ("030214", "NO"), ("030363", "RU") }
                .Select(x => new Entry("kcs-" + x.Item1 + "-" + x.Item2, 국내해양통계RevisionNormalizer.SourceId,
                    국내해양통계RevisionNormalizer.DatasetId,
                    $"https://apis.data.go.kr/1220000/nitemtrade/getNitemtradeList?strtYymm={month}&endYymm={month}&hsSgn={x.Item1}&cntyCd={x.Item2}",
                    "https://www.data.go.kr/catalog/15100475/openapi.json", month, x.Item1, x.Item2, "Pending", "", default, null, null)).ToList();
            jobs.Add(new("khoa-DT_0001", "khoa", "marine-station-temperature-history",
                $"https://apis.data.go.kr/1192136/dtRecent/GetDTRecentApiService?obsCode=DT_0001&reqDate={date}&min=60&type=json&numOfRows=24&pageNo=1",
                "https://www.data.go.kr/catalog/15155508/openapi.json", date, "", "", "Pending", "", default, null, null));
            var metadataCache = new Dictionary<string, byte[]>();
            foreach (var job in jobs)
            {
                var entry = job with { CheckedAt = DateTimeOffset.UtcNow };
                try
                {
                    if (!metadataCache.TryGetValue(job.MetadataUrl, out var metadata))
                    {
                        metadata = await http.GetByteArrayAsync(job.MetadataUrl);
                        using var metadataDoc = JsonDocument.Parse(metadata);
                        var license = metadataDoc.RootElement.GetProperty("license").GetString() ?? "";
                        Require(license.Contains("제한 없음", StringComparison.Ordinal) || license.Contains("제 1유형", StringComparison.Ordinal)
                            || license.Contains("제1유형", StringComparison.Ordinal), "DomesticMarineLicenseReviewRequired");
                        metadataCache.Add(job.MetadataUrl, metadata);
                    }
                    await WriteNew(Path.Combine(folder, job.Id + ".metadata.json"), metadata);
                    entry = entry with { MetadataHash = Hash(metadata) };
                    using var response = await http.GetAsync(job.SafeUrl + "&serviceKey=" + Uri.EscapeDataString(key));
                    var payload = await response.Content.ReadAsByteArrayAsync();
                    var body = Encoding.UTF8.GetString(payload);
                    Require(!new[] { key, Uri.EscapeDataString(key) }.Any(x => body.Contains(x, StringComparison.Ordinal)), "DomesticMarineCredentialEcho");
                    entry = entry with { CheckedAt = DateTimeOffset.UtcNow };
                    if (!response.IsSuccessStatusCode)
                    {
                        var code = body.Contains("SERVICE_KEY_IS_NOT_REGISTERED_ERROR", StringComparison.Ordinal) ? "ServiceKeyNotRegistered"
                            : body.Contains("SERVICE_ACCESS_DENIED_ERROR", StringComparison.Ordinal) ? "ServiceAccessDenied"
                            : "Http" + (int)response.StatusCode;
                        entry = entry with { Status = "Failed", ErrorCode = code };
                    }
                    else if (job.SourceId == "kcs")
                    {
                        await WriteNew(Path.Combine(folder, job.Id + ".raw"), payload);
                        entry = entry with { PayloadHash = Hash(payload) };
                        국내해양통계RevisionNormalizer.Parse(body, month, job.HsCode, job.Partner, entry.CheckedAt);
                        entry = entry with { Status = "Success" };
                    }
                    else
                    {
                        // 승인 전 실제 정상 응답 형식을 추측하지 않는다. 성공 본문은 향후 수온 매핑 검토 대상이다.
                        using var temperatureDoc = JsonDocument.Parse(payload);
                        Require(!temperatureDoc.RootElement.TryGetProperty("OpenAPI_ServiceResponse", out _), "DomesticMarineProviderError");
                        await WriteNew(Path.Combine(folder, job.Id + ".raw"), payload);
                        entry = entry with { Status = "Partial", ErrorCode = "TemperatureMappingReviewRequired", PayloadHash = Hash(payload) };
                    }
                }
                catch (InvalidDataException ex) { entry = entry with { Status = "Failed", ErrorCode = ex.Message }; }
                catch (OperationCanceledException) { entry = entry with { Status = "Failed", ErrorCode = "Timeout" }; }
                catch (Exception) { entry = entry with { Status = "Failed", ErrorCode = "SourceOrSchemaFailure" }; }
                entries.Add(entry);
            }
            await WriteNew(Path.Combine(folder, "receipt.json"), JsonSerializer.SerializeToUtf8Bytes(new Receipt(batchId, entries.ToArray()), Pretty));
            result["batchId"] = batchId;
            result["sources"] = entries.Select(x => new { x.Id, x.Status, x.ErrorCode });
            result["automaticScheduleEnabled"] = false;
            return;
        }
        Require(mode is "apply" or "verify" or "preview" && parameters.Length == 1, "DomesticMarineModeInvalid");
        var id = parameters[0];
        Require(Regex.IsMatch(id, "^[0-9]{8}T[0-9]{6}-[a-f0-9]{8}$"), "DomesticMarineBatchInvalid");
        var batchFolder = Path.Combine(root, Relative, id);
        var receipt = JsonSerializer.Deserialize<Receipt>(await File.ReadAllTextAsync(Path.Combine(batchFolder, "receipt.json")))!;
        Require(receipt.BatchId == id && receipt.Entries.Length == 4 && receipt.Entries.Select(x => x.Id).Distinct().Count() == 4, "DomesticMarineReceiptInvalid");
        var expected = new Dictionary<string, IReadOnlyList<외부데이터정규화Record>>();
        foreach (var entry in receipt.Entries)
        {
            Require(Regex.IsMatch(entry.Id, "^(kcs-(030354|030214|030363)-(NO|RU)|khoa-DT_0001)$")
                && entry.Status is "Success" or "Partial" or "Failed", "DomesticMarineEntryInvalid");
            Require(entry.Id.StartsWith("kcs-", StringComparison.Ordinal)
                ? entry.SourceId == 국내해양통계RevisionNormalizer.SourceId && entry.DatasetId == 국내해양통계RevisionNormalizer.DatasetId
                    && entry.Id == "kcs-" + entry.HsCode + "-" + entry.Partner
                : entry.SourceId == "khoa" && entry.DatasetId == "marine-station-temperature-history", "DomesticMarineReceiptSourceMismatch");
            Require(entry.Status != "Success" || entry.PayloadHash != null && entry.MetadataHash != null, "DomesticMarineSuccessPayloadMissing");
            if (entry.MetadataHash != null) Require(Hash(await File.ReadAllBytesAsync(Path.Combine(batchFolder, entry.Id + ".metadata.json"))) == entry.MetadataHash, "DomesticMarineMetadataHashChanged");
            if (entry.PayloadHash != null)
            {
                var data = await File.ReadAllBytesAsync(Path.Combine(batchFolder, entry.Id + ".raw"));
                Require(Hash(data) == entry.PayloadHash, "DomesticMarinePayloadHashChanged");
                if (entry.Status == "Success") expected.Add(entry.Id, 정규화(data, entry));
            }
        }
        var options = await 로컬공공자료Db.OptionsAsync(root);
        var added = 0; var unchanged = 0;
        if (mode == "apply")
        {
            await using var db = new PublicDataIngestionDbContext(options);
            await db.Database.OpenConnectionAsync();
            await using var gate = db.Database.GetDbConnection().CreateCommand();
            gate.CommandText = "SELECT GET_LOCK('mirror:domestic-marine-history-r1',0)";
            Require(Convert.ToInt32(await gate.ExecuteScalarAsync()) == 1, "DomesticMarineImportBusy");
            try
            {
                foreach (var entry in receipt.Entries)
                {
                    var runKey = "domestic-marine:" + id + ":" + entry.Id;
                    if (await db.IngestionRuns.AnyAsync(x => x.RunKey == runKey)) continue;
                    await using var tx = await db.Database.BeginTransactionAsync();
                    result["databaseWriteAttempted"] = true;
                    var rows = expected.GetValueOrDefault(entry.Id) ?? [];
                    var newCount = 0; var sameCount = 0;
                    if (entry.PayloadHash != null)
                    {
                        var registered = await new 평창군공공공간원본등록Service(db).RegisterFileAsync(Path.Combine(batchFolder, entry.Id + ".raw"),
                            new 공공공간원본등록Request(entry.SourceId, entry.DatasetId, "domestic-marine-source.r1", entry.PayloadHash,
                                rows.FirstOrDefault()?.EvidenceAsOfUtc, entry.SourceId == "kcs" ? "application/xml" : "application/json", Relative + "/" + id + "/" + entry.Id + ".raw"));
                        if (registered.Inserted)
                        {
                            var snapshot = await db.RawSnapshots.SingleAsync(x => x.Id == registered.RawSnapshotId);
                            snapshot.CollectedAtUtc = entry.CheckedAt;
                        }
                        var keys = rows.Select(x => x.RecordKey).ToList();
                        var existing = await db.NormalizedRecords.Where(x => keys.Contains(x.RecordKey)).ToDictionaryAsync(x => x.RecordKey);
                        foreach (var row in rows)
                        {
                            if (existing.TryGetValue(row.RecordKey, out var previous))
                            {
                                Require(국내해양통계RevisionNormalizer.SameVersion(previous, row), "DomesticMarineImmutableConflict");
                                if (row.LastSeenAtUtc > previous.LastSeenAtUtc) previous.LastSeenAtUtc = row.LastSeenAtUtc;
                                sameCount++;
                            }
                            else { row.RawSnapshotId = registered.RawSnapshotId; db.NormalizedRecords.Add(row); newCount++; }
                        }
                    }
                    db.IngestionRuns.Add(new 외부데이터수집Run { RunKey = runKey, SourceId = entry.SourceId, DatasetId = entry.DatasetId,
                        StatusCode = entry.Status, StartedAtUtc = entry.CheckedAt, CompletedAtUtc = entry.CheckedAt, AttemptCount = 1,
                        NormalizedCount = rows.Count, InsertedCount = newCount, ExistingCount = sameCount,
                        SourceVersion = "domestic-marine-source.r1", DataRevision = entry.PayloadHash ?? "", ErrorCode = entry.ErrorCode,
                        ErrorSummary = "PrivateReviewOnly; receipt=" + Relative + "/" + id + "/receipt.json" });
                    await db.SaveChangesAsync();
                    await tx.CommitAsync();
                    added += newCount; unchanged += sameCount;
                }
                result["databaseCommitted"] = true;
            }
            finally { gate.CommandText = "SELECT RELEASE_LOCK('mirror:domestic-marine-history-r1')"; await gate.ExecuteScalarAsync(); }
        }
        await using var readback = new PublicDataIngestionDbContext(options);
        var runKeys = receipt.Entries.Select(x => "domestic-marine:" + id + ":" + x.Id).ToList();
        var runs = await readback.IngestionRuns.AsNoTracking().Where(x => runKeys.Contains(x.RunKey)).ToListAsync();
        Require(runs.Count == 4 && runs.All(x => receipt.Entries.Any(y => x.RunKey.EndsWith(":" + y.Id, StringComparison.Ordinal) && x.StatusCode == y.Status && x.ErrorCode == y.ErrorCode)), "DomesticMarineRunReadbackMismatch");
        var all = expected.Values.SelectMany(x => x).ToList(); var recordKeys = all.Select(x => x.RecordKey).ToList();
        var stored = await readback.NormalizedRecords.AsNoTracking().Where(x => recordKeys.Contains(x.RecordKey)).Include(x => x.RawSnapshot).ToListAsync();
        Require(stored.Count == all.Count && stored.All(x => all.Any(y => 국내해양통계RevisionNormalizer.SameVersion(x, y))
            && x.RawSnapshot?.SourceId == x.SourceId && x.RawSnapshot.DatasetId == x.DatasetId), "DomesticMarineReadbackMismatch");
        var stableIds = all.Select(x => x.StableId).ToList();
        var history = await readback.NormalizedRecords.AsNoTracking().Where(x => stableIds.Contains(x.StableId)).ToListAsync();
        result["verifiedObservations"] = stored.Count; result["verifiedSourceAttempts"] = runs.Count;
        result["insertedVersions"] = added; result["unchangedVersions"] = unchanged;
        result["historyVersions"] = history.Count; result["batchId"] = id;
        result["sourceFailures"] = runs.Where(x => x.StatusCode != "Success").Select(x => new { x.SourceId, x.StatusCode, x.ErrorCode });
        result["latest"] = 국내해양통계RevisionNormalizer.Latest(history).Select(x => new { x.StableId, x.NumericValue, x.UnitCode, x.EvidenceAsOfUtc, x.LastSeenAtUtc, x.DataRevision });
        result["target"] = "hongdal-mysql-1 / hongdal_dev";
        if (mode == "preview")
        {
            Require(stored.Count == 12, "DomesticMarinePreviewSampleIncomplete");
            var items = stored.GroupBy(x =>
                {
                    using var facts = JsonDocument.Parse(x.TextValue!);
                    return facts.RootElement.GetProperty("hsCode").GetString()!;
                })
                .OrderBy(x => x.Key, StringComparer.Ordinal).Select(group =>
                {
                    var weight = group.Single(x => x.MetricCode == "impWgt");
                    var amount = group.Single(x => x.MetricCode == "impDlr");
                    using var facts = JsonDocument.Parse(weight.TextValue!);
                    var partner = facts.RootElement.GetProperty("partner").GetString();
                    return new { hsCode = group.Key, name = facts.RootElement.GetProperty("name").GetString(), partner,
                        importKg = weight.NumericValue, importUsd = amount.NumericValue,
                        weightRevision = weight.DataRevision, amountRevision = amount.DataRevision };
                }).ToArray();
            var snapshot = new { schemaVersion = "marine-trade-preview.v1", localReviewOnly = true,
                distributionApproved = false, country = "KR", sourceId = "kcs", month = receipt.Entries.First(x => x.SourceId == "kcs").Month,
                batchId = id, collectedAtUtc = stored.Max(x => x.CollectedAtUtc).ToString("O"),
                sourceUrl = "https://www.data.go.kr/data/15100475/openapi.do", temperatureAvailable = false, items };
            var file = Path.Combine(batchFolder, "globe-preview.json");
            var bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot, Pretty);
            if (File.Exists(file)) Require(Hash(await File.ReadAllBytesAsync(file)) == Hash(bytes), "DomesticMarinePreviewConflict");
            else await WriteNew(file, bytes);
            result["previewPath"] = file; result["previewHash"] = Hash(bytes);
        }
    }

    private static IReadOnlyList<외부데이터정규화Record> 정규화(byte[] data, Entry entry)
        => 국내해양통계RevisionNormalizer.Parse(Encoding.UTF8.GetString(data), entry.Month, entry.HsCode, entry.Partner, entry.CheckedAt);
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static async Task WriteNew(string path, byte[] bytes)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await stream.WriteAsync(bytes);
    }
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidDataException(code); }
}
