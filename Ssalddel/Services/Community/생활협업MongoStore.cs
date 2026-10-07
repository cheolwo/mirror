using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using 살뜰.Services.Options;

namespace Ssalddel.Services.Community;

/// <summary>
/// 주소를 포함할 수 있는 합의 조건·사용자 식별자는 Data Protection으로 영속 경계에서 보호합니다.
/// community_ledgers 및 공개 완료글 투영 경로와 독립된 컬렉션이므로 generic 원장 reader는 접근하지 않습니다.
/// 키 ring 보존은 기존 서버 Data Protection 운영 설정의 책임이며 복호화/저장 실패를 샘플로 대체하지 않습니다.
/// </summary>
public sealed class Mongo생활협업Store : I생활협업Store
{
    public const string CollectionName = "neighborhood_collaborations_private";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly IMongoCollection<생활협업보호Document> collection;
    private readonly IDataProtector protector;
    private readonly SemaphoreSlim indexGate = new(1, 1);
    private bool indexesReady;

    public Mongo생활협업Store(IMongoClient client, IOptions<MongoDbOptions> options, IDataProtectionProvider protection)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Database)) throw new InvalidOperationException("MongoDb:Database configuration is required.");
        collection = client.GetDatabase(options.Value.Database.Trim()).GetCollection<생활협업보호Document>(CollectionName);
        protector = protection.CreateProtector("Ssalddel.NeighborhoodCollaboration.Private.v1");
    }

    public async Task<생활협업Record?> 조회Async(string stableId, CancellationToken cancellationToken = default)
    {
        await EnsureIndexesAsync(cancellationToken);
        var document = await collection.Find(x => x.StableId == stableId).FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : Read(document);
    }

    public async Task<bool> 생성Async(생활협업Record record, CancellationToken cancellationToken = default)
    {
        await EnsureIndexesAsync(cancellationToken);
        try { await collection.InsertOneAsync(Write(record), cancellationToken: cancellationToken); return true; }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey) { return false; }
    }

    public async Task<bool> 교체Async(생활협업Record record, long expectedRevision, CancellationToken cancellationToken = default)
    {
        await EnsureIndexesAsync(cancellationToken);
        if (record.Revision != expectedRevision + 1) throw new InvalidOperationException("Collaboration revision must advance once.");
        var result = await collection.ReplaceOneAsync(x => x.StableId == record.StableId && x.Revision == expectedRevision,
            Write(record), cancellationToken: cancellationToken);
        return result.MatchedCount == 1;
    }

    public async Task<IReadOnlyList<생활협업Record>> 목록Async(생활협업StoreQuery query, CancellationToken cancellationToken = default)
    {
        await EnsureIndexesAsync(cancellationToken);
        var filters = Builders<생활협업보호Document>.Filter;
        FilterDefinition<생활협업보호Document> filter;
        if (!string.IsNullOrWhiteSpace(query.UserId))
        {
            var actor = Hash(query.UserId);
            filter = query.Scope switch
            {
                "requested" => filters.Eq(x => x.RequesterHash, actor),
                "undertaken" => filters.Or(filters.Eq(x => x.OwnerHash, actor), filters.AnyEq(x => x.AcceptedParticipantHashes, actor)),
                _ => filters.Or(filters.Eq(x => x.OwnerHash, actor), filters.Eq(x => x.RequesterHash, actor), filters.AnyEq(x => x.ParticipantHashes, actor))
            };
        }
        else if (query.SourcePostId is > 0) filter = filters.Eq(x => x.SourcePostId, query.SourcePostId);
        else return [];
        if (query.SourcePostId is > 0) filter &= filters.Eq(x => x.SourcePostId, query.SourcePostId);
        var rows = await collection.Find(filter).SortByDescending(x => x.UpdatedAtUtc).ThenBy(x => x.StableId)
            .Skip(Math.Max(0, query.Skip)).Limit(Math.Clamp(query.Take, 1, 101)).ToListAsync(cancellationToken);
        return rows.Select(Read).ToArray();
    }

    private 생활협업보호Document Write(생활협업Record record) => new()
    {
        StableId = record.StableId, Revision = record.Revision, OwnerHash = Hash(record.OwnerUserId), RequesterHash = Hash(record.RequesterUserId),
        ParticipantHashes = record.Participants.Select(x => Hash(x.UserId)).ToArray(),
        AcceptedParticipantHashes = record.Participants.Where(x => x.StatusCode == "accepted").Select(x => Hash(x.UserId)).ToArray(),
        SourcePostId = record.SourcePostId, UpdatedAtUtc = record.UpdatedAtUtc,
        ProtectedPayload = protector.Protect(JsonSerializer.Serialize(record, Json))
    };

    private 생활협업Record Read(생활협업보호Document document)
    {
        var record = JsonSerializer.Deserialize<생활협업Record>(protector.Unprotect(document.ProtectedPayload), Json)
            ?? throw new InvalidDataException("Collaboration payload is missing.");
        if (record.StableId != document.StableId || record.Revision != document.Revision)
            throw new InvalidDataException("Collaboration protected payload revision does not match.");
        return record;
    }

    private async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        if (indexesReady) return;
        await indexGate.WaitAsync(cancellationToken);
        try
        {
            if (indexesReady) return;
            var keys = Builders<생활협업보호Document>.IndexKeys;
            await collection.Indexes.CreateManyAsync([
                new(keys.Ascending(x => x.RequesterHash).Descending(x => x.UpdatedAtUtc)),
                new(keys.Ascending(x => x.OwnerHash).Descending(x => x.UpdatedAtUtc)),
                new(keys.Ascending(x => x.ParticipantHashes).Descending(x => x.UpdatedAtUtc)),
                new(keys.Ascending(x => x.AcceptedParticipantHashes).Descending(x => x.UpdatedAtUtc)),
                new(keys.Ascending(x => x.SourcePostId).Descending(x => x.UpdatedAtUtc))
            ], cancellationToken);
            indexesReady = true;
        }
        finally { indexGate.Release(); }
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    public sealed class 생활협업보호Document
    {
        [BsonId] public string StableId { get; set; } = string.Empty;
        public long Revision { get; set; }
        public string OwnerHash { get; set; } = string.Empty;
        public string RequesterHash { get; set; } = string.Empty;
        public string[] ParticipantHashes { get; set; } = [];
        public string[] AcceptedParticipantHashes { get; set; } = [];
        public long? SourcePostId { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public string ProtectedPayload { get; set; } = string.Empty;
    }
}
