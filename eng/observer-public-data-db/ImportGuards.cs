using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Ssalddel.Domain.PublicData;

internal sealed class ImportGuardException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
internal sealed record ImportInput(string Mode, string Root, string Manifest, string Sha256, string Receipt);
internal sealed class ImportManifest
{
    public string SchemaVersion { get; set; } = "";
    public ImportEntry[] Entries { get; set; } = [];
}
internal sealed class ImportEntry
{
    public string Kind { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string DatasetId { get; set; } = "";
    public string SourceVersion { get; set; } = "";
    public string DataRevision { get; set; } = "";
    public DateTimeOffset CollectedAtUtc { get; set; }
    public DateTimeOffset? EvidenceAsOfUtc { get; set; }
    public string RawPath { get; set; } = "";
    public string RawSha256 { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string OfficialUrl { get; set; } = "";
    public string? RecordsPath { get; set; }
    public string? RecordsSha256 { get; set; }
}
internal sealed class ImportRecord
{
    public string SourceId { get; set; } = "";
    public string DatasetId { get; set; } = "";
    public string RecordKey { get; set; } = "";
    public string StableId { get; set; } = "";
    public string RegionStableId { get; set; } = "";
    public string MetricCode { get; set; } = "";
    public decimal? NumericValue { get; set; }
    public string TextValue { get; set; } = "";
    public string UnitCode { get; set; } = "";
    public DateTimeOffset EvidenceAsOfUtc { get; set; }
    public DateTimeOffset CollectedAtUtc { get; set; }
    public string SpatialPrecisionCode { get; set; } = "";
    public string TemporalPrecisionCode { get; set; } = "";
    public string QualityCode { get; set; } = "";
    public string LimitationCode { get; set; } = "";
    public string DimensionKey { get; set; } = "";
    public string SourceVersion { get; set; } = "";
    public string DataRevision { get; set; } = "";
}
internal sealed record PreparedEntry(ImportEntry Entry, long RawBytes, ImportRecord[] Records);

internal static class ImportGuards
{
    public const int MaxEntries = 64;
    public const int MaxRecords = 100_000;
    public const long MaxBytes = 64L * 1024 * 1024;
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, WriteIndented = true,
        Converters = { new UtcConverter() },
    };
    public static void Require([DoesNotReturnIf(false)] bool condition, string code)
    {
        if (!condition) throw new ImportGuardException(code);
    }
    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    public static void HashValue(string? value) => Require(value is not null && Regex.IsMatch(value, "^[a-fA-F0-9]{64}$"), "ImportHashInvalid");
    public static void Text(string? value, int max, string code, bool empty = false)
        => Require(value is not null && value.Length <= max && (empty || value.Length > 0)
            && value == value.Trim() && !value.Any(char.IsControl), code);
    public static void Time(DateTimeOffset value)
        => Require(value.Offset == TimeSpan.Zero && value.Year >= 1000 && value.Ticks % 10 == 0, "ImportUtcPrecisionInvalid");

    public static ImportInput ParseArguments(string[] args)
    {
        Require(args.Length == 9 && args[0] is "preview" or "apply" or "verify", "ImportArgumentsInvalid");
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Length; i += 2)
            Require(args[i] is "--root" or "--manifest" or "--sha256" or "--receipt"
                && options.TryAdd(args[i], args[i + 1]), "ImportArgumentsInvalid");
        Require(options.Count == 4 && Path.IsPathFullyQualified(options["--root"]), "ImportRootInvalid");
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options["--root"]));
        Require(Directory.Exists(root) && File.Exists(Path.Combine(root, "AGENTS.md")), "ImportRootInvalid");
        HashValue(options["--sha256"]);
        var manifest = AbsoluteArtifactPath(root, options["--manifest"], true);
        var receipt = AbsoluteArtifactPath(root, options["--receipt"], false);
        Require(!File.Exists(receipt) && !Directory.Exists(receipt), "ImportReceiptAlreadyExists");
        return new(args[0], root, manifest, options["--sha256"].ToLowerInvariant(), receipt);
    }
    public static string RelativeArtifactPath(string root, string value)
    {
        Require(!string.IsNullOrWhiteSpace(value) && !value.Contains('\\') && !value.Contains(':')
            && !Path.IsPathRooted(value) && value.StartsWith("artifacts/local/", StringComparison.Ordinal)
            && !value.Split('/').Any(p => p is "" or "." or ".."), "ImportPathRejected");
        return AbsoluteArtifactPath(root, Path.GetFullPath(Path.Combine(root, value)), true);
    }
    public static string AbsoluteArtifactPath(string root, string value, bool mustExist)
    {
        Require(Path.IsPathFullyQualified(value), "ImportPathRejected");
        var path = Path.GetFullPath(value);
        var allowed = Path.GetFullPath(Path.Combine(root, "artifacts", "local")) + Path.DirectorySeparatorChar;
        Require(path.StartsWith(allowed, StringComparison.OrdinalIgnoreCase), "ImportPathRejected");
        var relative = Path.GetRelativePath(root, path);
        var current = root;
        Require((File.GetAttributes(root) & FileAttributes.ReparsePoint) == 0, "ImportReparsePointRejected");
        foreach (var part in relative.Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, part);
            try { Require((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "ImportReparsePointRejected"); }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
        Require(!mustExist || File.Exists(path), "ImportInputMissing");
        return path;
    }
    public static byte[] LockedBytes(string path, string expected, List<FileStream> handles, long max = MaxBytes)
    {
        HashValue(expected);
        var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        handles.Add(file);
        Require(file.Length > 0 && file.Length <= max, "ImportFileSizeInvalid");
        using var memory = new MemoryStream(); file.CopyTo(memory);
        var bytes = memory.ToArray();
        Require(bytes.LongLength == file.Length && Hash(bytes).Equals(expected, StringComparison.OrdinalIgnoreCase), "ImportFileHashMismatch");
        return bytes;
    }
    public static T Read<T>(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        CheckDuplicates(document.RootElement);
        return JsonSerializer.Deserialize<T>(bytes, Json) ?? throw new ImportGuardException("ImportJsonInvalid");
    }
    private static void CheckDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                Require(seen.Add(property.Name), "ImportDuplicateJsonProperty"); CheckDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) CheckDuplicates(item);
    }
    public static List<PreparedEntry> Load(ImportInput input, List<FileStream> handles)
    {
        var manifest = Read<ImportManifest>(LockedBytes(input.Manifest, input.Sha256, handles, 2 * 1024 * 1024));
        Require(manifest.SchemaVersion == "observer-public-data-db.v1" && manifest.Entries is { Length: > 0 and <= MaxEntries }, "ImportManifestInvalid");
        var result = new List<PreparedEntry>();
        var entries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var count = 0;
        foreach (var entry in manifest.Entries)
        {
            ValidateEntry(entry);
            Require(entries.Add(entry.SourceId + "\n" + entry.DatasetId + "\n" + entry.RawSha256), "ImportDuplicateEntry");
            var raw = LockedBytes(RelativeArtifactPath(input.Root, entry.RawPath), entry.RawSha256, handles);
            ImportRecord[] records = [];
            if (entry.RecordsPath is not null)
            {
                Require(entry.Kind == "observations", "ImportReferenceNormalizationRejected");
                records = Read<ImportRecord[]>(LockedBytes(RelativeArtifactPath(input.Root, entry.RecordsPath), entry.RecordsSha256!, handles));
            }
            Require(entry.Kind != "observations" || records.Length > 0, "ImportObservationRecordsMissing");
            count += records.Length; Require(count <= MaxRecords, "ImportRecordLimitExceeded");
            foreach (var record in records)
            {
                ValidateRecord(record, entry);
                Require(keys.Add(record.RecordKey), "ImportRecordKeyDuplicate");
            }
            result.Add(new(entry, raw.LongLength, records));
        }
        return result;
    }
    public static void ValidateEntry(ImportEntry entry)
    {
        Require(entry is not null && entry.Kind is "metadata" or "reference" or "observations", "ImportKindInvalid");
        Text(entry.SourceId, 160, "ImportSourceInvalid"); Text(entry.DatasetId, 160, "ImportDatasetInvalid");
        Text(entry.SourceVersion, 200, "ImportVersionInvalid"); Text(entry.DataRevision, 200, "ImportVersionInvalid");
        Time(entry.CollectedAtUtc); if (entry.EvidenceAsOfUtc.HasValue) Time(entry.EvidenceAsOfUtc.Value);
        HashValue(entry.RawSha256); Text(entry.ContentType, 160, "ImportContentTypeInvalid");
        Require((entry.RecordsPath is null) == (entry.RecordsSha256 is null), "ImportRecordsBindingInvalid");
        if (entry.RecordsSha256 is not null) HashValue(entry.RecordsSha256);
        Require(Uri.TryCreate(entry.OfficialUrl, UriKind.Absolute, out var uri) && uri.Scheme == "https"
            && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment)
            && !uri.IsLoopback, "ImportOfficialUrlInvalid");
        Require(!Regex.IsMatch(Uri.UnescapeDataString(uri.Query), @"(?:[?&])(?:servicekey|api_?key|token|access_token|password|secret)=", RegexOptions.IgnoreCase), "ImportCredentialUrlRejected");
    }
    public static void ValidateRecord(ImportRecord record, ImportEntry entry)
    {
        Require(record is not null, "ImportRecordInvalid"); HashValue(record.RecordKey);
        Text(record.StableId, 240, "ImportStableIdInvalid"); Text(record.RegionStableId, 240, "ImportRegionInvalid");
        Require(RegionStableIdRules.IsValid(record.RegionStableId), "ImportRegionInvalid");
        Text(record.MetricCode, 160, "ImportMetricInvalid"); Text(record.UnitCode, 80, "ImportUnitInvalid");
        Text(record.TextValue, 2000, "ImportTextInvalid", true);
        Text(record.SpatialPrecisionCode, 80, "ImportPrecisionInvalid"); Text(record.TemporalPrecisionCode, 80, "ImportPrecisionInvalid");
        Text(record.QualityCode, 80, "ImportQualityInvalid"); Text(record.LimitationCode, 240, "ImportLimitationInvalid", true);
        Text(record.DimensionKey, 500, "ImportDimensionInvalid", true);
        Require(record.SourceId == entry.SourceId && record.DatasetId == entry.DatasetId
            && record.SourceVersion == entry.SourceVersion && record.DataRevision == entry.DataRevision, "ImportRecordProvenanceMismatch");
        Time(record.EvidenceAsOfUtc); Time(record.CollectedAtUtc);
        Require(record.CollectedAtUtc == entry.CollectedAtUtc, "ImportRecordCollectedTimeMismatch");
        Require(record.RecordKey.Equals(외부데이터RecordKey.Create(entry.SourceId, entry.DatasetId,
            record.RegionStableId, record.MetricCode, record.EvidenceAsOfUtc, record.DimensionKey), StringComparison.OrdinalIgnoreCase),
            "ImportRecordCanonicalKeyMismatch");
        if (record.NumericValue is decimal value)
            Require(value > -1_000_000_000_000_000_000m && value < 1_000_000_000_000_000_000m && decimal.Round(value, 10) == value, "ImportNumericPrecisionInvalid");
    }
    private sealed class UtcConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            Require(value is not null && Regex.IsMatch(value, @"(?:Z|\+00:00)$")
                && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _), "ImportUtcTimestampInvalid");
            var parsed = DateTimeOffset.Parse(value!, CultureInfo.InvariantCulture, DateTimeStyles.None); Time(parsed); return parsed;
        }
        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString("O", CultureInfo.InvariantCulture));
    }
}
