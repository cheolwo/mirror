using System.Text.Json;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using 홍달.Services.Options;

namespace Hongdal.Services.CollectiveProcurement;

public sealed class MongoCollectiveProcurementPlanningStore : ICollectiveProcurementPlanningStore
{
    private const string CollectionName = "collective_procurement_plans";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IMongoCollection<CollectiveProcurementPlanDocument> collection;
    private readonly SemaphoreSlim indexLock = new(1, 1);
    private bool indexesReady;

    public MongoCollectiveProcurementPlanningStore(
        IMongoClient mongoClient,
        IOptions<MongoDbOptions> options)
    {
        var databaseName = options.Value.Database;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("MongoDb:Database configuration is required.");
        }

        collection = mongoClient
            .GetDatabase(databaseName.Trim())
            .GetCollection<CollectiveProcurementPlanDocument>(CollectionName);
    }

    public async Task<CollectiveProcurementPlanState?> GetAsync(
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(planId, Guid.Empty);
        await EnsureIndexesAsync(cancellationToken);
        var planKey = planId.ToString("N");
        var document = await collection
            .Find(item => item.PlanId == planKey)
            .FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : Deserialize(document.PayloadJson);
    }

    public async Task<IReadOnlyList<CollectiveProcurementPlanState>> ListBySourceAsync(
        string sourceTypeCode,
        string sourceReferenceId,
        CancellationToken cancellationToken = default)
    {
        var sourceType = sourceTypeCode?.Trim() ?? string.Empty;
        var sourceReference = sourceReferenceId?.Trim() ?? string.Empty;
        if (sourceType.Length == 0 || sourceReference.Length == 0)
        {
            return [];
        }

        await EnsureIndexesAsync(cancellationToken);
        var documents = await collection
            .Find(item => item.SourceTypeCode == sourceType && item.SourceReferenceId == sourceReference)
            .SortByDescending(item => item.UpdatedAtUtc)
            .Limit(20)
            .ToListAsync(cancellationToken);
        return documents.Select(document => Deserialize(document.PayloadJson)).ToArray();
    }

    public async Task CreateAsync(
        CollectiveProcurementPlanState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        await EnsureIndexesAsync(cancellationToken);
        var document = ToDocument(state);
        try
        {
            await collection.InsertOneAsync(document, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new CollectiveProcurementPlanConcurrencyException(
                "공동조달계획이 다른 요청에서 먼저 생성되었습니다.",
                exception);
        }
    }

    public async Task SaveAsync(
        CollectiveProcurementPlanState state,
        long expectedPlanRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        await EnsureIndexesAsync(cancellationToken);
        var document = ToDocument(state);
        var filter = Builders<CollectiveProcurementPlanDocument>.Filter.And(
            Builders<CollectiveProcurementPlanDocument>.Filter.Eq(item => item.PlanId, document.PlanId),
            Builders<CollectiveProcurementPlanDocument>.Filter.Eq(item => item.PlanRevision, expectedPlanRevision));
        var result = await collection.ReplaceOneAsync(
            filter,
            document,
            new ReplaceOptions { IsUpsert = false },
            cancellationToken);
        if (result.MatchedCount == 0)
        {
            throw new CollectiveProcurementPlanConcurrencyException(
                "공동조달계획이 다른 참여자에 의해 먼저 변경되었습니다.");
        }
    }

    private async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        if (indexesReady)
        {
            return;
        }

        await indexLock.WaitAsync(cancellationToken);
        try
        {
            if (indexesReady)
            {
                return;
            }

            var indexes = new[]
            {
                new CreateIndexModel<CollectiveProcurementPlanDocument>(
                    Builders<CollectiveProcurementPlanDocument>.IndexKeys.Ascending(item => item.PlanId),
                    new CreateIndexOptions
                    {
                        Unique = true,
                        Name = "ux_collective_procurement_plan"
                    }),
                new CreateIndexModel<CollectiveProcurementPlanDocument>(
                    Builders<CollectiveProcurementPlanDocument>.IndexKeys
                        .Ascending(item => item.SourceTypeCode)
                        .Ascending(item => item.SourceReferenceId)
                        .Descending(item => item.UpdatedAtUtc),
                    new CreateIndexOptions { Name = "ix_collective_procurement_source" })
            };
            await collection.Indexes.CreateManyAsync(
                indexes,
                cancellationToken: cancellationToken);
            indexesReady = true;
        }
        finally
        {
            indexLock.Release();
        }
    }

    private static CollectiveProcurementPlanDocument ToDocument(CollectiveProcurementPlanState state)
    {
        var key = state.PlanId.ToString("N");
        return new CollectiveProcurementPlanDocument
        {
            Id = key,
            PlanId = key,
            PlanRevision = state.PlanRevision,
            OwnerUserId = state.OwnerUserId,
            SourceTypeCode = state.SourceTypeCode,
            SourceReferenceId = state.SourceReferenceId,
            PayloadJson = JsonSerializer.Serialize(state, JsonOptions),
            CreatedAtUtc = state.CreatedAtUtc.UtcDateTime,
            UpdatedAtUtc = state.UpdatedAtUtc.UtcDateTime
        };
    }

    private static CollectiveProcurementPlanState Deserialize(string payloadJson)
        => JsonSerializer.Deserialize<CollectiveProcurementPlanState>(payloadJson, JsonOptions)
           ?? throw new InvalidOperationException("저장된 공동조달계획을 읽을 수 없습니다.");
}

internal sealed class CollectiveProcurementPlanDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    public string PlanId { get; set; } = string.Empty;
    public long PlanRevision { get; set; }
    public string OwnerUserId { get; set; } = string.Empty;
    public string SourceTypeCode { get; set; } = string.Empty;
    public string SourceReferenceId { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
