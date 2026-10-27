using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Ssalddel.Contracts.Common.Community;
using 살뜰.Services.Options;

namespace Ssalddel.Services.Community;

public interface I화물정보공고Store
{
    Task<화물정보공고문서?> 조회Async(string noticeId, CancellationToken ct = default);
    Task<화물정보공고문서?> 요청조회Async(string requestKey, CancellationToken ct = default);
    Task<bool> 저장Async(화물정보공고문서 document, long expectedRevision, CancellationToken ct = default);
    Task<(IReadOnlyList<화물정보공고문서> Items, int Total)> 목록Async(string? ownerId, bool publicOnly,
        IReadOnlyList<string> allowedRegionKeys, DateTime nowUtc, int page, int pageSize, CancellationToken ct = default);
}

/// <summary>한 공고의 상태·동의·명령 결과를 한 문서의 CAS로 저장합니다. 운송 원장을 참조하지 않습니다.</summary>
public sealed class Mongo화물정보공고Store : I화물정보공고Store
{
    public const string CollectionName = "community_cargo_information_notices";
    private readonly IMongoCollection<화물정보공고문서> collection;
    private readonly SemaphoreSlim indexLock = new(1, 1);
    private bool indexesReady;

    public Mongo화물정보공고Store(IMongoClient client, IOptions<MongoDbOptions> options)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Database))
            throw new InvalidOperationException("MongoDb:Database configuration is required.");
        collection = client.GetDatabase(options.Value.Database.Trim()).GetCollection<화물정보공고문서>(CollectionName);
    }

    public async Task<화물정보공고문서?> 조회Async(string noticeId, CancellationToken ct = default)
    {
        await EnsureIndexesAsync(ct);
        return await collection.Find(x => x.NoticeId == noticeId).FirstOrDefaultAsync(ct);
    }

    public async Task<화물정보공고문서?> 요청조회Async(string requestKey, CancellationToken ct = default)
    {
        await EnsureIndexesAsync(ct);
        return await collection.Find(Builders<화물정보공고문서>.Filter.ElemMatch(x => x.Operations,
            operation => operation.RequestKey == requestKey)).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> 저장Async(화물정보공고문서 document, long expectedRevision, CancellationToken ct = default)
    {
        if (document.Revision != expectedRevision + 1 || document.Operations.Count == 0)
            throw new InvalidOperationException("Cargo notice revision and command evidence are required.");
        await EnsureIndexesAsync(ct);
        try
        {
            if (expectedRevision == 0)
            {
                await collection.InsertOneAsync(document, cancellationToken: ct);
                return true;
            }
            var changed = await collection.ReplaceOneAsync(x => x.NoticeId == document.NoticeId
                && x.OwnerUserId == document.OwnerUserId && x.Revision == expectedRevision,
                document, new ReplaceOptions { IsUpsert = false }, ct);
            return changed.MatchedCount == 1;
        }
        catch (MongoWriteException error) when (error.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // 동일 요청의 동시 실행인지 다른 입력의 충돌인지 UseCase가 정본을 다시 읽어 구분합니다.
            return false;
        }
    }

    public async Task<(IReadOnlyList<화물정보공고문서> Items, int Total)> 목록Async(string? ownerId,
        bool publicOnly, IReadOnlyList<string> allowedRegionKeys, DateTime nowUtc, int page, int pageSize,
        CancellationToken ct = default)
    {
        await EnsureIndexesAsync(ct);
        var filters = Builders<화물정보공고문서>.Filter;
        var filter = filters.Empty;
        if (ownerId is not null) filter &= filters.Eq(x => x.OwnerUserId, ownerId);
        if (publicOnly)
        {
            filter &= filters.Eq(x => x.Status, CargoNoticeStatus.Published)
                & filters.Lte(x => x.PublishedAtUtc, nowUtc)
                & filters.Gt(x => x.ExpiresAtUtc, nowUtc)
                & filters.In(x => x.Content.PickupRegionKey, allowedRegionKeys)
                & filters.In(x => x.Content.DropoffRegionKey, allowedRegionKeys)
                & new BsonDocumentFilterDefinition<화물정보공고문서>(new BsonDocument("$expr",
                    new BsonDocument("$eq", new BsonArray { "$ConsentedContentRevision", "$ContentRevision" })));
        }
        var count = await collection.CountDocumentsAsync(filter, cancellationToken: ct);
        var items = await collection.Find(filter).SortByDescending(x => x.UpdatedAtUtc).ThenBy(x => x.NoticeId)
            .Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(ct);
        return (items, (int)Math.Min(count, int.MaxValue));
    }

    private async Task EnsureIndexesAsync(CancellationToken ct)
    {
        if (indexesReady) return;
        await indexLock.WaitAsync(ct);
        try
        {
            if (indexesReady) return;
            var keys = Builders<화물정보공고문서>.IndexKeys;
            await collection.Indexes.CreateManyAsync(new[]
            {
                // 계정 범위의 같은 요청 ID를 다른 공고에 재사용해도 중복 확정할 수 없습니다.
                new CreateIndexModel<화물정보공고문서>(keys.Ascending("Operations.RequestKey"),
                    new CreateIndexOptions { Unique = true, Name = "cargo_notice_unique_actor_request" }),
                new CreateIndexModel<화물정보공고문서>(keys.Ascending(x => x.OwnerUserId).Descending(x => x.UpdatedAtUtc)),
                new CreateIndexModel<화물정보공고문서>(keys.Ascending(x => x.Status).Ascending(x => x.ExpiresAtUtc)
                    .Descending(x => x.UpdatedAtUtc))
            }, cancellationToken: ct);
            indexesReady = true;
        }
        finally { indexLock.Release(); }
    }
}

[BsonIgnoreExtraElements]
public sealed class 화물정보공고문서
{
    [BsonId] public string NoticeId { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public long Revision { get; set; }
    [BsonElement("ContentRevision")] public long ContentRevision { get; set; }
    public string Status { get; set; } = CargoNoticeStatus.Draft;
    public CargoNoticeContent Content { get; set; } = new();
    [BsonElement("ConsentedContentRevision")] public long? ConsentedContentRevision { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    [BsonElement("Operations")] public List<화물정보공고요청증적> Operations { get; set; } = [];
}

public sealed class 화물정보공고요청증적
{
    [BsonElement("RequestKey")] public string RequestKey { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public string ResultJson { get; set; } = string.Empty;
}
