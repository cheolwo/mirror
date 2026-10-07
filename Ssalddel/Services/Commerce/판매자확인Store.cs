using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;
using System.Text.Json;

namespace Ssalddel.Services.Commerce;

public sealed class 판매자확인Record
{
    [BsonId] public string UserId { get; set; } = string.Empty;
    public long Revision { get; set; }
    public Guid LastRequestId { get; set; }
    public string LastRequestHash { get; set; } = string.Empty;
    public string SellerKind { get; set; } = "Unknown";
    public string DisplayName { get; set; } = string.Empty;
    public string ProfileJson { get; set; } = "{}";
    public string NoticeVersion { get; set; } = string.Empty;
    public DateTime ConsentedAtUtc { get; set; }
    public 거래신원확인결과? Verification { get; set; }
}

public interface I판매자확인Store
{
    Task<판매자확인Record?> 조회Async(string userId, CancellationToken cancellationToken);
    Task<bool> 저장Async(판매자확인Record record, long expectedRevision, CancellationToken cancellationToken);
}

public sealed class Mongo판매자확인Store : I판매자확인Store
{
    private readonly IMongoCollection<저장Record> _collection;
    private readonly IIsmsPProtectedDataCryptoService _crypto;
    public Mongo판매자확인Store(IMongoClient client, IOptions<MongoDbOptions> options, IIsmsPProtectedDataCryptoService crypto)
    {
        _collection = client.GetDatabase(options.Value.Database).GetCollection<저장Record>("commerce_seller_verifications");
        _crypto = crypto;
    }
    public async Task<판매자확인Record?> 조회Async(string userId, CancellationToken cancellationToken)
    {
        var saved = await _collection.Find(x => x.Id == userId).FirstOrDefaultAsync(cancellationToken);
        if (saved is null) return null;
        var record = JsonSerializer.Deserialize<판매자확인Record>(_crypto.DecryptAtRest("commerce.seller.identity", saved.Payload));
        if (record is null || record.UserId != userId || saved.Id != userId || record.Revision != saved.Revision)
            throw new InvalidDataException("판매자 확인 원장의 계정·판본 결속이 일치하지 않습니다.");
        return record;
    }
    public async Task<bool> 저장Async(판매자확인Record record, long expectedRevision, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(record.UserId) || expectedRevision < 0 || record.Revision != expectedRevision + 1)
            throw new InvalidDataException("판매자 확인 원장의 판본이 유효하지 않습니다.");
        var saved = new 저장Record { Id = record.UserId, Revision = record.Revision,
            Payload = _crypto.EncryptAtRest("commerce.seller.identity", JsonSerializer.Serialize(record)).StoredValue };
        if (expectedRevision == 0)
        {
            try { await _collection.InsertOneAsync(saved, cancellationToken: cancellationToken); return true; }
            catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey) { return false; }
        }
        return (await _collection.ReplaceOneAsync(x => x.Id == record.UserId && x.Revision == expectedRevision, saved,
            cancellationToken: cancellationToken)).ModifiedCount == 1;
    }
    public sealed class 저장Record
    {
        [BsonId] public string Id { get; set; } = string.Empty;
        public long Revision { get; set; }
        public string Payload { get; set; } = string.Empty;
    }
}
