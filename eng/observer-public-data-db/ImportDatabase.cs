using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;

internal static class ImportDatabase
{
    private const string Storage = "local-private-observer-public-data";
    private const string LockName = "mirror:observer-public-data-db:v1";
    public static async Task RunAsync(ImportInput input, List<PreparedEntry> prepared,
        DbContextOptions<PublicDataIngestionDbContext> options, Dictionary<string, object?> result)
    {
        var readOptions = new DbContextOptionsBuilder<PublicDataIngestionDbContext>(options)
            .AddInterceptors(new ReadOnlyImportInterceptor()).Options;
        await using (var baseline = new PublicDataIngestionDbContext(readOptions))
        {
            await ValidateTimeSchema(baseline);
            result["beforeCounts"] = await Counts(baseline);
        }
        result["databaseDateTimePrecision"] = 6;
        result["timePrecisionPolicy"] = "InputMustAlreadyBeUTCAtMostSixFractionalDigits;NoImporterRounding";
        List<EntryResult> entries;
        var inserted = 0;
        if (input.Mode == "apply")
        {
            await using var db = new PublicDataIngestionDbContext(options);
            db.ChangeTracker.AutoDetectChangesEnabled = false;
            await db.Database.OpenConnectionAsync();
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"SELECT GET_LOCK('{LockName}',0)";
            ImportGuards.Require(Convert.ToInt32(await command.ExecuteScalarAsync()) == 1, "ImportDatabaseBusy");
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);
                entries = await Inspect(db, prepared, false);
                foreach (var item in prepared.Zip(entries))
                {
                    if (item.Second.Existing) continue;
                    result["databaseWriteAttempted"] = true;
                    var raw = CreateRaw(item.First);
                    db.RawSnapshots.Add(raw);
                    await db.SaveChangesAsync();
                    if (item.First.Records.Length > 0)
                    {
                        db.NormalizedRecords.AddRange(item.First.Records.Select(record => ToEntity(record, item.First.Entry, raw.Id)));
                        await db.SaveChangesAsync();
                    }
                    db.ChangeTracker.Clear();
                    inserted++;
                }
                db.ChangeTracker.Clear();
                var transactionReadback = await Inspect(db, prepared, true);
                result["transactionReadbackVerified"] = transactionReadback.All(x => x.Existing);
                if (inserted > 0)
                {
                    await transaction.CommitAsync(); result["committed"] = true;
                }
                else await transaction.RollbackAsync();
            }
            finally
            {
                command.CommandText = $"SELECT RELEASE_LOCK('{LockName}')";
                _ = await command.ExecuteScalarAsync();
            }
        }
        else
        {
            await using var db = new PublicDataIngestionDbContext(readOptions);
            entries = await Inspect(db, prepared, input.Mode == "verify");
        }

        // 쓰기 연결을 닫은 뒤 pooling=false의 별도 DbContext로 영속 결과를 재조회한다.
        await using var independent = new PublicDataIngestionDbContext(readOptions);
        var readback = await Inspect(independent, prepared, input.Mode != "preview");
        result["afterCounts"] = await Counts(independent);
        result["independentReadbackVerified"] = readback.All(x => x.Existing);
        result["insertedRawSnapshots"] = inserted;
        result["insertedNormalizedRecords"] = prepared.Zip(entries).Where(x => !x.Second.Existing && input.Mode == "apply").Sum(x => x.First.Records.Length);
        result["strictNoOp"] = input.Mode == "apply" && inserted == 0;
        result["entries"] = entries.Zip(readback).Select(x => new
        {
            x.First.SourceId, x.First.DatasetId, x.First.Kind,
            inserted = input.Mode == "apply" && !x.First.Existing,
            existing = x.First.Existing, x.Second.RawSnapshotId, x.Second.NormalizedCount,
            verified = x.Second.Existing,
        }).ToArray();
    }
    private static async Task<object> Counts(PublicDataIngestionDbContext db) => new
    {
        rawSnapshots = await db.RawSnapshots.CountAsync(), ingestionRuns = await db.IngestionRuns.CountAsync(),
        normalizedRecords = await db.NormalizedRecords.CountAsync(), regionMappings = await db.RegionMappings.CountAsync(),
    };
    private static async Task ValidateTimeSchema(PublicDataIngestionDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COUNT(*), MIN(DATETIME_PRECISION), MAX(DATETIME_PRECISION) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('public_data_ingestion_runs','public_data_raw_snapshots','public_data_normalized_records') AND DATA_TYPE='datetime'";
        await using var reader = await command.ExecuteReaderAsync();
        ImportGuards.Require(await reader.ReadAsync() && reader.GetInt64(0) == 10
            && !reader.IsDBNull(1) && !reader.IsDBNull(2) && reader.GetInt32(1) == 6 && reader.GetInt32(2) == 6,
            "ImportDatabaseTimePrecisionMismatch");
    }
    private static string Summary(PreparedEntry item)
        => $"ObserverPublicDataDb;Kind={item.Entry.Kind};RecordsSHA256={item.Entry.RecordsSha256?.ToLowerInvariant() ?? "none"};OfficialUrlSHA256={Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(item.Entry.OfficialUrl))).ToLowerInvariant()};PrivateReview;NoCollection;NoPackOrGame";
    private static 외부데이터RawSnapshot CreateRaw(PreparedEntry item)
    {
        var entry = item.Entry; var now = DateTimeOffset.UtcNow;
        now = new DateTimeOffset(now.Ticks - now.Ticks % 10, TimeSpan.Zero);
        var run = new 외부데이터수집Run
        {
            RunKey = $"observer-db:{now:yyyyMMddHHmmssfff}:{Guid.NewGuid():N}",
            SourceId = entry.SourceId, DatasetId = entry.DatasetId, StatusCode = 외부데이터수집StatusCodes.Success,
            StartedAtUtc = now, CompletedAtUtc = now, AttemptCount = 1, FetchedCount = 1,
            NormalizedCount = item.Records.Length, InsertedCount = item.Records.Length,
            SourceVersion = entry.SourceVersion, DataRevision = entry.DataRevision, ErrorCode = "", ErrorSummary = Summary(item),
        };
        return new 외부데이터RawSnapshot
        {
            FirstCollectionRun = run, SourceId = entry.SourceId, DatasetId = entry.DatasetId,
            SourceVersion = entry.SourceVersion, CollectedAtUtc = entry.CollectedAtUtc, EvidenceAsOfUtc = entry.EvidenceAsOfUtc,
            ContentHashSha256 = entry.RawSha256.ToLowerInvariant(), ContentLength = item.RawBytes,
            ContentType = entry.ContentType, OriginalFileName = Path.GetFileName(entry.RawPath),
            StorageContainer = Storage, StorageObjectName = entry.RawPath, StorageLocation = "private-file://" + entry.RawPath,
            FirstSeenAtUtc = entry.CollectedAtUtc, LastSeenAtUtc = entry.CollectedAtUtc,
        };
    }
    private static 외부데이터정규화Record ToEntity(ImportRecord record, ImportEntry entry, long rawId) => new()
    {
        RawSnapshotId = rawId, RecordKey = record.RecordKey.ToLowerInvariant(), StableId = record.StableId,
        SourceId = entry.SourceId, DatasetId = entry.DatasetId, RegionStableId = record.RegionStableId,
        MetricCode = record.MetricCode, NumericValue = record.NumericValue, TextValue = record.TextValue,
        UnitCode = record.UnitCode, EvidenceAsOfUtc = record.EvidenceAsOfUtc, CollectedAtUtc = record.CollectedAtUtc,
        SpatialPrecisionCode = record.SpatialPrecisionCode, TemporalPrecisionCode = record.TemporalPrecisionCode,
        QualityCode = record.QualityCode, LimitationCode = record.LimitationCode, DimensionKey = record.DimensionKey,
        SourceVersion = record.SourceVersion, DataRevision = record.DataRevision,
        FirstSeenAtUtc = record.CollectedAtUtc, LastSeenAtUtc = record.CollectedAtUtc,
    };
    internal static bool SameRecord(외부데이터정규화Record stored, ImportRecord record, ImportEntry entry, long rawId)
    {
        var value = ToEntity(record, entry, rawId);
        return stored.RawSnapshotId == value.RawSnapshotId && stored.RecordKey == value.RecordKey && stored.StableId == value.StableId
            && stored.SourceId == value.SourceId && stored.DatasetId == value.DatasetId && stored.RegionStableId == value.RegionStableId
            && stored.MetricCode == value.MetricCode && stored.NumericValue == value.NumericValue && stored.TextValue == value.TextValue
            && stored.UnitCode == value.UnitCode && stored.EvidenceAsOfUtc == value.EvidenceAsOfUtc && stored.CollectedAtUtc == value.CollectedAtUtc
            && stored.SpatialPrecisionCode == value.SpatialPrecisionCode && stored.TemporalPrecisionCode == value.TemporalPrecisionCode
            && stored.QualityCode == value.QualityCode && stored.LimitationCode == value.LimitationCode && stored.DimensionKey == value.DimensionKey
            && stored.SourceVersion == value.SourceVersion && stored.DataRevision == value.DataRevision
            && stored.FirstSeenAtUtc == value.FirstSeenAtUtc && stored.LastSeenAtUtc == value.LastSeenAtUtc;
    }
    private static async Task<List<EntryResult>> Inspect(PublicDataIngestionDbContext db, List<PreparedEntry> entries, bool required)
    {
        var result = new List<EntryResult>();
        foreach (var item in entries)
        {
            var entry = item.Entry; var hash = entry.RawSha256.ToLowerInvariant();
            var raw = await db.RawSnapshots.AsNoTracking().Include(x => x.FirstCollectionRun).SingleOrDefaultAsync(x =>
                x.SourceId == entry.SourceId && x.DatasetId == entry.DatasetId && x.ContentHashSha256 == hash);
            if (raw is null)
            {
                ImportGuards.Require(!required, "ImportReadbackMissing");
                foreach (var chunk in item.Records.Chunk(500))
                {
                    var keys = chunk.Select(x => x.RecordKey.ToLowerInvariant()).ToList();
                    ImportGuards.Require(!await db.NormalizedRecords.AsNoTracking().AnyAsync(x => keys.Contains(x.RecordKey)), "ImportExistingRecordKeyConflict");
                }
                result.Add(new(entry.SourceId, entry.DatasetId, entry.Kind, false, null, item.Records.Length));
                continue;
            }
            var run = raw.FirstCollectionRun;
            ImportGuards.Require(raw.SourceId == entry.SourceId && raw.DatasetId == entry.DatasetId
                && raw.SourceVersion == entry.SourceVersion && raw.CollectedAtUtc == entry.CollectedAtUtc
                && raw.EvidenceAsOfUtc == entry.EvidenceAsOfUtc && raw.ContentHashSha256 == hash && raw.ContentLength == item.RawBytes
                && raw.ContentType == entry.ContentType && raw.OriginalFileName == Path.GetFileName(entry.RawPath)
                && raw.StorageContainer == Storage && raw.StorageObjectName == entry.RawPath && raw.StorageLocation == "private-file://" + entry.RawPath
                && raw.FirstSeenAtUtc == entry.CollectedAtUtc && raw.LastSeenAtUtc == entry.CollectedAtUtc
                && run is not null && run.SourceId == entry.SourceId && run.DatasetId == entry.DatasetId
                && run.SourceVersion == entry.SourceVersion && run.DataRevision == entry.DataRevision
                && run.StatusCode == 외부데이터수집StatusCodes.Success && run.ErrorCode == ""
                && run.NormalizedCount == item.Records.Length && run.ErrorSummary == Summary(item), "ImportExistingRawBindingConflict");
            var stored = await db.NormalizedRecords.AsNoTracking().Where(x => x.RawSnapshotId == raw.Id)
                .Take(ImportGuards.MaxRecords + 1).ToListAsync();
            ImportGuards.Require(stored.Count == item.Records.Length, "ImportExistingRecordSetConflict");
            var indexed = stored.ToDictionary(x => x.RecordKey, StringComparer.Ordinal);
            foreach (var record in item.Records)
                ImportGuards.Require(indexed.TryGetValue(record.RecordKey.ToLowerInvariant(), out var found)
                    && SameRecord(found, record, entry, raw.Id), "ImportExistingRecordBindingConflict");
            result.Add(new(entry.SourceId, entry.DatasetId, entry.Kind, true, raw.Id, stored.Count));
        }
        return result;
    }
    private sealed record EntryResult(string SourceId, string DatasetId, string Kind, bool Existing, long? RawSnapshotId, int NormalizedCount);
    private sealed class ReadOnlyImportInterceptor : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
            => throw new ImportGuardException("ImportReadOnlyWriteForbidden");
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => throw new ImportGuardException("ImportReadOnlyWriteForbidden");
    }
}
