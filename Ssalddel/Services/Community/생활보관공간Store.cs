using System.Text.Json;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;

namespace Ssalddel.Services.Community;

/// <summary>생활 보관 능력과 그 시간·수량 예약은 한 문서에서 원자적으로 변경합니다.</summary>
public interface I생활보관공간Store
{
    Task<생활보관공간문서?> 조회Async(string spaceId, CancellationToken cancellationToken = default);
    Task<bool> 저장Async(생활보관공간문서 document, long expectedRevision, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<생활보관공간문서> Items, int Total)> 목록Async(string? ownerId, string? regionKey,
        bool publishedOnly, DateTime nowUtc, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, int>> 공개동네집계Async(DateTime nowUtc, CancellationToken cancellationToken = default);
}

public sealed class Mongo생활보관공간Store : I생활보관공간Store
{
    public const string CollectionName = "neighborhood_storage_spaces";
    private readonly IMongoCollection<생활보관공간문서> collection;

    public Mongo생활보관공간Store(IMongoClient client, IOptions<MongoDbOptions> options)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Database))
            throw new InvalidOperationException("MongoDb:Database configuration is required.");
        collection = client.GetDatabase(options.Value.Database.Trim()).GetCollection<생활보관공간문서>(CollectionName);
    }

    public Task<생활보관공간문서?> 조회Async(string spaceId, CancellationToken cancellationToken = default)
        => collection.Find(document => document.SpaceId == spaceId).FirstOrDefaultAsync(cancellationToken)!;

    public async Task<bool> 저장Async(생활보관공간문서 document, long expectedRevision,
        CancellationToken cancellationToken = default)
    {
        if (document.Revision != expectedRevision + 1)
            throw new InvalidOperationException("보관 공간의 저장 revision이 올바르지 않습니다.");
        try
        {
            if (expectedRevision == 0)
            {
                // SpaceId가 Mongo _id이므로 여러 서버의 최초 등록도 같은 키에 한 번만 저장됩니다.
                await collection.InsertOneAsync(document, cancellationToken: cancellationToken);
                return true;
            }
            var result = await collection.ReplaceOneAsync(x => x.SpaceId == document.SpaceId && x.Revision == expectedRevision,
                document, new ReplaceOptions { IsUpsert = false }, cancellationToken);
            return result.MatchedCount == 1;
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public async Task<(IReadOnlyList<생활보관공간문서> Items, int Total)> 목록Async(string? ownerId,
        string? regionKey, bool publishedOnly, DateTime nowUtc, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var filter = Filter(ownerId, regionKey, publishedOnly, nowUtc);
        var count = await collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await collection.Find(filter).SortByDescending(x => x.UpdatedAtUtc)
            .Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(cancellationToken);
        return (items, (int)Math.Min(count, int.MaxValue));
    }

    public async Task<IReadOnlyDictionary<string, int>> 공개동네집계Async(DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var counts = await collection.Aggregate().Match(Filter(null, null, true, nowUtc))
            .Group(x => x.PublicNeighborhoodRegionKey, group => new 동네집계 { RegionKey = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        return counts.ToDictionary(x => x.RegionKey, x => x.Count, StringComparer.Ordinal);
    }

    private static FilterDefinition<생활보관공간문서> Filter(string? ownerId, string? regionKey,
        bool publishedOnly, DateTime nowUtc)
    {
        var builder = Builders<생활보관공간문서>.Filter;
        var filter = builder.Empty;
        if (ownerId is not null) filter &= builder.Eq(x => x.OwnerUserId, ownerId);
        if (regionKey is not null) filter &= builder.Eq(x => x.PublicNeighborhoodRegionKey, regionKey);
        if (publishedOnly) filter &= builder.Eq(x => x.Status, Ssalddel.Contracts.Common.Community.NeighborhoodStorageStatus.Published)
            & builder.Gt(x => x.AvailableUntilUtc, nowUtc);
        return filter;
    }

    private sealed class 동네집계
    {
        public string RegionKey { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}

[BsonIgnoreExtraElements]
public sealed class 생활보관공간문서
{
    [BsonId] public string SpaceId { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public long Revision { get; set; }
    public long OfferRevision { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PublicTitle { get; set; } = string.Empty;
    public string PublicDescription { get; set; } = string.Empty;
    public string PublicNeighborhoodRegionKey { get; set; } = string.Empty;
    public string GoodsKind { get; set; } = string.Empty;
    [BsonRepresentation(MongoDB.Bson.BsonType.Decimal128)] public decimal CapacityQuantity { get; set; }
    public string CapacityUnit { get; set; } = string.Empty;
    public DateTime AvailableFromUtc { get; set; }
    public DateTime AvailableUntilUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string ProtectedHandover { get; set; } = string.Empty;
    public List<생활보관요청증적> Operations { get; set; } = [];
    public List<생활보관예약문서> Reservations { get; set; } = [];
}

public sealed class 생활보관요청증적
{
    public string ActorId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public string? CollaborationId { get; set; }
    public long? TermsRevision { get; set; }
    public string Outcome { get; set; } = "committed";
    public string? FailureCode { get; set; }
}

public sealed class 생활보관예약문서
{
    public string CollaborationId { get; set; } = string.Empty;
    public long TermsRevision { get; set; }
    [BsonGuidRepresentation(MongoDB.Bson.GuidRepresentation.Standard)] public Guid IntentRequestId { get; set; }
    public string IntentActorId { get; set; } = string.Empty;
    public string RequesterUserId { get; set; } = string.Empty;
    [BsonRepresentation(MongoDB.Bson.BsonType.Decimal128)] public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime FromUtc { get; set; }
    public DateTime UntilUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool OwnerIntakeConfirmed { get; set; }
    public bool RequesterIntakeConfirmed { get; set; }
    public bool OwnerReturnConfirmed { get; set; }
    public bool RequesterReturnConfirmed { get; set; }
}

public sealed record 생활보관비공개인계정보(string Address, string Contact, string Instructions);

/// <summary>프로젝트의 기존 개인정보 암호화 경계를 Mongo 쓰기·읽기에 적용합니다.</summary>
public sealed class 생활보관인계정보Protection(IPersonalDataEncryptionService encryption)
{
    public string 보호(string address, string contact, string instructions)
        => encryption.Protect(JsonSerializer.Serialize(new 생활보관비공개인계정보(address, contact, instructions)))
            ?? throw new InvalidOperationException("보관 인계 정보 암호화에 실패했습니다.");

    public 생활보관비공개인계정보 복원(string value)
        => JsonSerializer.Deserialize<생활보관비공개인계정보>(encryption.Unprotect(value)
            ?? throw new InvalidOperationException("보관 인계 정보를 복원할 수 없습니다."))
            ?? throw new InvalidOperationException("보관 인계 정보가 올바르지 않습니다.");
}
