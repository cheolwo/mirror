using System.Text;
using System.Text.Json;
using Ssalddel.Domain.PublicData;

internal static class SelfTests
{
    public static int Run()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "observer-self-test-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(Path.Combine(root, "artifacts", "local"));
        File.WriteAllText(Path.Combine(root, "AGENTS.md"), "Offline fixture only", Encoding.UTF8);
        var passed = 0;
        try
        {
            var meta = Entry("metadata", "fixture:catalog", "metadata.json");
            var observed = Entry("observations", "fixture:observations", "observed.csv");
            File.WriteAllText(Path.Combine(root, "artifacts", "local", "metadata.json"), "{}", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(root, "artifacts", "local", "observed.csv"), "period,value\n2024,1\n", new UTF8Encoding(false));
            meta.RawSha256 = ImportGuards.Hash(File.ReadAllBytes(Path.Combine(root, meta.RawPath)));
            observed.RawSha256 = ImportGuards.Hash(File.ReadAllBytes(Path.Combine(root, observed.RawPath)));
            observed.RecordsPath = "artifacts/local/records.json";
            var record = Record(observed);
            File.WriteAllBytes(Path.Combine(root, observed.RecordsPath), JsonSerializer.SerializeToUtf8Bytes(new[] { record }, ImportGuards.Json));
            observed.RecordsSha256 = ImportGuards.Hash(File.ReadAllBytes(Path.Combine(root, observed.RecordsPath)));
            var good = new ImportManifest { SchemaVersion = "observer-public-data-db.v1", Entries = [meta, observed] };

            void Check(bool ok) { ImportGuards.Require(ok, "ImportSelfTestFailed"); passed++; }
            void Reject(Action action, string code)
            {
                try { action(); } catch (ImportGuardException error) when (error.Code == code) { passed++; return; }
                throw new ImportGuardException("ImportNegativeSelfTestFailed");
            }
            ImportManifest Copy() => ImportGuards.Read<ImportManifest>(JsonSerializer.SerializeToUtf8Bytes(good, ImportGuards.Json));
            List<PreparedEntry> Load(ImportManifest manifest, string? hash = null)
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(manifest, ImportGuards.Json);
                var path = Path.Combine(root, "artifacts", "local", "manifest.json");
                File.WriteAllBytes(path, bytes);
                var input = new ImportInput("preview", root, path, hash ?? ImportGuards.Hash(bytes), Path.Combine(root, "artifacts", "local", "receipt.json"));
                var handles = new List<FileStream>();
                try { return ImportGuards.Load(input, handles); }
                finally { foreach (var handle in handles) handle.Dispose(); }
            }

            var loaded = Load(good);
            Check(loaded.Count == 2 && loaded[0].Entry.EvidenceAsOfUtc is null && loaded[0].Records.Length == 0
                && loaded[1].Records.Length == 1 && loaded[1].Records[0].NumericValue == 1);
            Reject(() => Load(good, new string('0', 64)), "ImportFileHashMismatch");
            var copy = Copy(); copy.Entries[0].RawSha256 = new string('0', 64);
            Reject(() => Load(copy), "ImportFileHashMismatch");
            copy = Copy(); copy.Entries[0].RawPath = "artifacts/local/../outside.json";
            Reject(() => Load(copy), "ImportPathRejected");
            copy = Copy(); copy.Entries[0].RawPath = "docs/file.json";
            Reject(() => Load(copy), "ImportPathRejected");
            copy = Copy(); copy.Entries[0].RawPath = "artifacts/local/missing.json";
            Reject(() => Load(copy), "ImportInputMissing");
            copy = Copy(); copy.Entries[0].RecordsPath = observed.RecordsPath;
            Reject(() => Load(copy), "ImportRecordsBindingInvalid");
            copy = Copy(); copy.Entries = [meta, meta];
            Reject(() => Load(copy), "ImportDuplicateEntry");
            copy = Copy(); copy.Entries = Enumerable.Repeat(meta, ImportGuards.MaxEntries + 1).ToArray();
            Reject(() => Load(copy), "ImportManifestInvalid");
            copy = Copy(); copy.Entries[0].OfficialUrl = "https://example.go.kr/file?serviceKey=not-a-real-key";
            Reject(() => Load(copy), "ImportCredentialUrlRejected");
            Reject(() => ImportGuards.Read<ImportManifest>(Encoding.UTF8.GetBytes("{\"schemaVersion\":\"a\",\"schemaVersion\":\"b\",\"entries\":[]}")), "ImportDuplicateJsonProperty");
            try { _ = ImportGuards.Read<ImportEntry>(Encoding.UTF8.GetBytes("{\"unknown\":1}")); throw new ImportGuardException("ImportSelfTestFailed"); }
            catch (JsonException) { passed++; }
            try { _ = ImportGuards.Read<ImportEntry>(Encoding.UTF8.GetBytes("{\"collectedAtUtc\":\"2026-11-13T01:00:00\"}")); throw new ImportGuardException("ImportSelfTestFailed"); }
            catch (ImportGuardException error) when (error.Code == "ImportUtcTimestampInvalid") { passed++; }
            Reject(() => ImportGuards.Time(observed.CollectedAtUtc.AddTicks(1)), "ImportUtcPrecisionInvalid");

            foreach (var change in new Action<ImportRecord>[]
            {
                x => x.SourceId = "another-source", x => x.DatasetId = "another-dataset",
                x => x.SourceVersion = "another-version", x => x.DataRevision = "another-revision",
            })
            {
                var changed = ImportGuards.Read<ImportRecord>(JsonSerializer.SerializeToUtf8Bytes(record, ImportGuards.Json));
                change(changed); Reject(() => ImportGuards.ValidateRecord(changed, observed), "ImportRecordProvenanceMismatch");
            }
            var wrong = Record(observed); wrong.EvidenceAsOfUtc = default;
            Reject(() => ImportGuards.ValidateRecord(wrong, observed), "ImportUtcPrecisionInvalid");
            wrong = Record(observed); wrong.NumericValue = 1.00000000001m;
            Reject(() => ImportGuards.ValidateRecord(wrong, observed), "ImportNumericPrecisionInvalid");
            wrong = Record(observed); wrong.RecordKey = "not-a-key";
            Reject(() => ImportGuards.ValidateRecord(wrong, observed), "ImportHashInvalid");
            wrong = Record(observed); wrong.RecordKey = new string('a', 64);
            Reject(() => ImportGuards.ValidateRecord(wrong, observed), "ImportRecordCanonicalKeyMismatch");
            var repeated = new[] { record, record };
            var repeatedBytes = JsonSerializer.SerializeToUtf8Bytes(repeated, ImportGuards.Json);
            File.WriteAllBytes(Path.Combine(root, observed.RecordsPath!), repeatedBytes);
            copy = Copy(); copy.Entries[1].RecordsSha256 = ImportGuards.Hash(repeatedBytes);
            Reject(() => Load(copy), "ImportRecordKeyDuplicate");
            var sizePath = Path.Combine(root, "artifacts", "local", "oversized.bin");
            using (var oversized = File.Create(sizePath)) oversized.SetLength(ImportGuards.MaxBytes + 1);
            var sizeHandles = new List<FileStream>();
            try { Reject(() => ImportGuards.LockedBytes(sizePath, new string('0', 64), sizeHandles), "ImportFileSizeInvalid"); }
            finally { foreach (var handle in sizeHandles) handle.Dispose(); }
            return passed;
        }
        finally
        {
            var basePath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory)) + Path.DirectorySeparatorChar;
            ImportGuards.Require(root.StartsWith(basePath, StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(root).StartsWith("observer-self-test-", StringComparison.Ordinal), "ImportSelfTestCleanupRejected");
            Directory.Delete(root, true);
        }
    }
    private static ImportEntry Entry(string kind, string dataset, string name) => new()
    {
        Kind = kind, SourceId = "offline-fixture", DatasetId = dataset, SourceVersion = "r1", DataRevision = "r1:fixture",
        CollectedAtUtc = new DateTimeOffset(2026, 11, 13, 1, 0, 0, TimeSpan.Zero), EvidenceAsOfUtc = null,
        RawPath = "artifacts/local/" + name, ContentType = "application/json", OfficialUrl = "https://example.go.kr/file",
    };
    private static ImportRecord Record(ImportEntry entry) => new()
    {
        SourceId = entry.SourceId, DatasetId = entry.DatasetId,
        RecordKey = 외부데이터RecordKey.Create(entry.SourceId, entry.DatasetId, "country:kr", "fixture.value",
            new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), "fixture=one"), StableId = "fixture:value:2024",
        RegionStableId = "country:kr", MetricCode = "fixture.value", NumericValue = 1, TextValue = "",
        UnitCode = "count", EvidenceAsOfUtc = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
        CollectedAtUtc = entry.CollectedAtUtc, SpatialPrecisionCode = "country", TemporalPrecisionCode = "annual",
        QualityCode = "PendingHumanReview", LimitationCode = "FixtureOnly", DimensionKey = "fixture=one",
        SourceVersion = entry.SourceVersion, DataRevision = entry.DataRevision,
    };
}
