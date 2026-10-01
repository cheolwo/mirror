using System.Text.Json;
using Hongdal.Contracts.Common.Orderer;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using 홍달.Services.Options;

namespace Hongdal.Services.Orderer;

public sealed class MongoDomesticGroupPurchaseNegotiationStore
    : IDomesticGroupPurchaseNegotiationStore
{
    private const string CollectionName = "orderer_group_purchase_negotiations";
    private readonly IMongoCollection<DomesticGroupPurchaseNegotiationDocument> collection;
    private readonly SemaphoreSlim indexLock = new(1, 1);
    private bool indexesReady;

    public MongoDomesticGroupPurchaseNegotiationStore(
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
            .GetCollection<DomesticGroupPurchaseNegotiationDocument>(CollectionName);
    }

    public async Task<DomesticGroupPurchaseNegotiationCampaignState> GetOrCreateAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(campaignId, Guid.Empty);
        await EnsureIndexesAsync(cancellationToken);
        var document = await collection
            .Find(x => x.GroupPurchaseCampaignId == campaignId.ToString("N"))
            .FirstOrDefaultAsync(cancellationToken);
        return document is null ? new DomesticGroupPurchaseNegotiationCampaignState() : ToState(document);
    }

    public async Task SaveAsync(
        Guid campaignId,
        DomesticGroupPurchaseNegotiationCampaignState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(campaignId, Guid.Empty);
        ArgumentNullException.ThrowIfNull(state);
        await EnsureIndexesAsync(cancellationToken);

        var campaignKey = campaignId.ToString("N");
        DomesticGroupPurchaseNegotiationPayload payload;
        lock (state.SyncRoot)
        {
            payload = ToPayload(state);
        }

        var expectedRevision = state.Revision;
        var now = DateTime.UtcNow;
        var document = new DomesticGroupPurchaseNegotiationDocument
        {
            Id = campaignKey,
            GroupPurchaseCampaignId = campaignKey,
            Revision = expectedRevision + 1,
            PayloadJson = JsonSerializer.Serialize(payload),
            UpdatedAtUtc = now
        };
        var filter = Builders<DomesticGroupPurchaseNegotiationDocument>.Filter.And(
            Builders<DomesticGroupPurchaseNegotiationDocument>.Filter.Eq(x => x.GroupPurchaseCampaignId, campaignKey),
            Builders<DomesticGroupPurchaseNegotiationDocument>.Filter.Eq(x => x.Revision, expectedRevision));

        try
        {
            if (expectedRevision == 0)
            {
                var created = await collection.ReplaceOneAsync(
                    filter,
                    document,
                    new ReplaceOptions { IsUpsert = true },
                    cancellationToken);
                if (created.MatchedCount == 0 && created.UpsertedId is null)
                {
                    throw new InvalidOperationException("공동구매 협상 이력이 다른 요청에서 먼저 변경되었습니다.");
                }
            }
            else
            {
                var updated = await collection.ReplaceOneAsync(
                    filter,
                    document,
                    new ReplaceOptions { IsUpsert = false },
                    cancellationToken);
                if (updated.MatchedCount == 0)
                {
                    throw new InvalidOperationException("공동구매 협상 이력이 다른 요청에서 먼저 변경되었습니다.");
                }
            }
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new InvalidOperationException("공동구매 협상 이력이 다른 요청에서 먼저 생성되었습니다.", exception);
        }

        state.Revision = document.Revision;
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

            await collection.Indexes.CreateOneAsync(
                new CreateIndexModel<DomesticGroupPurchaseNegotiationDocument>(
                    Builders<DomesticGroupPurchaseNegotiationDocument>.IndexKeys
                        .Ascending(x => x.GroupPurchaseCampaignId),
                    new CreateIndexOptions
                    {
                        Unique = true,
                        Name = "ux_group_purchase_campaign"
                    }),
                cancellationToken: cancellationToken);
            indexesReady = true;
        }
        finally
        {
            indexLock.Release();
        }
    }

    private static DomesticGroupPurchaseNegotiationCampaignState ToState(
        DomesticGroupPurchaseNegotiationDocument document)
    {
        var payload = JsonSerializer.Deserialize<DomesticGroupPurchaseNegotiationPayload>(document.PayloadJson)
            ?? new DomesticGroupPurchaseNegotiationPayload();
        var state = new DomesticGroupPurchaseNegotiationCampaignState { Revision = document.Revision };
        state.Events.AddRange(payload.Events);
        state.Issues.AddRange(payload.Issues.Select(issue =>
        {
            var item = new DomesticGroupPurchaseNegotiationIssueState { PublicIssue = issue.PublicIssue };
            item.Positions.AddRange(issue.Positions.Select(position =>
                new DomesticGroupPurchaseNegotiationPositionState
                {
                    AuthorUserId = position.AuthorUserId,
                    PublicPosition = position.PublicPosition
                }));
            return item;
        }));
        return state;
    }

    private static DomesticGroupPurchaseNegotiationPayload ToPayload(
        DomesticGroupPurchaseNegotiationCampaignState state)
        => new()
        {
            Events = state.Events.ToList(),
            Issues = state.Issues.Select(issue => new DomesticGroupPurchaseNegotiationIssuePayload
            {
                PublicIssue = issue.PublicIssue,
                Positions = issue.Positions.Select(position => new DomesticGroupPurchaseNegotiationPositionPayload
                {
                    AuthorUserId = position.AuthorUserId,
                    PublicPosition = position.PublicPosition
                }).ToList()
            }).ToList()
        };
}

internal sealed class DomesticGroupPurchaseNegotiationDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    public string GroupPurchaseCampaignId { get; set; } = string.Empty;
    public long Revision { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
}

internal sealed class DomesticGroupPurchaseNegotiationPayload
{
    public List<DomesticGroupPurchaseNegotiationEventResponse> Events { get; set; } = [];
    public List<DomesticGroupPurchaseNegotiationIssuePayload> Issues { get; set; } = [];
}

internal sealed class DomesticGroupPurchaseNegotiationIssuePayload
{
    public DomesticGroupPurchaseNegotiationIssueResponse PublicIssue { get; set; } = new();
    public List<DomesticGroupPurchaseNegotiationPositionPayload> Positions { get; set; } = [];
}

internal sealed class DomesticGroupPurchaseNegotiationPositionPayload
{
    public string AuthorUserId { get; set; } = string.Empty;
    public DomesticGroupPurchaseDeliberationPositionResponse PublicPosition { get; set; } = new();
}
