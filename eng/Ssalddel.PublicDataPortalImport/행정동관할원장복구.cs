using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.DeliveryZones;
using Ssalddel.Contracts.Common.PublicData;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.DeliveryZones;
using 살뜰.Services.External.PublicData;
using 살뜰.Services.External.PublicData.Korea;

// 로컬 저장소에 보존된 2026-08-12 동결 원본을 공공자료 원장으로만 복구한다.
// 외부 호출, 최신 판본 대체, 운영 권역 확정 또는 공개 승격은 수행하지 않는다.
internal static class 행정동관할원장복구
{
    private const string ArchiveRelative =
        "Ssalddel/.local-private-storage/external-data/raw/mois-resident-registration-codes/" +
        "korea-administrative-legal-jurisdictions/2026-08-12/4a10e6ee2ec94f4c93638b7c0bd8ebe4.zip";
    private const string ArchiveHash =
        "8AF8C1F122D67D43518F58B37AEA6EEA7986F2809062F24E2E03465F21AE7A08";
    private const long ArchiveBytes = 2_129_571;
    private const string SourceVersion = "mois-jscode:20260301:retrieved:2026-08-12";
    private const string DataRevision = "mois-hjd-bjd-20260301-8af8c1f122d67d43518f";
    private const string RunKey = "recover-mois-hjd-bjd-20260301-8af8c1f122d67d43518f";
    private const int ExpectedAdministrativeRecordCount = 3_922;
    private const int ExpectedJurisdictionRecordCount = 21_817;
    private const int ExpectedNormalizedRecordCount =
        ExpectedAdministrativeRecordCount + ExpectedJurisdictionRecordCount;
    private static readonly DateTimeOffset CollectedAtUtc =
        DateTimeOffset.Parse("2026-08-12T23:31:48.2845587Z");
    private static readonly DateTimeOffset EvidenceAsOfUtc =
        DateTimeOffset.Parse("2026-03-01T00:00:00Z");

    internal static async Task RunAsync(
        string mode,
        string root,
        Dictionary<string, object?> result)
    {
        Require(mode is "preview" or "apply" or "verify",
            "AdministrativeJurisdictionLedgerRecoveryModeInvalid");
        var archivePath = Path.GetFullPath(Path.Combine(root, ArchiveRelative));
        Require(archivePath.StartsWith(root + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase),
            "AdministrativeJurisdictionLedgerRecoveryPathOutsideRoot");
        Require(File.Exists(archivePath), "AdministrativeJurisdictionFrozenArchiveMissing");
        Require(new FileInfo(archivePath).Length == ArchiveBytes,
            "AdministrativeJurisdictionFrozenArchiveLengthMismatch");
        await using (var input = File.OpenRead(archivePath))
        {
            Require(Convert.ToHexString(await SHA256.HashDataAsync(input)) == ArchiveHash,
                "AdministrativeJurisdictionFrozenArchiveHashMismatch");
        }

        var raw = CreateRawSnapshot(root, archivePath);
        var normalizer = new 대한민국행정동관할CodeNormalizer(
            Options.Create(new 대한민국행정동관할CodeOptions()));
        var source = new 대한민국행정동관할CodeSourceRegistration()
            .GetDefinitions().Single();
        var storage = new FrozenArchiveStorage(archivePath);
        var normalized = await normalizer.NormalizeAsync(source, raw, storage);
        Require(normalized.DataRevision == DataRevision,
            "AdministrativeJurisdictionFrozenRevisionMismatch");
        Require(normalized.Records.Count > 0
                && normalized.Records.All(item =>
                    item.SourceVersion == SourceVersion
                    && item.DataRevision == DataRevision),
            "AdministrativeJurisdictionFrozenNormalizationMismatch");
        Require(normalized.Records.Count == ExpectedNormalizedRecordCount
                && normalized.Records.Count(item =>
                    item.MetricCode == 대한민국행정동관할CodeDataset.AdministrativeMetricCode)
                == ExpectedAdministrativeRecordCount
                && normalized.Records.Count(item =>
                    item.MetricCode == 대한민국행정동관할CodeDataset.JurisdictionMetricCode)
                == ExpectedJurisdictionRecordCount,
            "AdministrativeJurisdictionFrozenRecordCountMismatch");

        result["stage"] = "ValidateFrozenAdministrativeJurisdictionLedger";
        result["mode"] = mode;
        result["sourceId"] = 대한민국행정동관할CodeDataset.SourceId;
        result["datasetId"] = 대한민국행정동관할CodeDataset.DatasetId;
        result["sourceVersion"] = SourceVersion;
        result["dataRevision"] = DataRevision;
        result["archiveSha256"] = ArchiveHash;
        result["archiveBytes"] = ArchiveBytes;
        result["normalizedRecordCount"] = normalized.Records.Count;
        result["administrativeRecordCount"] = normalized.Records.Count(item =>
            item.MetricCode == 대한민국행정동관할CodeDataset.AdministrativeMetricCode);
        result["jurisdictionRecordCount"] = normalized.Records.Count(item =>
            item.MetricCode == 대한민국행정동관할CodeDataset.JurisdictionMetricCode);
        result["externalRequestAttempted"] = false;
        result["publicationAttempted"] = false;

        if (mode == "preview")
        {
            result["databaseWriteAttempted"] = false;
            result["committed"] = false;
            result["stage"] = "Complete";
            return;
        }

        if (mode == "apply")
        {
            await ApplyAsync(root, raw, normalized, result);
        }

        await VerifyAsync(root, result);
        result["stage"] = "Complete";
    }

    private static async Task ApplyAsync(
        string root,
        외부데이터RawSnapshot raw,
        ExternalDataNormalizationBatch normalized,
        Dictionary<string, object?> result)
    {
        await using var db = new PublicDataIngestionDbContext(
            await 로컬공공자료Db.OptionsAsync(root));
        var existingRows = await db.NormalizedRecords
            .AsNoTracking()
            .Where(item => item.SourceId == 대한민국행정동관할CodeDataset.SourceId
                           && item.DatasetId == 대한민국행정동관할CodeDataset.DatasetId)
            .Select(item => new { item.SourceVersion, item.DataRevision })
            .ToListAsync();
        Require(existingRows.All(item =>
                item.SourceVersion == SourceVersion && item.DataRevision == DataRevision),
            "AdministrativeJurisdictionDifferentLedgerAlreadyStored");

        await using var transaction = await db.Database.BeginTransactionAsync();
        var store = new EfExternalDataIngestionStore(db);
        var storedRaw = await store.FindRawSnapshotAsync(
            대한민국행정동관할CodeDataset.SourceId,
            대한민국행정동관할CodeDataset.DatasetId,
            ArchiveHash.ToLowerInvariant());
        var rawInserted = false;
        외부데이터수집Run? run = null;
        if (storedRaw is null)
        {
            run = await db.IngestionRuns.SingleOrDefaultAsync(item => item.RunKey == RunKey);
            if (run is null)
            {
                run = await store.StartRunAsync(new 외부데이터수집Run
                {
                    RunKey = RunKey,
                    SourceId = 대한민국행정동관할CodeDataset.SourceId,
                    DatasetId = 대한민국행정동관할CodeDataset.DatasetId,
                    StatusCode = 외부데이터수집StatusCodes.Running,
                    StartedAtUtc = DateTimeOffset.UtcNow,
                    AttemptCount = 1,
                    SourceVersion = SourceVersion,
                    DataRevision = DataRevision,
                });
            }
            raw.FirstCollectionRunId = run.Id;
            storedRaw = await store.SaveRawSnapshotAsync(raw);
            rawInserted = true;
        }
        else
        {
            Require(storedRaw.SourceVersion == SourceVersion
                    && storedRaw.ContentLength == ArchiveBytes
                    && storedRaw.DataSetIdentityMatches(),
                "AdministrativeJurisdictionFrozenRawSnapshotConflict");
        }

        foreach (var record in normalized.Records)
            record.RawSnapshotId = storedRaw.Id;
        var saved = await store.UpsertNormalizedAsync(normalized.Records);
        Require(saved.UpdatedCount == 0,
            "AdministrativeJurisdictionFrozenLedgerUnexpectedUpdate");
        if (run is not null)
        {
            run.StatusCode = 외부데이터수집StatusCodes.Success;
            run.CompletedAtUtc = DateTimeOffset.UtcNow;
            run.FetchedCount = normalized.Records.Count;
            run.NormalizedCount = normalized.Records.Count;
            run.InsertedCount = saved.InsertedCount;
            run.ExistingCount = saved.ExistingCount;
            await store.CompleteRunAsync(run);
        }
        await transaction.CommitAsync();

        result["databaseWriteAttempted"] = true;
        result["committed"] = true;
        result["rawSnapshotInserted"] = rawInserted;
        result["recordsInserted"] = saved.InsertedCount;
        result["recordsVerifiedExisting"] = saved.ExistingCount;
    }

    private static async Task VerifyAsync(
        string root,
        Dictionary<string, object?> result)
    {
        await using var db = new PublicDataIngestionDbContext(
            await 로컬공공자료Db.OptionsAsync(root));
        var storedMetrics = await db.NormalizedRecords
            .AsNoTracking()
            .Where(item => item.SourceId == 대한민국행정동관할CodeDataset.SourceId
                           && item.DatasetId == 대한민국행정동관할CodeDataset.DatasetId
                           && item.SourceVersion == SourceVersion
                           && item.DataRevision == DataRevision)
            .Select(item => item.MetricCode)
            .ToListAsync();
        var storedAdministrative = storedMetrics.Count(item =>
            item == 대한민국행정동관할CodeDataset.AdministrativeMetricCode);
        var storedJurisdiction = storedMetrics.Count(item =>
            item == 대한민국행정동관할CodeDataset.JurisdictionMetricCode);
        Require(storedMetrics.Count == ExpectedNormalizedRecordCount
                && storedAdministrative == ExpectedAdministrativeRecordCount
                && storedJurisdiction == ExpectedJurisdictionRecordCount,
            "AdministrativeJurisdictionRecoveryStoredRecordCountMismatch");
        var storedRaw = await db.RawSnapshots.AsNoTracking().SingleOrDefaultAsync(item =>
            item.SourceId == 대한민국행정동관할CodeDataset.SourceId
            && item.DatasetId == 대한민국행정동관할CodeDataset.DatasetId
            && item.SourceVersion == SourceVersion
            && item.ContentHashSha256 == ArchiveHash.ToLowerInvariant());
        Require(storedRaw is not null
                && storedRaw.ContentLength == ArchiveBytes
                && storedRaw.DataSetIdentityMatches(),
            "AdministrativeJurisdictionRecoveryRawSnapshotMismatch");
        var areas = await new Official행정동배달운영권역Source(db)
            .조회Async(배달운영권역SourceScopes.NortheastSeoulRiderR1);
        Require(areas.Count == 행정동배달운영권역SourceScopeCatalog
                .NortheastSeoulRiderR1ExpectedAdministrativeDongCount,
            "AdministrativeJurisdictionRecoveryAreaCountMismatch");
        Require(areas.SelectMany(item => item.LegalAreas)
                    .Select(item => item.LegalAreaStableId)
                    .Distinct(StringComparer.Ordinal).Count() == 12,
            "AdministrativeJurisdictionRecoveryLegalAreaCountMismatch");
        result["independentReadbackVerified"] = true;
        result["storedRecordCount"] = storedMetrics.Count;
        result["storedAdministrativeRecordCount"] = storedAdministrative;
        result["storedJurisdictionRecordCount"] = storedJurisdiction;
        result["verifiedRawSnapshotCount"] = 1;
        result["scopeAdministrativeAreaCount"] = areas.Count;
        result["scopeLegalAreaCount"] = 12;
        if (!result.ContainsKey("databaseWriteAttempted"))
            result["databaseWriteAttempted"] = false;
        if (!result.ContainsKey("committed"))
            result["committed"] = false;
    }

    private static 외부데이터RawSnapshot CreateRawSnapshot(string root, string archivePath)
    {
        var storageRoot = Path.Combine(root, "Ssalddel", ".local-private-storage");
        var objectName = Path.GetRelativePath(storageRoot, archivePath)
            .Replace(Path.DirectorySeparatorChar, '/');
        Require(!objectName.StartsWith("..", StringComparison.Ordinal),
            "AdministrativeJurisdictionFrozenStoragePathInvalid");
        return new 외부데이터RawSnapshot
        {
            SourceId = 대한민국행정동관할CodeDataset.SourceId,
            DatasetId = 대한민국행정동관할CodeDataset.DatasetId,
            SourceVersion = SourceVersion,
            CollectedAtUtc = CollectedAtUtc,
            EvidenceAsOfUtc = EvidenceAsOfUtc,
            ContentHashSha256 = ArchiveHash.ToLowerInvariant(),
            ContentLength = ArchiveBytes,
            ContentType = "application/zip",
            OriginalFileName = "대한민국-행정기관-관할법정동-20260301.zip",
            StorageContainer = "development-private",
            StorageObjectName = objectName,
            StorageLocation = "local-storage-private://development-private/" + objectName,
            FirstSeenAtUtc = CollectedAtUtc,
            LastSeenAtUtc = CollectedAtUtc,
        };
    }

    private static bool DataSetIdentityMatches(this 외부데이터RawSnapshot snapshot)
        => snapshot.SourceId == 대한민국행정동관할CodeDataset.SourceId
           && snapshot.DatasetId == 대한민국행정동관할CodeDataset.DatasetId
           && snapshot.ContentHashSha256.Equals(
               ArchiveHash, StringComparison.OrdinalIgnoreCase);

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }

    private sealed class FrozenArchiveStorage(string archivePath) : IExternalDataRawStorage
    {
        public Task<ExternalDataRawStorageResult> StoreAsync(
            ExternalDataSourceDefinition source,
            ExternalDataCollectedPayload payload,
            DateTimeOffset collectedAtUtc,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("FrozenArchiveStorageReadOnly");

        public Task<Stream> OpenReadAsync(
            외부데이터RawSnapshot snapshot,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Stream>(new FileStream(
                archivePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                81920, FileOptions.Asynchronous | FileOptions.SequentialScan));
    }
}
