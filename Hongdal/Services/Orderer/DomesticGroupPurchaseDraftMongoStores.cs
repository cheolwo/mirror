using System.Text.Json;
using Hongdal.Contracts.Common.Orderer;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using 홍달.Services.Options;

namespace Hongdal.Services.Orderer;

public sealed class MongoDomesticProducerContactRequestDraftStore
    : IDomesticProducerContactRequestDraftStore
{
    private readonly MongoGroupPurchaseDraftStore<DomesticProducerContactRequestDraftResponse> store;

    public MongoDomesticProducerContactRequestDraftStore(IMongoClient mongoClient, IOptions<MongoDbOptions> options)
    {
        store = new(mongoClient, options, "orderer_group_purchase_producer_contact_drafts");
    }

    public Task<DomesticProducerContactRequestDraftResponse> SaveAsync(
        DomesticProducerContactRequestDraftResponse draft,
        CancellationToken cancellationToken = default)
    {
        draft.IsDurablyPersisted = true;
        draft.GuidanceMessage = "생산자 연락 요청 초안을 영구 저장하고 공동구매 원장에 연결했습니다. 상대 수락 전에는 연락처를 공개하지 않습니다.";
        return store.SaveAsync(
            draft.DraftId,
            draft.GroupPurchaseCampaignId,
            draft.RequestedByUserId,
            draft,
            cancellationToken);
    }

    public Task<DomesticProducerContactRequestDraftResponse?> GetAsync(
        Guid draftId,
        CancellationToken cancellationToken = default)
        => store.GetAsync(draftId, cancellationToken);
}

public sealed class MongoDomesticProducerSupplyOfferDraftStore
    : IDomesticProducerSupplyOfferDraftStore
{
    private readonly MongoGroupPurchaseDraftStore<DomesticProducerSupplyOfferDraftResponse> store;

    public MongoDomesticProducerSupplyOfferDraftStore(IMongoClient mongoClient, IOptions<MongoDbOptions> options)
    {
        store = new(mongoClient, options, "orderer_group_purchase_producer_supply_offer_drafts");
    }

    public Task<DomesticProducerSupplyOfferDraftResponse> SaveAsync(
        DomesticProducerSupplyOfferDraftResponse draft,
        CancellationToken cancellationToken = default)
    {
        draft.IsDurablyPersisted = true;
        draft.GuidanceMessage = "생산자 공급 제안 초안을 영구 저장하고 공동구매 원장에 연결했습니다. 대표 수락 전에는 연락처 공개나 거래 확정으로 보지 않습니다.";
        return store.SaveAsync(
            draft.DraftId,
            draft.GroupPurchaseCampaignId,
            draft.OfferedByUserId,
            draft,
            cancellationToken);
    }

    public Task<DomesticProducerSupplyOfferDraftResponse?> GetAsync(
        Guid draftId,
        CancellationToken cancellationToken = default)
        => store.GetAsync(draftId, cancellationToken);
}

public sealed class MongoDomesticGroupPurchaseFulfillmentOrderDraftStore
    : IDomesticGroupPurchaseFulfillmentOrderDraftStore
{
    private readonly MongoGroupPurchaseDraftStore<DomesticGroupPurchaseFulfillmentOrderDraftResponse> store;

    public MongoDomesticGroupPurchaseFulfillmentOrderDraftStore(IMongoClient mongoClient, IOptions<MongoDbOptions> options)
    {
        store = new(mongoClient, options, "orderer_group_purchase_fulfillment_order_drafts");
    }

    public Task<DomesticGroupPurchaseFulfillmentOrderDraftResponse> SaveAsync(
        DomesticGroupPurchaseFulfillmentOrderDraftResponse draft,
        CancellationToken cancellationToken = default)
    {
        draft.IsDurablyPersisted = true;
        return store.SaveAsync(
            draft.DraftId,
            draft.Plan.GroupPurchaseCampaignId,
            draft.CreatedByUserId,
            draft,
            cancellationToken);
    }

    public Task<DomesticGroupPurchaseFulfillmentOrderDraftResponse?> GetAsync(
        Guid draftId,
        CancellationToken cancellationToken = default)
        => store.GetAsync(draftId, cancellationToken);
}

internal sealed class MongoGroupPurchaseDraftStore<TDraft>
    where TDraft : class
{
    private readonly IMongoCollection<GroupPurchaseDraftDocument> collection;
    private readonly SemaphoreSlim indexLock = new(1, 1);
    private bool indexesReady;

    public MongoGroupPurchaseDraftStore(
        IMongoClient mongoClient,
        IOptions<MongoDbOptions> options,
        string collectionName)
    {
        var databaseName = options.Value.Database;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("MongoDb:Database configuration is required.");
        }

        collection = mongoClient
            .GetDatabase(databaseName.Trim())
            .GetCollection<GroupPurchaseDraftDocument>(collectionName);
    }

    public async Task<TDraft> SaveAsync(
        Guid draftId,
        Guid campaignId,
        string ownerUserId,
        TDraft draft,
        CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);
        var id = draftId.ToString("N");
        var existing = await collection
            .Find(x => x.DraftId == id)
            .FirstOrDefaultAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var document = new GroupPurchaseDraftDocument
        {
            Id = existing?.Id ?? ObjectId.GenerateNewId(),
            DraftId = id,
            GroupPurchaseCampaignId = campaignId.ToString("N"),
            OwnerUserId = ownerUserId,
            PayloadJson = JsonSerializer.Serialize(draft),
            CreatedAtUtc = existing?.CreatedAtUtc ?? now,
            UpdatedAtUtc = now
        };
        await collection.ReplaceOneAsync(
            x => x.DraftId == id,
            document,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
        return draft;
    }

    public async Task<TDraft?> GetAsync(Guid draftId, CancellationToken cancellationToken)
    {
        if (draftId == Guid.Empty)
        {
            return null;
        }

        await EnsureIndexesAsync(cancellationToken);
        var document = await collection
            .Find(x => x.DraftId == draftId.ToString("N"))
            .FirstOrDefaultAsync(cancellationToken);
        return document is null
            ? null
            : JsonSerializer.Deserialize<TDraft>(document.PayloadJson);
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

            await collection.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<GroupPurchaseDraftDocument>(
                    Builders<GroupPurchaseDraftDocument>.IndexKeys.Ascending(x => x.DraftId),
                    new CreateIndexOptions { Unique = true, Name = "ux_draft_id" }),
                new CreateIndexModel<GroupPurchaseDraftDocument>(
                    Builders<GroupPurchaseDraftDocument>.IndexKeys
                        .Ascending(x => x.GroupPurchaseCampaignId)
                        .Descending(x => x.UpdatedAtUtc),
                    new CreateIndexOptions { Name = "ix_campaign_updated" }),
                new CreateIndexModel<GroupPurchaseDraftDocument>(
                    Builders<GroupPurchaseDraftDocument>.IndexKeys
                        .Ascending(x => x.OwnerUserId)
                        .Descending(x => x.UpdatedAtUtc),
                    new CreateIndexOptions { Name = "ix_owner_updated" })
            ], cancellationToken);
            indexesReady = true;
        }
        finally
        {
            indexLock.Release();
        }
    }
}

internal sealed class GroupPurchaseDraftDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    public string DraftId { get; set; } = string.Empty;
    public string GroupPurchaseCampaignId { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
