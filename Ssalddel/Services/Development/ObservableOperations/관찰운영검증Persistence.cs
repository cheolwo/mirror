using System.Text.Json;
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
    public DbSet<관찰운영검증OutboxRecord> Outbox => Set<관찰운영검증OutboxRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<관찰운영검증실행Record>(entity =>
        {
            entity.ToTable("관찰운영검증_실행");
            entity.HasKey(x => x.RunStableId);
            entity.Property(x => x.RunStableId).HasMaxLength(96);
            entity.Property(x => x.StatusCode).HasMaxLength(24);
            entity.Property(x => x.AreaStableId).HasMaxLength(128);
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
    }
}

public sealed class 관찰운영검증실행Record
{
    public string RunStableId { get; set; } = string.Empty;
    public string StatusCode { get; set; } = 관찰운영검증상태Codes.Idle;
    public string AreaStableId { get; set; } = string.Empty;
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

    public Task UpsertAsync(OperationalWorldSceneItem item, CancellationToken cancellationToken)
    {
        var document = new 관찰운영검증ProjectionDocument
        {
            SnapshotStableId = item.SnapshotStableId,
            CaseCode = ReadCaseCode(item.RepresentationDataJson),
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
            ScenarioRunStableId = item.ScenarioRunStableId
        };
        return collection.ReplaceOneAsync(
            x => x.SnapshotStableId == document.SnapshotStableId,
            document,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task<OperationalWorldSceneItem[]> 지역목록Async(
        string areaStableId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var documents = await collection.Find(x =>
                x.AreaStableId == areaStableId && x.ExpiresAtUtc > utcNow)
            .SortBy(x => x.PublishedAtUtc)
            .ThenBy(x => x.SnapshotStableId)
            .Limit(100)
            .ToListAsync(cancellationToken);
        return documents.Select(x => new OperationalWorldSceneItem
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
                x.CaseCode,
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
        }).ToArray();
    }

    private static string ReadCaseCode(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("caseCode", out var value)
                ? value.GetString() ?? string.Empty
                : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }
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
            new HashEntry("pendingOutboxCount", state.PendingOutboxCount),
            new HashEntry("failedOutboxCount", state.FailedOutboxCount)
        ]);
        await database.KeyExpireAsync(key, TimeSpan.FromHours(1));
    }
}
