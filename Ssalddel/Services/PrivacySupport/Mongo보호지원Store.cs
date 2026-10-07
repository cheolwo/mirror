using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using 살뜰.Services.Options;

namespace Ssalddel.Services.PrivacySupport;

public sealed class Mongo보호지원Store : I보호지원Store
{
    public const string CollectionName = "commerce_privacy_support_private";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly IMongoCollection<보호지원Document> collection;
    private readonly IMongoCollection<보호지원파기Document> deletionManifest;
    private readonly IDataProtector protector;
    private readonly SemaphoreSlim indexGate = new(1, 1);
    private bool indexesReady;

    public Mongo보호지원Store(IMongoClient mongo, IOptions<MongoDbOptions> options, IDataProtectionProvider protection)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Database)) throw new InvalidOperationException("MongoDb:Database configuration is required.");
        collection = mongo.GetDatabase(options.Value.Database.Trim()).GetCollection<보호지원Document>(CollectionName);
        deletionManifest = mongo.GetDatabase(options.Value.Database.Trim()).GetCollection<보호지원파기Document>("commerce_privacy_support_deletion_manifest");
        protector = protection.CreateProtector("Ssalddel.CommercePrivacySupport.Private.v1");
    }

    public async Task<IReadOnlyList<보호지원Record>> 파기후보Async(DateTime nowUtc, int take, CancellationToken cancellationToken = default)
    {
        await IndexesAsync(cancellationToken);
        // 삭제 뒤 완료 기록 직전 연결이 끊긴 경우 실제 원본 부재를 다시 확인합니다. 삭제 시각을 지어내지 않습니다.
        var intents = await deletionManifest.Find(x => x.DeletedAtUtc == null && x.VerifiedAbsentAtUtc == null)
            .Limit(100).ToListAsync(cancellationToken);
        foreach (var intent in intents)
            if (!await collection.Find(x => x.CaseId == intent.CaseId).AnyAsync(cancellationToken))
                await deletionManifest.UpdateOneAsync(x => x.CaseId == intent.CaseId,
                    Builders<보호지원파기Document>.Update.Set(x => x.VerifiedAbsentAtUtc, nowUtc), cancellationToken: cancellationToken);
        var rows = await collection.Find(x => x.Kind == "transaction-dispute" && x.StatusCode == "closed"
                && !x.SourceHoldActive && !x.RetentionPending && x.PurgeAfterAtUtc <= nowUtc)
            .SortBy(x => x.PurgeAfterAtUtc).Limit(Math.Clamp(take, 1, 100)).ToListAsync(cancellationToken);
        return rows.Select(Read).ToArray();
    }

    public async Task<bool> 파기Async(보호지원Record record, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        if (record.PurgeStateCode != "claimed" || record.SourceHoldActive || record.RetentionIntent is not null || record.Kind != "transaction-dispute"
            || record.StatusCode != "closed" || record.PurgeAfterAtUtc is null || record.PurgeAfterAtUtc > nowUtc)
            throw new InvalidOperationException("Case must be claimed and past its confirmed retention period.");
        // 삭제 의도를 먼저 기록합니다. 중간 실패 시 claimed 상태의 원본과 intent로 재처리합니다.
        var intent = new 보호지원파기Document { CaseId = record.CaseId, PolicyVersion = record.RetentionPolicyVersion,
            ClaimedRevision = record.Revision, IntendedAtUtc = nowUtc };
        await deletionManifest.UpdateOneAsync(x => x.CaseId == record.CaseId,
            Builders<보호지원파기Document>.Update.SetOnInsert(x => x.PolicyVersion, intent.PolicyVersion)
                .SetOnInsert(x => x.ClaimedRevision, intent.ClaimedRevision).SetOnInsert(x => x.IntendedAtUtc, nowUtc),
            new UpdateOptions { IsUpsert = true }, cancellationToken);
        var deleted = await collection.DeleteOneAsync(x => x.CaseId == record.CaseId && x.Revision == record.Revision
                && x.PurgeStateCode == "claimed" && !x.SourceHoldActive && !x.RetentionPending && x.StatusCode == "closed" && x.PurgeAfterAtUtc <= nowUtc, cancellationToken);
        if (deleted.DeletedCount != 1) return false;
        await deletionManifest.UpdateOneAsync(x => x.CaseId == record.CaseId,
            Builders<보호지원파기Document>.Update.Set(x => x.DeletedAtUtc, nowUtc), cancellationToken: cancellationToken);
        return true;
    }

    public async Task<보호지원Record?> 조회Async(string caseId, CancellationToken cancellationToken = default)
    {
        await IndexesAsync(cancellationToken);
        var row = await collection.Find(x => x.CaseId == caseId).FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : Read(row);
    }

    public async Task<IReadOnlyList<보호지원Record>> 보존조정후보Async(int take, CancellationToken cancellationToken = default)
    {
        await IndexesAsync(cancellationToken);
        return (await collection.Find(x => x.RetentionPending).SortBy(x => x.UpdatedAtUtc)
            .Limit(Math.Clamp(take, 1, 100)).ToListAsync(cancellationToken)).Select(Read).ToArray();
    }

    public async Task<bool> 생성Async(보호지원Record record, CancellationToken cancellationToken = default)
    {
        await IndexesAsync(cancellationToken);
        try { await collection.InsertOneAsync(Write(record), cancellationToken: cancellationToken); return true; }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey) { return false; }
    }

    public async Task<bool> 교체Async(보호지원Record record, long expectedRevision, CancellationToken cancellationToken = default)
    {
        await IndexesAsync(cancellationToken);
        if (record.Revision != expectedRevision + 1) throw new InvalidOperationException("Support case revision must advance once.");
        var result = await collection.ReplaceOneAsync(x => x.CaseId == record.CaseId && x.Revision == expectedRevision,
            Write(record), cancellationToken: cancellationToken);
        return result.MatchedCount == 1;
    }

    public async Task<IReadOnlyList<보호지원Record>> 목록Async(string? userId, string? kind, int skip, int take, CancellationToken cancellationToken = default)
    {
        await IndexesAsync(cancellationToken);
        var filters = Builders<보호지원Document>.Filter;
        var filter = userId is null ? filters.Empty : filters.AnyEq(x => x.PartyHashes, Hash(userId));
        if (!string.IsNullOrWhiteSpace(kind)) filter &= filters.Eq(x => x.Kind, kind);
        var rows = await collection.Find(filter).SortByDescending(x => x.UpdatedAtUtc).ThenBy(x => x.CaseId)
            .Skip(Math.Max(0, skip)).Limit(Math.Clamp(take, 1, 101)).ToListAsync(cancellationToken);
        return rows.Select(Read).ToArray();
    }

    private 보호지원Document Write(보호지원Record row) => new()
    {
        CaseId = row.CaseId, Revision = row.Revision, Kind = row.Kind, StatusCode = row.StatusCode,
        PartyHashes = row.PartyUserIds.Select(Hash).ToArray(), UpdatedAtUtc = row.UpdatedAtUtc,
        SourceHoldActive = row.SourceHoldActive, RetentionPending = row.RetentionIntent is not null,
        PurgeAfterAtUtc = row.PurgeAfterAtUtc, PurgeStateCode = row.PurgeStateCode,
        ProtectedPayload = protector.Protect(JsonSerializer.Serialize(row, Json))
    };

    private 보호지원Record Read(보호지원Document row)
    {
        var record = JsonSerializer.Deserialize<보호지원Record>(protector.Unprotect(row.ProtectedPayload), Json)
                     ?? throw new InvalidDataException("Support case payload is missing.");
        if (record.CaseId != row.CaseId || record.Revision != row.Revision || record.Kind != row.Kind || record.StatusCode != row.StatusCode
            || MongoTime(record.PurgeAfterAtUtc) != MongoTime(row.PurgeAfterAtUtc) || record.SourceHoldActive != row.SourceHoldActive
            || (record.RetentionIntent is not null) != row.RetentionPending || record.PurgeStateCode != row.PurgeStateCode
            || !record.PartyUserIds.Select(Hash).Order(StringComparer.Ordinal).SequenceEqual(row.PartyHashes.Order(StringComparer.Ordinal)))
            throw new InvalidDataException("Support case protected payload binding does not match.");
        return record;
    }

    private async Task IndexesAsync(CancellationToken cancellationToken)
    {
        if (indexesReady) return;
        await indexGate.WaitAsync(cancellationToken);
        try
        {
            if (indexesReady) return;
            var keys = Builders<보호지원Document>.IndexKeys;
            await collection.Indexes.CreateManyAsync([
                new(keys.Ascending(x => x.PartyHashes).Descending(x => x.UpdatedAtUtc)),
                new(keys.Ascending(x => x.Kind).Ascending(x => x.StatusCode).Descending(x => x.UpdatedAtUtc)),
                new(keys.Ascending(x => x.PurgeAfterAtUtc).Ascending(x => x.SourceHoldActive).Ascending(x => x.RetentionPending)),
                new(keys.Ascending(x => x.RetentionPending).Ascending(x => x.UpdatedAtUtc))
            ], cancellationToken);
            indexesReady = true;
        }
        finally { indexGate.Release(); }
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static long? MongoTime(DateTime? value) => value.HasValue ? new DateTimeOffset(value.Value).ToUnixTimeMilliseconds() : null;
    public sealed class 보호지원Document
    {
        [BsonId] public string CaseId { get; set; } = string.Empty;
        public long Revision { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string StatusCode { get; set; } = string.Empty;
        public string[] PartyHashes { get; set; } = [];
        public DateTime UpdatedAtUtc { get; set; }
        public string ProtectedPayload { get; set; } = string.Empty;
        public bool SourceHoldActive { get; set; }
        public bool RetentionPending { get; set; }
        public DateTime? PurgeAfterAtUtc { get; set; }
        public string PurgeStateCode { get; set; } = string.Empty;
    }

    public sealed class 보호지원파기Document
    {
        [BsonId] public string CaseId { get; set; } = string.Empty;
        public string PolicyVersion { get; set; } = string.Empty;
        public long ClaimedRevision { get; set; }
        public DateTime IntendedAtUtc { get; set; }
        public DateTime? DeletedAtUtc { get; set; }
        public DateTime? VerifiedAbsentAtUtc { get; set; }
    }
}
