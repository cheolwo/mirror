using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Ssalddel.Contracts.Common.PrivacyRetention;
using 살뜰.Services.Options;
using 살뜰.Infrastructure.Security;

namespace Ssalddel.Services.PrivacyRetention;

public sealed class 개인정보파기Job
{
    [BsonId] public string Id { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;
    public string RecordId { get; set; } = string.Empty;
    public DateTime DueAtUtc { get; set; }
    public string StatusCode { get; set; } = 개인정보파기상태Codes.Pending;
    public long Revision { get; set; }
    public int Attempts { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public string? LeaseToken { get; set; }
    public DateTime? LeaseUntilUtc { get; set; }
    public string FailureCode { get; set; } = string.Empty;
    public List<개인정보보존정지> Holds { get; set; } = [];
    public List<개인정보보존적용Receipt> HoldReceipts { get; set; } = [];
    public string[] VerifiedStorageCodes { get; set; } = [];
    public DateTime? CompletedAtUtc { get; set; }
    public static string Key(string sourceCode, string recordId)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceCode + ":" + recordId))).ToLowerInvariant();
}

public sealed record 개인정보보존정지(string CaseId, string ReasonCode, DateTime ReviewAtUtc);
public sealed record 개인정보보존적용Receipt(string CaseId, long Sequence, bool Hold, string ReasonCode,
    DateTime? ReviewAtUtc, string Fingerprint);

public sealed class 개인정보파기Manifest
{
    [BsonId] public string Id { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;
    public string RecordId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
}

public sealed class 개인정보보존Evidence
{
    [BsonId] public string Id { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;
    public string RecordId { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
}

public sealed class 개인정보파기ScanCursor
{
    [BsonId] public string Id { get; set; } = string.Empty;
    public long AfterSequence { get; set; }
}

public interface I개인정보보존Store
{
    Task<개인정보파기Job?> GetAsync(string id, CancellationToken ct = default);
    Task CreateAsync(개인정보파기Job job, CancellationToken ct = default);
    Task<bool> ReplaceAsync(개인정보파기Job job, long expectedRevision, CancellationToken ct = default);
    Task<IReadOnlyList<개인정보파기Job>> DueAsync(DateTime now, int limit, CancellationToken ct = default, int maxAttempts = 8);
    Task SaveEvidenceAsync(string key, string sourceCode, string recordId, string minimizedJson, DateTime expiresAtUtc, CancellationToken ct = default);
    Task MarkDeletionAsync(개인정보파기Job job, DateTime now, CancellationToken ct = default);
    Task VerifyDeletionAsync(string key, DateTime now, CancellationToken ct = default);
    Task<bool> HasDeletionAsync(string sourceCode, string recordId, CancellationToken ct = default);
    Task<int> DeleteExpiredEvidenceAsync(DateTime now, CancellationToken ct = default);
    Task<long> ScanAfterAsync(string sourceCode, CancellationToken ct = default) => Task.FromResult(0L);
    Task AdvanceScanAsync(string sourceCode, long expectedAfter, long nextAfter, CancellationToken ct = default) => Task.CompletedTask;
}

public sealed class Mongo개인정보보존Store : I개인정보보존Store
{
    private readonly IMongoCollection<개인정보파기Job> _jobs;
    private readonly IMongoCollection<개인정보보존Evidence> _evidence;
    private readonly IMongoCollection<개인정보파기Manifest> _manifest;
    private readonly IPersonalDataEncryptionService _crypto;
    private readonly IMongoCollection<개인정보파기ScanCursor> _cursors;
    public Mongo개인정보보존Store(IMongoClient client, IOptions<MongoDbOptions> options, IPersonalDataEncryptionService crypto)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Database)) throw new InvalidOperationException("MongoDb:Database is required.");
        var db = client.GetDatabase(options.Value.Database.Trim());
        _jobs = db.GetCollection<개인정보파기Job>("privacy_retention_jobs");
        _evidence = db.GetCollection<개인정보보존Evidence>("privacy_retained_evidence");
        _manifest = db.GetCollection<개인정보파기Manifest>("privacy_deletion_manifest");
        _cursors = db.GetCollection<개인정보파기ScanCursor>("privacy_retention_scan_cursors");
        _crypto = crypto;
    }
    public Task<개인정보파기Job?> GetAsync(string id, CancellationToken ct = default)
        => _jobs.Find(x => x.Id == id).FirstOrDefaultAsync(ct)!;
    public async Task CreateAsync(개인정보파기Job job, CancellationToken ct = default)
    {
        try { await _jobs.InsertOneAsync(job, cancellationToken: ct); }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey) { }
    }
    public async Task<bool> ReplaceAsync(개인정보파기Job job, long expectedRevision, CancellationToken ct = default)
    {
        job.Revision = expectedRevision + 1;
        return (await _jobs.ReplaceOneAsync(x => x.Id == job.Id && x.Revision == expectedRevision, job, cancellationToken: ct)).MatchedCount == 1;
    }
    public async Task<IReadOnlyList<개인정보파기Job>> DueAsync(DateTime now, int limit, CancellationToken ct = default, int maxAttempts = 8)
        => await _jobs.Find(x => x.CompletedAtUtc == null && x.DueAtUtc <= now && x.NextAttemptAtUtc <= now
            && (x.Attempts < maxAttempts || x.Holds.Count > 0)
            && (x.LeaseUntilUtc == null || x.LeaseUntilUtc <= now)).SortBy(x => x.DueAtUtc).Limit(Math.Clamp(limit, 1, 200)).ToListAsync(ct);
    public async Task SaveEvidenceAsync(string key, string sourceCode, string recordId, string minimizedJson, DateTime expiresAtUtc, CancellationToken ct = default)
    {
        var cipher = _crypto.Protect(minimizedJson);
        if (string.IsNullOrWhiteSpace(cipher) || cipher == minimizedJson) throw new InvalidOperationException("RetentionEvidenceEncryptionUnavailable");
        await _evidence.UpdateOneAsync(x => x.Id == key, Builders<개인정보보존Evidence>.Update
            .SetOnInsert(x => x.Id, key).SetOnInsert(x => x.SourceCode, sourceCode).SetOnInsert(x => x.RecordId, recordId)
            .SetOnInsert(x => x.Ciphertext, cipher).SetOnInsert(x => x.ExpiresAtUtc, expiresAtUtc), new UpdateOptions { IsUpsert = true }, ct);
    }
    public Task MarkDeletionAsync(개인정보파기Job job, DateTime now, CancellationToken ct = default)
        => _manifest.UpdateOneAsync(x => x.Id == job.Id, Builders<개인정보파기Manifest>.Update
            .SetOnInsert(x => x.Id, job.Id).SetOnInsert(x => x.SourceCode, job.SourceCode)
            .SetOnInsert(x => x.RecordId, job.RecordId).SetOnInsert(x => x.CreatedAtUtc, now), new UpdateOptions { IsUpsert = true }, ct);
    public Task VerifyDeletionAsync(string key, DateTime now, CancellationToken ct = default)
        => _manifest.UpdateOneAsync(x => x.Id == key, Builders<개인정보파기Manifest>.Update.Set(x => x.VerifiedAtUtc, now), cancellationToken: ct);
    public Task<bool> HasDeletionAsync(string sourceCode, string recordId, CancellationToken ct = default)
        => _manifest.Find(x => x.Id == 개인정보파기Job.Key(sourceCode, recordId)).AnyAsync(ct);
    public async Task<int> DeleteExpiredEvidenceAsync(DateTime now, CancellationToken ct = default)
    {
        var held = await _jobs.Find(x => x.Holds.Count > 0).Project(x => x.Id).ToListAsync(ct);
        return checked((int)(await _evidence.DeleteManyAsync(x => x.ExpiresAtUtc <= now && !held.Contains(x.Id), ct)).DeletedCount);
    }
    public async Task<long> ScanAfterAsync(string sourceCode, CancellationToken ct = default)
        => (await _cursors.Find(x => x.Id == sourceCode).FirstOrDefaultAsync(ct))?.AfterSequence ?? 0;
    public async Task AdvanceScanAsync(string sourceCode, long expectedAfter, long nextAfter, CancellationToken ct = default)
    {
        try
        {
            await _cursors.UpdateOneAsync(x => x.Id == sourceCode && x.AfterSequence == expectedAfter,
                Builders<개인정보파기ScanCursor>.Update.SetOnInsert(x => x.Id, sourceCode).Set(x => x.AfterSequence, nextAfter),
                new UpdateOptions { IsUpsert = expectedAfter == 0 }, ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey) { }
    }
}
