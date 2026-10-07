using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

// 이번 공식 취득 파일과 receipt의 계보만 저장한다. HTTP host/migration/current/Unity를 시작하지 않는다.
var result = new Dictionary<string, object?>
{
    ["databaseTarget"] = "hongdal-mysql-1 / 127.0.0.1:13306 / hongdal_dev",
    ["databaseWriteAttempted"] = false, ["committed"] = false,
    ["reviewState"] = "PendingHumanReview", ["distributionApproved"] = false,
    ["normalizedDataProduced"] = false, ["unityApplied"] = false,
};
var locks = new List<FileStream>();
try
{
    if (args.Length == 1 && args[0] == "self-test")
    {
        result["selfTestsPassed"] = CollectionGuards.SelfTest();
        Console.WriteLine(JsonSerializer.Serialize(result)); return 0;
    }
    CollectionGuards.Require(args.Length == 4 && args[0] is "preview" or "apply" or "verify", "CollectionArgumentsInvalid");
    var mode = args[0]; var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1]));
    var manifestPath = CollectionGuards.SafePath(root, args[2]);
    var manifestBytes = CollectionGuards.LockAndHash(manifestPath, args[3], null, locks);
    var manifest = CollectionGuards.Read<CollectionManifest>(manifestBytes);
    CollectionGuards.Validate(manifest);
    result["mode"] = mode; result["batchId"] = manifest.BatchId; result["revision"] = manifest.Revision;
    result["manifestSha256"] = CollectionGuards.Hash(manifestBytes);
    var inputs = new List<RegistrationInput>();
    foreach (var record in manifest.Records)
    {
        var payloadPath = CollectionGuards.SafePath(root, record.File.Path);
        var receiptPath = CollectionGuards.SafePath(root, record.Receipt.Path);
        _ = CollectionGuards.LockAndHash(payloadPath, record.File.Sha256, record.File.Bytes, locks);
        var receiptBytes = CollectionGuards.LockAndHash(receiptPath, record.Receipt.Sha256, null, locks);
        CollectionGuards.Require(receiptBytes.Length <= 256 * 1024, "CollectionReceiptTooLarge");
        var receipt = CollectionGuards.Read<AcquisitionReceipt>(receiptBytes);
        CollectionGuards.ValidateReceipt(receipt, record);
        if (!string.IsNullOrEmpty(receipt.OriginalReceiptPath))
            _ = CollectionGuards.LockAndHash(CollectionGuards.SafePath(root, receipt.OriginalReceiptPath),
                receipt.OriginalReceiptSha256, null, locks);
        inputs.Add(new(record, payloadPath, record.File.Path, record.DatasetId, record.File.Sha256,
            record.File.Bytes, record.File.ContentType, record.PayloadKind, record.DataRevision));
        inputs.Add(new(record, receiptPath, record.Receipt.Path, record.DatasetId + ":receipt", record.Receipt.Sha256,
            receiptBytes.Length, "application/json", "OfficialAcquisitionReceipt", record.DataRevision + ":receipt"));
    }
    CollectionGuards.Require(inputs.Select(x => (x.Record.SourceId, x.DatasetId)).Distinct().Count() == inputs.Count,
        "CollectionRegistrationKeyCollision");

    // 기존 explicit-local 분기를 쓰며 비밀값은 이 도구의 process 환경에만 잠시 둔다.
    using var scopedConnection = await ScopedLocalConnection.PrepareAsync(root);
    var options = await 로컬공공자료Db.OptionsAsync(root);
    var baseline = await ReadCounts(options);
    result["beforeCounts"] = baseline;
    var before = await Readback(options, inputs, false);
    if (mode == "apply" && before.Count < inputs.Count)
    {
        await using var db = new PublicDataIngestionDbContext(options);
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT GET_LOCK('mirror:public-data:neighborhood-collection-r1',0)";
        CollectionGuards.Require(Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 1,
            "CollectionImportBusy");
        await using var transaction = await db.Database.BeginTransactionAsync();
        var service = new 평창군공공공간원본등록Service(db);
        var inserted = 0;
        foreach (var input in inputs)
        {
            var existing = await Find(db, input);
            if (existing != null) { ValidateRow(existing, input); continue; }
            result["databaseWriteAttempted"] = true;
            var registered = await service.RegisterFileAsync(input.AbsolutePath, new 공공공간원본등록Request(
                input.Record.SourceId, input.DatasetId, input.Record.SourceVersion, input.DataRevision,
                input.Record.EvidenceAsOfUtc, input.ContentType, input.RelativePath));
            CollectionGuards.Require(registered.Inserted && registered.ContentLength == input.Bytes
                && registered.SourceHashSha256 == input.Sha256.ToLowerInvariant(), "CollectionRegistrationMismatch");
            var raw = await db.RawSnapshots.Include(x => x.FirstCollectionRun).SingleAsync(x => x.Id == registered.RawSnapshotId);
            raw.CollectedAtUtc = input.Record.CollectedAtUtc;
            var run = raw.FirstCollectionRun!;
            run.StatusCode = 외부데이터수집StatusCodes.Partial; run.ErrorCode = "PendingHumanReview";
            run.ErrorSummary = Summary(input); run.NormalizedCount = 0;
            await db.SaveChangesAsync(); inserted++;
        }
        await transaction.CommitAsync(); result["committed"] = true; result["inserted"] = inserted;
        command.CommandText = "SELECT RELEASE_LOCK('mirror:public-data:neighborhood-collection-r1')";
        _ = await command.ExecuteScalarAsync();
    }
    else if (mode == "apply")
    {
        result["inserted"] = 0; result["strictNoOp"] = true;
        // RegisterFileAsync는 기존 LastSeen을 갱신하므로 정확히 같은 기록에는 호출하지 않는다.
    }
    // 별도 DbContext/물리 connection(pooling=false)로 모든 출처/판본/상태/hash를 재조회한다.
    var stored = await Readback(options, inputs, mode != "preview");
    result["afterCounts"] = await ReadCounts(options);
    result["independentReadbackVerified"] = stored.Count == inputs.Count;
    result["preflightExistingRecordReadVerified"] = true; result["sourceLineageOnly"] = true;
    result["storedRecords"] = stored.Select(row => new
    {
        row.Id, row.FirstCollectionRunId, row.SourceId, row.DatasetId, row.SourceVersion,
        row.ContentHashSha256, row.ContentLength, row.CollectedAtUtc, row.EvidenceAsOfUtc,
        row.StorageContainer, row.StorageObjectName,
        status = row.FirstCollectionRun!.StatusCode, review = row.FirstCollectionRun.ErrorCode,
        row.FirstCollectionRun.DataRevision, row.FirstCollectionRun.ErrorSummary,
    });
    result["payloadKinds"] = manifest.Records.Select(x => new { x.SourceId, x.DatasetId, x.PayloadKind, x.EvidenceAsOfNote });
    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })); return 0;
}
catch (Exception ex)
{
    result["errorCode"] = ex is InvalidDataException ? ex.Message : ex.GetType().Name;
    var deepest = ex; while (deepest.InnerException != null) deepest = deepest.InnerException;
    if (deepest is MySqlException sql) { result["databaseErrorNumber"] = sql.Number; result["databaseSqlState"] = sql.SqlState; }
    // 접속 문자열·원예외·키/비밀값을 내보내지 않는다.
    Console.WriteLine(JsonSerializer.Serialize(result)); return 1;
}
finally { foreach (var file in locks) file.Dispose(); }

static async Task<object> ReadCounts(DbContextOptions<PublicDataIngestionDbContext> options)
{
    await using var db = new PublicDataIngestionDbContext(options);
    return new { rawSnapshots = await db.RawSnapshots.CountAsync(), runs = await db.IngestionRuns.CountAsync(),
        normalizedRecords = await db.NormalizedRecords.CountAsync() };
}
static async Task<외부데이터RawSnapshot?> Find(PublicDataIngestionDbContext db, RegistrationInput input)
{
    var hash = input.Sha256.ToLowerInvariant();
    return await db.RawSnapshots.Include(x => x.FirstCollectionRun).SingleOrDefaultAsync(x =>
        x.SourceId == input.Record.SourceId && x.DatasetId == input.DatasetId && x.ContentHashSha256 == hash);
}
static string Summary(RegistrationInput input) =>
    $"PrivateReview;Kind={input.Kind};ReceiptSHA256={input.Record.Receipt.Sha256.ToLowerInvariant()};License={input.Record.LicenseCode};NoCurrentOrUnity";
static void ValidateRow(외부데이터RawSnapshot row, RegistrationInput input)
{
    CollectionGuards.Require(row.ContentLength == input.Bytes && row.ContentType == input.ContentType
        && row.SourceVersion == input.Record.SourceVersion && row.EvidenceAsOfUtc == input.Record.EvidenceAsOfUtc
        && row.CollectedAtUtc == input.Record.CollectedAtUtc
        && row.StorageContainer == "local-private-public-spatial" && row.StorageObjectName == input.RelativePath
        && row.StorageLocation == "private-file://" + input.RelativePath
        && row.FirstCollectionRun is { } run && run.DataRevision == input.DataRevision
        && run.StatusCode == 외부데이터수집StatusCodes.Partial && run.ErrorCode == "PendingHumanReview"
        && run.ErrorSummary == Summary(input) && run.NormalizedCount == 0, "CollectionExistingBindingConflict");
}
static async Task<List<외부데이터RawSnapshot>> Readback(DbContextOptions<PublicDataIngestionDbContext> options,
    List<RegistrationInput> inputs, bool required)
{
    await using var db = new PublicDataIngestionDbContext(options);
    var rows = new List<외부데이터RawSnapshot>();
    foreach (var input in inputs)
    {
        var row = await Find(db, input);
        if (row == null) { CollectionGuards.Require(!required, "CollectionReadbackMissing"); continue; }
        ValidateRow(row, input); rows.Add(row);
    }
    var ids = rows.Select(x => x.Id).ToList();
    CollectionGuards.Require(!await db.NormalizedRecords.AnyAsync(x => ids.Contains(x.RawSnapshotId)),
        "CollectionUnexpectedNormalization");
    return rows;
}

internal sealed record RegistrationInput(CollectionRecord Record, string AbsolutePath, string RelativePath,
    string DatasetId, string Sha256, long Bytes, string ContentType, string Kind, string DataRevision);
internal sealed class CollectionManifest
{
    public string SchemaVersion { get; set; } = "";
    public string BatchId { get; set; } = "";
    public string Revision { get; set; } = "";
    public string ReviewState { get; set; } = "";
    public bool DistributionApproved { get; set; }
    public bool UnityApplied { get; set; }
    public CollectionRecord[] Records { get; set; } = [];
}
internal sealed class CollectionRecord
{
    public string SourceId { get; set; } = "";
    public string DatasetId { get; set; } = "";
    public string SourceVersion { get; set; } = "";
    public string DataRevision { get; set; } = "";
    public string PayloadKind { get; set; } = "";
    public string OfficialSourceUrl { get; set; } = "";
    public DateTimeOffset CollectedAtUtc { get; set; }
    public DateTimeOffset? EvidenceAsOfUtc { get; set; }
    public string EvidenceAsOfNote { get; set; } = "";
    public string LicenseCode { get; set; } = "";
    public bool PrivateStorageAllowed { get; set; }
    public bool RightsConflict { get; set; }
    public CollectionFile File { get; set; } = new();
    public CollectionFile Receipt { get; set; } = new();
}
internal sealed class CollectionFile
{
    public string Path { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long Bytes { get; set; }
    public string ContentType { get; set; } = "";
}
internal sealed class AcquisitionReceipt
{
    public string SchemaVersion { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string DatasetId { get; set; } = "";
    public string SourceVersion { get; set; } = "";
    public string OfficialSourceUrl { get; set; } = "";
    public string PayloadPath { get; set; } = "";
    public string PayloadSha256 { get; set; } = "";
    public long PayloadBytes { get; set; }
    public string PayloadKind { get; set; } = "";
    public DateTimeOffset CollectedAtUtc { get; set; }
    public DateTimeOffset? EvidenceAsOfUtc { get; set; }
    public string EvidenceAsOfNote { get; set; } = "";
    public string LicenseCode { get; set; } = "";
    public bool PrivateStorageAllowed { get; set; }
    public bool RightsConflict { get; set; }
    public bool DistributionApproved { get; set; }
    public bool RuntimeAuthorized { get; set; }
    public string OriginalReceiptPath { get; set; } = "";
    public string OriginalReceiptSha256 { get; set; } = "";
}

internal static class CollectionGuards
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public static T Read<T>(byte[] bytes)
    {
        // UTF-8 BOM은 원본 hash에 포함하며 JSON을 읽을 때만 제거한다.
        var text = System.Text.Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
        using var doc = JsonDocument.Parse(text); RejectDuplicates(doc.RootElement);
        return JsonSerializer.Deserialize<T>(text, JsonOptions) ?? throw new InvalidDataException("CollectionJsonMissing");
    }
    private static void RejectDuplicates(JsonElement item)
    {
        if (item.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in item.EnumerateObject()) { Require(names.Add(property.Name), "CollectionDuplicateJsonProperty"); RejectDuplicates(property.Value); }
        }
        else if (item.ValueKind == JsonValueKind.Array) foreach (var value in item.EnumerateArray()) RejectDuplicates(value);
    }
    public static void Validate(CollectionManifest manifest)
    {
        Require(manifest.SchemaVersion == "neighborhood-public-data-local-collection.v1"
            && !string.IsNullOrWhiteSpace(manifest.BatchId) && manifest.BatchId.Length <= 120
            && !string.IsNullOrWhiteSpace(manifest.Revision) && manifest.Revision.Length <= 160, "CollectionManifestSchemaInvalid");
        Require(manifest.ReviewState == "PendingHumanReview" && !manifest.DistributionApproved && !manifest.UnityApplied,
            "CollectionAuthorityRejected");
        Require(manifest.Records.Length is > 0 and <= 7, "CollectionRecordBudgetInvalid");
        Require(manifest.Records.Select(x => (x.SourceId, x.DatasetId)).Distinct().Count() == manifest.Records.Length,
            "CollectionDuplicateDataset");
        foreach (var item in manifest.Records)
        {
            Require(item.PrivateStorageAllowed && !item.RightsConflict && !item.LicenseCode.Contains("Conflict", StringComparison.OrdinalIgnoreCase),
                "CollectionRightsConflictRejected");
            Require(!string.IsNullOrWhiteSpace(item.SourceId) && item.SourceId.Length <= 160
                && !string.IsNullOrWhiteSpace(item.DatasetId) && item.DatasetId.Length <= 140
                && !string.IsNullOrWhiteSpace(item.SourceVersion) && item.SourceVersion.Length <= 200
                && !string.IsNullOrWhiteSpace(item.DataRevision) && item.DataRevision.Length <= 180
                && !string.IsNullOrWhiteSpace(item.LicenseCode) && item.LicenseCode.Length <= 100, "CollectionIdentityInvalid");
            Require(item.PayloadKind is "DatasetInventory" or "SourceMetadata" or "PhysicalGeometryOrMeasurement" or "PlanningGeometry" or "OfficialBuildingRegisterRecords", "CollectionPayloadKindInvalid");
            Require(Uri.TryCreate(item.OfficialSourceUrl, UriKind.Absolute, out var url) && url.Scheme == "https"
                && url.UserInfo == "" && (url.Host.EndsWith(".go.kr", StringComparison.OrdinalIgnoreCase)
                    || url.Host.Equals("www.vworld.kr", StringComparison.OrdinalIgnoreCase))
                && !url.Query.Contains("key", StringComparison.OrdinalIgnoreCase) && !url.Query.Contains("token", StringComparison.OrdinalIgnoreCase),
                "CollectionOfficialUrlInvalid");
            Require(item.CollectedAtUtc.Offset == TimeSpan.Zero && item.CollectedAtUtc.UtcDateTime.Date == new DateTime(2026, 10, 2)
                && item.CollectedAtUtc.Ticks % 10 == 0 && (item.EvidenceAsOfUtc is null ||
                    item.EvidenceAsOfUtc.Value.Offset == TimeSpan.Zero && item.EvidenceAsOfUtc.Value.Ticks % 10 == 0
                    && item.EvidenceAsOfUtc <= item.CollectedAtUtc)
                && !string.IsNullOrWhiteSpace(item.EvidenceAsOfNote), "CollectionEvidenceDateInvalid");
            Require(item.File.Bytes is > 0 and <= 64 * 1024 * 1024 && item.File.ContentType.Length is > 0 and <= 160,
                "CollectionPayloadBudgetInvalid");
            Require(IsHash(item.File.Sha256) && IsHash(item.Receipt.Sha256), "CollectionHashInvalid");
            foreach (var path in new[] { item.File.Path, item.Receipt.Path })
                Require(!path.Contains("AL_D010", StringComparison.OrdinalIgnoreCase)
                    && !path.Contains("/generations/", StringComparison.OrdinalIgnoreCase)
                    && !item.DatasetId.Contains("al-d010", StringComparison.OrdinalIgnoreCase), "CollectionExistingFrozenSourceRejected");
            Require(item.File.Path != item.Receipt.Path, "CollectionReceiptEqualsPayload");
        }
    }
    public static void ValidateReceipt(AcquisitionReceipt receipt, CollectionRecord item)
    {
        Require(receipt.SchemaVersion == "neighborhood-official-data-acquisition.v1"
            && receipt.SourceId == item.SourceId && receipt.DatasetId == item.DatasetId
            && receipt.SourceVersion == item.SourceVersion && receipt.EvidenceAsOfNote == item.EvidenceAsOfNote
            && receipt.OfficialSourceUrl == item.OfficialSourceUrl && receipt.PayloadPath == item.File.Path
            && receipt.PayloadSha256.Equals(item.File.Sha256, StringComparison.OrdinalIgnoreCase)
            && receipt.PayloadBytes == item.File.Bytes && receipt.PayloadKind == item.PayloadKind
            && receipt.CollectedAtUtc == item.CollectedAtUtc && receipt.EvidenceAsOfUtc == item.EvidenceAsOfUtc
            && receipt.LicenseCode == item.LicenseCode && receipt.PrivateStorageAllowed && !receipt.RightsConflict
            && !receipt.DistributionApproved && !receipt.RuntimeAuthorized, "CollectionReceiptBindingMismatch");
        Require(string.IsNullOrEmpty(receipt.OriginalReceiptPath) == string.IsNullOrEmpty(receipt.OriginalReceiptSha256)
            && (string.IsNullOrEmpty(receipt.OriginalReceiptSha256) || IsHash(receipt.OriginalReceiptSha256)),
            "CollectionOriginalReceiptBindingInvalid");
    }
    public static string SafePath(string root, string relative)
    {
        Require(!Path.IsPathRooted(relative) && !relative.Contains('\\'), "CollectionRelativePathInvalid");
        var path = Path.GetFullPath(Path.Combine(root, relative));
        Require(path.StartsWith(Path.Combine(root, "artifacts", "local") + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase), "CollectionPathOutsideLocalArtifacts");
        for (var dir = new FileInfo(path).Directory; dir != null; dir = dir.Parent)
            Require(!dir.Exists || !dir.Attributes.HasFlag(FileAttributes.ReparsePoint), "CollectionReparseRejected");
        Require(!File.Exists(path) || !File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint), "CollectionReparseRejected");
        return path;
    }
    public static byte[] LockAndHash(string path, string expectedHash, long? expectedBytes, List<FileStream> locks)
    {
        Require(IsHash(expectedHash), "CollectionHashInvalid");
        var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read); locks.Add(file);
        Require(file.Length is > 0 and <= 64 * 1024 * 1024 && (!expectedBytes.HasValue || file.Length == expectedBytes),
            "CollectionInputLengthMismatch");
        using var memory = new MemoryStream(); file.CopyTo(memory); var bytes = memory.ToArray();
        Require(Hash(bytes).Equals(expectedHash, StringComparison.OrdinalIgnoreCase), "CollectionInputHashMismatch");
        return bytes;
    }
    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    public static bool IsHash(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);
    public static void Require(bool value, string code) { if (!value) throw new InvalidDataException(code); }
    public static int SelfTest()
    {
        var good = new CollectionManifest { SchemaVersion = "neighborhood-public-data-local-collection.v1", BatchId = "fixture", Revision = "r1",
            ReviewState = "PendingHumanReview", Records = [new() { SourceId = "ngii", DatasetId = "fixture-inventory", SourceVersion = "revision", DataRevision = "r1",
            PayloadKind = "DatasetInventory", OfficialSourceUrl = "https://www.data.go.kr/data/15067637/fileData.do", CollectedAtUtc = DateTimeOffset.Parse("2026-10-02T12:00:00Z"),
            EvidenceAsOfNote = "Row-specific basis dates; no single file date", LicenseCode = "FixtureOnly", PrivateStorageAllowed = true,
            File = new() { Path = "artifacts/local/fixture/inventory.csv", Sha256 = new string('A',64), Bytes = 10, ContentType = "text/csv" },
            Receipt = new() { Path = "artifacts/local/fixture/receipt.json", Sha256 = new string('B',64) } }] };
        Validate(good); var passed = 1;
        foreach (var mutate in new Action<CollectionManifest>[] {
            x=>x.UnityApplied=true, x=>x.DistributionApproved=true, x=>x.ReviewState="Approved",
            x=>x.Records[0].PrivateStorageAllowed=false, x=>x.Records[0].RightsConflict=true,
            x=>x.Records[0].LicenseCode="RightsConflictUnresolved", x=>x.Records[0].File.Path="artifacts/local/AL_D010.zip",
            x=>x.Records[0].File.Path="artifacts/local/frozen/generations/r7/file.json", x=>x.Records[0].File.Sha256="bad",
            x=>x.Records[0].EvidenceAsOfNote="", x=>x.Records[0].CollectedAtUtc=DateTimeOffset.Parse("2026-09-14T00:00:00Z"),
            x=>x.Records[0].OfficialSourceUrl="https://www.data.go.kr/x?serviceKey=secret", x=>x.Records=[x.Records[0],x.Records[0]],
            x=>x.Records[0].PayloadKind="GuessedHeight" })
        {
            var copy=JsonSerializer.Deserialize<CollectionManifest>(JsonSerializer.Serialize(good,JsonOptions),JsonOptions)!; mutate(copy);
            var rejected=false; try { Validate(copy); } catch(InvalidDataException) { rejected=true; }
            Require(rejected,"CollectionNegativeFixturePassed"); passed++;
        }
        var denied=false; try { SafePath(Path.GetFullPath("."),"../outside.txt"); } catch(InvalidDataException){denied=true;}
        Require(denied,"CollectionPathFixturePassed"); passed++;
        denied=false; try { _=Read<CollectionManifest>(System.Text.Encoding.UTF8.GetBytes("{\"batchId\":\"a\",\"batchId\":\"b\"}")); } catch(InvalidDataException){denied=true;}
        Require(denied,"CollectionDuplicateFixturePassed"); passed++;
        foreach (var kind in new[] { "PlanningGeometry", "OfficialBuildingRegisterRecords", "SourceMetadata" })
        {
            var copy=JsonSerializer.Deserialize<CollectionManifest>(JsonSerializer.Serialize(good,JsonOptions),JsonOptions)!;
            copy.Records[0].PayloadKind=kind; Validate(copy); passed++;
        }
        var record=good.Records[0];
        var receipt=new AcquisitionReceipt { SchemaVersion="neighborhood-official-data-acquisition.v1",SourceId=record.SourceId,
            DatasetId=record.DatasetId,SourceVersion=record.SourceVersion,OfficialSourceUrl=record.OfficialSourceUrl,
            PayloadPath=record.File.Path,PayloadSha256=record.File.Sha256,PayloadBytes=record.File.Bytes,PayloadKind=record.PayloadKind,
            CollectedAtUtc=record.CollectedAtUtc,EvidenceAsOfUtc=record.EvidenceAsOfUtc,EvidenceAsOfNote=record.EvidenceAsOfNote,
            LicenseCode=record.LicenseCode,PrivateStorageAllowed=true };
        ValidateReceipt(receipt,record);passed++;
        foreach(var mutate in new Action<AcquisitionReceipt>[] {
            x=>x.PayloadSha256=new string('C',64),x=>x.PayloadBytes++,x=>x.PayloadKind="PhysicalGeometryOrMeasurement",
            x=>x.SourceVersion="other",x=>x.EvidenceAsOfNote="invented",x=>x.RuntimeAuthorized=true,
            x=>x.PrivateStorageAllowed=false,x=>x.OriginalReceiptPath="artifacts/local/missing-hash.json" })
        {
            var copy=JsonSerializer.Deserialize<AcquisitionReceipt>(JsonSerializer.Serialize(receipt,JsonOptions),JsonOptions)!;
            mutate(copy);denied=false;try{ValidateReceipt(copy,record);}catch(InvalidDataException){denied=true;}
            Require(denied,"CollectionReceiptFixturePassed");passed++;
        }
        var officialDefinition=JsonSerializer.Deserialize<CollectionManifest>(JsonSerializer.Serialize(good,JsonOptions),JsonOptions)!;
        officialDefinition.Records[0].OfficialSourceUrl="https://www.vworld.kr/contents/definition.xlsx";
        Validate(officialDefinition);passed++;
        officialDefinition.Records[0].OfficialSourceUrl="https://www.vworld.kr.example.org/contents/definition.xlsx";
        denied=false;try{Validate(officialDefinition);}catch(InvalidDataException){denied=true;}
        Require(denied,"CollectionOfficialHostFixturePassed");passed++;
        return passed;
    }
}

internal sealed class ScopedLocalConnection : IDisposable
{
    private const string Variable = "SSALDDEL_PUBLIC_DATA_LOCAL_CONNECTION";
    private readonly string? previous = Environment.GetEnvironmentVariable(Variable);
    public static async Task<ScopedLocalConnection> PrepareAsync(string root)
    {
        var scope = new ScopedLocalConnection();
        if (!string.IsNullOrWhiteSpace(scope.previous)) return scope;
        var start = new ProcessStartInfo("docker") { RedirectStandardOutput=true, RedirectStandardError=true, UseShellExecute=false, CreateNoWindow=true };
        start.ArgumentList.Add("inspect"); start.ArgumentList.Add("hongdal-mysql-1");
        using var process = Process.Start(start)!; var output=process.StandardOutput.ReadToEndAsync(); var error=process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(); await error; CollectionGuards.Require(process.ExitCode==0,"CollectionDockerInspectionFailed");
        using var data=JsonDocument.Parse(await output); var container=data.RootElement[0]; var config=container.GetProperty("Config"); var labels=config.GetProperty("Labels");
        CollectionGuards.Require(container.GetProperty("Name").GetString()=="/hongdal-mysql-1"
            && container.GetProperty("State").GetProperty("Running").GetBoolean()
            && labels.GetProperty("com.docker.compose.project").GetString()=="hongdal"
            && labels.GetProperty("com.docker.compose.service").GetString()=="mysql"
            && Path.TrimEndingDirectorySeparator(Path.GetFullPath(labels.GetProperty("com.docker.compose.project.working_dir").GetString()!)).Equals(root,StringComparison.OrdinalIgnoreCase)
            && container.GetProperty("NetworkSettings").GetProperty("Ports").GetProperty("3306/tcp").EnumerateArray().Any(p=>p.GetProperty("HostPort").GetString()=="13306"),
            "CollectionDockerTargetMismatch");
        var env=config.GetProperty("Env").EnumerateArray().Select(x=>x.GetString()!.Split('=',2)).ToDictionary(x=>x[0],x=>x[1]);
        CollectionGuards.Require(env.TryGetValue("MYSQL_USER",out var user) && !string.IsNullOrWhiteSpace(user) && user!="root"
            && env.TryGetValue("MYSQL_PASSWORD",out var password) && !string.IsNullOrWhiteSpace(password), "CollectionDockerAccountRejected");
        var connection=new MySqlConnectionStringBuilder { Server="127.0.0.1", Port=13306, Database="hongdal_dev", UserID=user,
            Password=env["MYSQL_PASSWORD"], PersistSecurityInfo=false, Pooling=false, ConnectionTimeout=10, DefaultCommandTimeout=30 };
        Environment.SetEnvironmentVariable(Variable,connection.ConnectionString);
        return scope;
    }
    public void Dispose() => Environment.SetEnvironmentVariable(Variable,previous);
}
