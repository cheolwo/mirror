using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Ssalddel.Application.WorldProjection;
using Ssalddel.WorkflowRules.Contracts;
using StackExchange.Redis;

namespace Ssalddel.Services.Development.ObservableOperations;

public sealed class 관찰운영검증DbContext(DbContextOptions<관찰운영검증DbContext> options)
    : DbContext(options)
{
    public DbSet<관찰운영검증실행Record> Runs => Set<관찰운영검증실행Record>();
    public DbSet<관찰운영검증사례Record> Cases => Set<관찰운영검증사례Record>();
    public DbSet<관찰운영검증단계Record> Steps => Set<관찰운영검증단계Record>();
    public DbSet<관찰운영검증OutboxRecord> Outbox => Set<관찰운영검증OutboxRecord>();
    public DbSet<관찰운영검증Fixture묶음Record> FixturePacks => Set<관찰운영검증Fixture묶음Record>();
    public DbSet<관찰운영검증Fixture결속Record> FixtureBindings => Set<관찰운영검증Fixture결속Record>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<관찰운영검증실행Record>(entity =>
        {
            entity.ToTable("관찰운영검증_실행");
            entity.HasKey(x => x.RunStableId);
            entity.Property(x => x.RunStableId).HasMaxLength(96);
            entity.Property(x => x.StatusCode).HasMaxLength(24);
            entity.Property(x => x.AreaStableId).HasMaxLength(128);
            entity.Property(x => x.FixturePackStableId).HasMaxLength(128);
            entity.Property(x => x.FixtureHashSha256).HasMaxLength(64);
            entity.Property(x => x.FixtureRevision).HasMaxLength(48);
            entity.HasIndex(x => x.CreatedAtUtc);
        });
        modelBuilder.Entity<관찰운영검증사례Record>(entity =>
        {
            entity.ToTable("관찰운영검증_사례");
            entity.HasKey(x => new { x.RunStableId, x.CaseCode });
            entity.Property(x => x.RunStableId).HasMaxLength(96);
            entity.Property(x => x.CaseCode).HasMaxLength(64);
            entity.Property(x => x.OperatingSystemId).HasMaxLength(64);
            entity.Property(x => x.WorkStableId).HasMaxLength(196);
            entity.Property(x => x.LifecycleStageCode).HasMaxLength(96);
            entity.Property(x => x.AttentionStateCode).HasMaxLength(32);
            entity.Property(x => x.ObjectKindCode).HasMaxLength(64);
            entity.Property(x => x.ItemKind).HasMaxLength(64);
            entity.Property(x => x.RoleCode).HasMaxLength(64);
            entity.Property(x => x.SemanticPlaceStableId).HasMaxLength(160);
            entity.Property(x => x.StateCode).HasMaxLength(24);
            entity.Property(x => x.RelationStableIdsJson).HasColumnType("json");
            entity.HasIndex(x => new { x.RunStableId, x.ScheduledOffsetSeconds });
        });
        modelBuilder.Entity<관찰운영검증OutboxRecord>(entity =>
        {
            entity.ToTable("관찰운영검증_Outbox");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventStableId).HasMaxLength(220);
            entity.Property(x => x.RunStableId).HasMaxLength(96);
            entity.Property(x => x.CaseCode).HasMaxLength(64);
            entity.Property(x => x.EventTypeCode).HasMaxLength(64);
            entity.Property(x => x.StatusCode).HasMaxLength(24);
            entity.Property(x => x.PayloadJson).HasColumnType("json");
            entity.Property(x => x.LastErrorCode).HasMaxLength(96);
            entity.HasIndex(x => x.EventStableId).IsUnique();
            entity.HasIndex(x => new { x.StatusCode, x.NextAttemptAtUtc });
        });
        modelBuilder.Entity<관찰운영검증단계Record>(entity =>
        {
            entity.ToTable("관찰운영검증_단계");
            entity.HasKey(x => new { x.RunStableId, x.CaseCode, x.StepSequence });
            entity.Property(x => x.RunStableId).HasMaxLength(96);
            entity.Property(x => x.CaseCode).HasMaxLength(64);
            entity.Property(x => x.OperatingSystemId).HasMaxLength(64);
            entity.Property(x => x.WorkStableId).HasMaxLength(196);
            entity.Property(x => x.LifecycleStageCode).HasMaxLength(96);
            entity.Property(x => x.AttentionStateCode).HasMaxLength(32);
            entity.Property(x => x.ObjectKindCode).HasMaxLength(64);
            entity.Property(x => x.ItemKind).HasMaxLength(64);
            entity.Property(x => x.RoleCode).HasMaxLength(64);
            entity.Property(x => x.SemanticPlaceStableId).HasMaxLength(160);
            entity.Property(x => x.RelationStableIdsJson).HasColumnType("json");
            entity.Property(x => x.ExpectedStateCode).HasMaxLength(96);
            entity.Property(x => x.FailureOrRecoveryCode).HasMaxLength(96);
            entity.Property(x => x.ReturnStateCode).HasMaxLength(96);
            entity.Property(x => x.SourceKindCode).HasMaxLength(48);
            entity.Property(x => x.FixtureRevision).HasMaxLength(48);
            entity.Property(x => x.StepHashSha256).HasMaxLength(64);
            entity.Property(x => x.StateCode).HasMaxLength(24);
            entity.HasIndex(x => new { x.RunStableId, x.ScheduledOffsetMilliseconds });
            entity.HasIndex(x => new { x.RunStableId, x.CaseCode, x.Revision }).IsUnique();
        });
        modelBuilder.Entity<관찰운영검증Fixture묶음Record>(entity =>
        {
            entity.ToTable("관찰운영검증_Fixture묶음");
            entity.HasKey(x => x.FixturePackStableId);
            entity.Property(x => x.FixturePackStableId).HasMaxLength(128);
            entity.Property(x => x.FixtureRevision).HasMaxLength(48);
            entity.Property(x => x.FixtureHashSha256).HasMaxLength(64);
            entity.Property(x => x.InputHashSha256).HasMaxLength(64);
            entity.Property(x => x.SourceRevision).HasMaxLength(96);
            entity.Property(x => x.DeterministicSeed).HasMaxLength(128);
            entity.Property(x => x.SourceKindCode).HasMaxLength(48);
            entity.Property(x => x.EnvironmentCode).HasMaxLength(48);
            entity.Property(x => x.GeneratorVersion).HasMaxLength(48);
        });
        modelBuilder.Entity<관찰운영검증Fixture결속Record>(entity =>
        {
            entity.ToTable("관찰운영검증_Fixture결속");
            entity.HasKey(x => new { x.FixturePackStableId, x.FixtureObjectStableId });
            entity.Property(x => x.FixturePackStableId).HasMaxLength(128);
            entity.Property(x => x.FixtureObjectStableId).HasMaxLength(196);
            entity.Property(x => x.ObjectKindCode).HasMaxLength(64);
            entity.Property(x => x.PublicBusinessObservationStableId).HasMaxLength(196);
            entity.Property(x => x.LocationAnchorStableId).HasMaxLength(196);
            entity.Property(x => x.BuildingStableId).HasMaxLength(160);
            entity.Property(x => x.SemanticPlaceStableId).HasMaxLength(160);
            entity.Property(x => x.BindingPurposeCode).HasMaxLength(64);
            entity.Property(x => x.AffiliationCode).HasMaxLength(64);
            entity.Property(x => x.DisplayDisclosureCode).HasMaxLength(96);
            entity.Property(x => x.ReviewStatusCode).HasMaxLength(48);
            entity.Property(x => x.BindingHashSha256).HasMaxLength(64);
            entity.HasIndex(x => x.RestaurantProfileId);
            entity.HasIndex(x => x.MenuId);
        });
    }
}

public sealed class 관찰운영검증실행Record
{
    public string RunStableId { get; set; } = string.Empty;
    public string StatusCode { get; set; } = 관찰운영검증상태Codes.Idle;
    public string AreaStableId { get; set; } = string.Empty;
    public string FixturePackStableId { get; set; } = string.Empty;
    public string FixtureHashSha256 { get; set; } = string.Empty;
    public string FixtureRevision { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }
    public int ElapsedBeforeResumeSeconds { get; set; }
    public DateTime? ResumedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public long Revision { get; set; }
}

public sealed class 관찰운영검증사례Record
{
    public string RunStableId { get; set; } = string.Empty;
    public string CaseCode { get; set; } = string.Empty;
    public string OperatingSystemId { get; set; } = string.Empty;
    public string WorkStableId { get; set; } = string.Empty;
    public string LifecycleStageCode { get; set; } = string.Empty;
    public string AttentionStateCode { get; set; } = string.Empty;
    public string ObjectKindCode { get; set; } = string.Empty;
    public string ItemKind { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
    public string SemanticPlaceStableId { get; set; } = string.Empty;
    public string RelationStableIdsJson { get; set; } = "[]";
    public string StateCode { get; set; } = 관찰운영검증상태Codes.Scheduled;
    public int ScheduledOffsetSeconds { get; set; }
    public long Revision { get; set; }
    public DateTime? OccurredAtUtc { get; set; }
}

/// <summary>한 사례가 같은 WorkStableId로 진행하는 불변 revision 단계입니다.</summary>
public sealed class 관찰운영검증단계Record
{
    public string RunStableId { get; set; } = string.Empty;
    public string CaseCode { get; set; } = string.Empty;
    public int StepSequence { get; set; }
    public string OperatingSystemId { get; set; } = string.Empty;
    public string WorkStableId { get; set; } = string.Empty;
    public string LifecycleStageCode { get; set; } = string.Empty;
    public string AttentionStateCode { get; set; } = string.Empty;
    public string ObjectKindCode { get; set; } = string.Empty;
    public string ItemKind { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
    public string SemanticPlaceStableId { get; set; } = string.Empty;
    public string RelationStableIdsJson { get; set; } = "[]";
    public string ExpectedStateCode { get; set; } = string.Empty;
    public string FailureOrRecoveryCode { get; set; } = string.Empty;
    public string ReturnStateCode { get; set; } = string.Empty;
    public string SourceKindCode { get; set; } = OperationalWorldSceneSourceKinds.VerificationSample;
    public string FixtureRevision { get; set; } = string.Empty;
    public string StepHashSha256 { get; set; } = string.Empty;
    public string StateCode { get; set; } = 관찰운영검증상태Codes.Scheduled;
    public int ScheduledOffsetMilliseconds { get; set; }
    public long Revision { get; set; }
    public DateTime? OccurredAtUtc { get; set; }
}

public sealed class 관찰운영검증OutboxRecord
{
    public long Id { get; set; }
    public string EventStableId { get; set; } = string.Empty;
    public string RunStableId { get; set; } = string.Empty;
    public string CaseCode { get; set; } = string.Empty;
    public string EventTypeCode { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public string StatusCode { get; set; } = 관찰운영검증상태Codes.Pending;
    public int AttemptCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public string LastErrorCode { get; set; } = string.Empty;
}

public sealed class 관찰운영검증Fixture묶음Record
{
    public string FixturePackStableId { get; set; } = string.Empty;
    public string FixtureRevision { get; set; } = string.Empty;
    public string FixtureHashSha256 { get; set; } = string.Empty;
    public string InputHashSha256 { get; set; } = string.Empty;
    public string SourceRevision { get; set; } = string.Empty;
    public string DeterministicSeed { get; set; } = string.Empty;
    public string SourceKindCode { get; set; } = string.Empty;
    public string EnvironmentCode { get; set; } = string.Empty;
    public bool DistributionApproved { get; set; }
    public bool OperationalEffectsAllowed { get; set; }
    public string GeneratorVersion { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}

public sealed class 관찰운영검증Fixture결속Record
{
    public string FixturePackStableId { get; set; } = string.Empty;
    public string FixtureObjectStableId { get; set; } = string.Empty;
    public string ObjectKindCode { get; set; } = string.Empty;
    public long RestaurantProfileId { get; set; }
    public long? MenuId { get; set; }
    public string PublicBusinessObservationStableId { get; set; } = string.Empty;
    public string LocationAnchorStableId { get; set; } = string.Empty;
    public string BuildingStableId { get; set; } = string.Empty;
    public string SemanticPlaceStableId { get; set; } = string.Empty;
    public string BindingPurposeCode { get; set; } = string.Empty;
    public string AffiliationCode { get; set; } = string.Empty;
    public string DisplayDisclosureCode { get; set; } = string.Empty;
    public bool ScenarioOrderAllowed { get; set; }
    public bool ActualOrderAllowed { get; set; }
    public bool DistributionApproved { get; set; }
    public string ReviewStatusCode { get; set; } = string.Empty;
    public long Revision { get; set; }
    public string BindingHashSha256 { get; set; } = string.Empty;
}

public interface I관찰운영검증ProjectionWriter
{
    Task UpsertAsync(OperationalWorldSceneItem item, CancellationToken cancellationToken);
}

public sealed class 관찰운영검증ProjectionDocument
{
    [BsonId]
    public string SnapshotStableId { get; set; } = string.Empty;
    public string CaseCode { get; set; } = string.Empty;
    public string AreaStableId { get; set; } = string.Empty;
    public string OperatingSystemId { get; set; } = string.Empty;
    public string ItemKind { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
    public string ActivityCode { get; set; } = string.Empty;
    public long Revision { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public DateTime PublishedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string WorkStableId { get; set; } = string.Empty;
    public string LifecycleStageCode { get; set; } = string.Empty;
    public string AttentionStateCode { get; set; } = string.Empty;
    public string ObjectKindCode { get; set; } = string.Empty;
    public string SemanticPlaceStableId { get; set; } = string.Empty;
    public string[] RelationStableIds { get; set; } = Array.Empty<string>();
    public string ScenarioRunStableId { get; set; } = string.Empty;
    public int StepSequence { get; set; }
    public string ExpectedStateCode { get; set; } = string.Empty;
    public string FailureOrRecoveryCode { get; set; } = string.Empty;
    public string ReturnStateCode { get; set; } = string.Empty;
    public string StepHashSha256 { get; set; } = string.Empty;
    public string TimelineFixtureRevision { get; set; } = string.Empty;
    public string ProjectionHashSha256 { get; set; } = string.Empty;
}

public sealed class Mongo관찰운영검증ProjectionStore :
    I관찰운영검증ProjectionWriter,
    I관찰운영검증ProjectionReader
{
    private readonly IMongoCollection<관찰운영검증ProjectionDocument> collection;

    public Mongo관찰운영검증ProjectionStore(
        IMongoClient client,
        관찰운영검증Options options)
    {
        collection = client.GetDatabase(관찰운영검증Options.DatabaseName)
            .GetCollection<관찰운영검증ProjectionDocument>("observable_operations_scene_projection");
    }

    public async Task UpsertAsync(OperationalWorldSceneItem item, CancellationToken cancellationToken)
    {
        var document = CreateProjectionDocument(item);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var existing = await collection.Find(x => x.SnapshotStableId == document.SnapshotStableId)
                .FirstOrDefaultAsync(cancellationToken);
            if (existing is null)
            {
                try
                {
                    await collection.InsertOneAsync(document, cancellationToken: cancellationToken);
                    return;
                }
                catch (MongoWriteException exception) when (
                    exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
                {
                    continue;
                }
            }

            if (!ShouldReplace(existing, document)) return;

            var result = await collection.ReplaceOneAsync(
                x => x.SnapshotStableId == document.SnapshotStableId && x.Revision == existing.Revision,
                document,
                cancellationToken: cancellationToken);
            if (result.ModifiedCount == 1) return;
        }

        throw new InvalidOperationException("ObservableOperationsProjectionConcurrentWriteConflict");
    }

    internal static bool ShouldReplace(
        관찰운영검증ProjectionDocument existing,
        관찰운영검증ProjectionDocument incoming)
    {
        if (!string.Equals(existing.SnapshotStableId, incoming.SnapshotStableId, StringComparison.Ordinal))
            throw new InvalidOperationException("ObservableOperationsProjectionIdentityMismatch");
        if (existing.Revision > incoming.Revision)
            throw new InvalidOperationException("ObservableOperationsProjectionRevisionStale");
        if (existing.Revision < incoming.Revision) return true;

        var existingHash = string.IsNullOrWhiteSpace(existing.ProjectionHashSha256)
            ? ProjectionHash(existing)
            : existing.ProjectionHashSha256;
        if (string.Equals(existingHash, incoming.ProjectionHashSha256, StringComparison.Ordinal)) return false;
        throw new InvalidOperationException("ObservableOperationsProjectionRevisionConflict");
    }

    internal static 관찰운영검증ProjectionDocument CreateProjectionDocument(OperationalWorldSceneItem item)
    {
        var metadata = ReadRepresentationMetadata(item.RepresentationDataJson);
        var document = new 관찰운영검증ProjectionDocument
        {
            SnapshotStableId = item.SnapshotStableId,
            CaseCode = metadata.CaseCode,
            AreaStableId = item.AreaStableId,
            OperatingSystemId = item.OperatingSystemId,
            ItemKind = item.ItemKind,
            RoleCode = item.RoleCode,
            ActivityCode = item.ActivityCode,
            Revision = item.Revision,
            OccurredAtUtc = item.OccurredAtUtc,
            PublishedAtUtc = item.PublishedAtUtc,
            ExpiresAtUtc = item.ExpiresAtUtc,
            WorkStableId = item.WorkStableId,
            LifecycleStageCode = item.LifecycleStageCode,
            AttentionStateCode = item.AttentionStateCode,
            ObjectKindCode = item.ObjectKindCode,
            SemanticPlaceStableId = item.SemanticPlaceStableId,
            RelationStableIds = item.RelationStableIds,
            ScenarioRunStableId = item.ScenarioRunStableId,
            StepSequence = metadata.StepSequence,
            ExpectedStateCode = metadata.ExpectedStateCode,
            FailureOrRecoveryCode = metadata.FailureOrRecoveryCode,
            ReturnStateCode = metadata.ReturnStateCode,
            StepHashSha256 = metadata.StepHashSha256,
            TimelineFixtureRevision = metadata.TimelineFixtureRevision
        };
        document.ProjectionHashSha256 = ProjectionHash(document);
        return document;
    }

    public async Task<OperationalWorldSceneItem[]> 지역목록Async(
        string areaStableId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var candidates = await collection.Find(x =>
                x.AreaStableId == areaStableId
                && x.ExpiresAtUtc > utcNow
                && x.ScenarioRunStableId != string.Empty)
            .SortByDescending(x => x.PublishedAtUtc)
            .ThenByDescending(x => x.ScenarioRunStableId)
            .Limit(100)
            .ToListAsync(cancellationToken);
        var latestRunStableId = SelectLatestValidScenarioRunStableId(candidates, areaStableId, utcNow);
        if (string.IsNullOrWhiteSpace(latestRunStableId)) return Array.Empty<OperationalWorldSceneItem>();

        var documents = await collection.Find(x =>
                x.AreaStableId == areaStableId
                && x.ExpiresAtUtc > utcNow
                && x.ScenarioRunStableId == latestRunStableId)
            .SortBy(x => x.PublishedAtUtc)
            .ThenBy(x => x.SnapshotStableId)
            .Limit(100)
            .ToListAsync(cancellationToken);
        return documents.Select(ToSceneItem).ToArray();
    }

    internal static string SelectLatestValidScenarioRunStableId(
        IEnumerable<관찰운영검증ProjectionDocument> documents,
        string areaStableId,
        DateTime utcNow)
        => documents
            .Where(item => string.Equals(item.AreaStableId, areaStableId, StringComparison.Ordinal)
                           && item.ExpiresAtUtc > utcNow
                           && !string.IsNullOrWhiteSpace(item.ScenarioRunStableId))
            .OrderByDescending(item => item.PublishedAtUtc)
            .ThenByDescending(item => item.ScenarioRunStableId, StringComparer.Ordinal)
            .Select(item => item.ScenarioRunStableId)
            .FirstOrDefault() ?? string.Empty;

    internal static OperationalWorldSceneItem ToSceneItem(관찰운영검증ProjectionDocument x)
        => new()
        {
            SnapshotStableId = x.SnapshotStableId,
            AreaStableId = x.AreaStableId,
            OperatingSystemId = x.OperatingSystemId,
            ItemKind = x.ItemKind,
            RoleCode = x.RoleCode,
            ActivityCode = x.ActivityCode,
            Revision = x.Revision,
            OccurredAtUtc = x.OccurredAtUtc,
            PublishedAtUtc = x.PublishedAtUtc,
            ExpiresAtUtc = x.ExpiresAtUtc,
            DataPolicyCode = OperationalWorldScenePolicy.OnlineEphemeral,
            LocalStorageAllowed = false,
            ReplayAllowed = false,
            RepresentationDataJson = JsonSerializer.Serialize(new
            {
                caseCode = x.CaseCode,
                stepSequence = x.StepSequence,
                expectedStateCode = x.ExpectedStateCode,
                failureOrRecoveryCode = x.FailureOrRecoveryCode,
                returnStateCode = x.ReturnStateCode,
                stepHashSha256 = x.StepHashSha256,
                fixtureRevision = x.TimelineFixtureRevision,
                synthetic = true,
                exactPositionIncluded = false,
                personalDataIncluded = false
            }),
            WorkStableId = x.WorkStableId,
            LifecycleStageCode = x.LifecycleStageCode,
            AttentionStateCode = x.AttentionStateCode,
            ObjectKindCode = x.ObjectKindCode,
            SemanticPlaceStableId = x.SemanticPlaceStableId,
            RelationStableIds = x.RelationStableIds,
            SourceKindCode = OperationalWorldSceneSourceKinds.VerificationSample,
            ScenarioRunStableId = x.ScenarioRunStableId
        };

    private static 관찰운영검증표현Metadata ReadRepresentationMetadata(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            return new 관찰운영검증표현Metadata(
                ReadString(root, "caseCode"),
                ReadInt32(root, "stepSequence"),
                ReadString(root, "expectedStateCode"),
                ReadString(root, "failureOrRecoveryCode"),
                ReadString(root, "returnStateCode"),
                ReadString(root, "stepHashSha256"),
                ReadString(root, "fixtureRevision"));
        }
        catch (JsonException)
        {
            return new 관찰운영검증표현Metadata(
                string.Empty, 0, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        }
    }

    private static string ReadString(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) ? value.GetString() ?? string.Empty : string.Empty;

    private static int ReadInt32(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : 0;

    private static string ProjectionHash(관찰운영검증ProjectionDocument document)
    {
        var canonical = string.Join('\n',
            document.SnapshotStableId,
            document.CaseCode,
            document.AreaStableId,
            document.OperatingSystemId,
            document.ItemKind,
            document.RoleCode,
            document.ActivityCode,
            document.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture),
            document.OccurredAtUtc.ToUniversalTime().Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture),
            document.PublishedAtUtc.ToUniversalTime().Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture),
            document.ExpiresAtUtc.ToUniversalTime().Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture),
            document.WorkStableId,
            document.LifecycleStageCode,
            document.AttentionStateCode,
            document.ObjectKindCode,
            document.SemanticPlaceStableId,
            string.Join('\u001f', document.RelationStableIds ?? Array.Empty<string>()),
            document.ScenarioRunStableId,
            document.StepSequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            document.ExpectedStateCode,
            document.FailureOrRecoveryCode,
            document.ReturnStateCode,
            document.StepHashSha256,
            document.TimelineFixtureRevision);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private sealed record 관찰운영검증표현Metadata(
        string CaseCode,
        int StepSequence,
        string ExpectedStateCode,
        string FailureOrRecoveryCode,
        string ReturnStateCode,
        string StepHashSha256,
        string TimelineFixtureRevision);
}

public interface I관찰운영검증RunStateWriter
{
    Task WriteAsync(관찰운영검증상태 state, CancellationToken cancellationToken);
}

public sealed class Redis관찰운영검증RunStateWriter(IConnectionMultiplexer connection)
    : I관찰운영검증RunStateWriter
{
    public async Task WriteAsync(관찰운영검증상태 state, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = "verification:observable-operations:" + state.RunStableId;
        var database = connection.GetDatabase();
        await database.HashSetAsync(key,
        [
            new HashEntry("status", state.StatusCode),
            new HashEntry("elapsedSeconds", state.ElapsedSeconds),
            new HashEntry("publishedCaseCount", state.PublishedCaseCount),
            new HashEntry("publishedStepCount", state.PublishedStepCount),
            new HashEntry("totalStepCount", state.TotalStepCount),
            new HashEntry("pendingOutboxCount", state.PendingOutboxCount),
            new HashEntry("failedOutboxCount", state.FailedOutboxCount),
            new HashEntry("fixtureStatus", state.FixtureStatusCode),
            new HashEntry("fixturePackStableId", state.FixturePackStableId),
            new HashEntry("fixtureHashSha256", state.FixtureHashSha256),
            new HashEntry("fixtureRevision", state.FixtureRevision)
        ]);
        await database.KeyExpireAsync(key, TimeSpan.FromHours(1));
    }
}
