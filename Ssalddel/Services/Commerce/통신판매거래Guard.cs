using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Ssalddel.Contracts.Common.Commerce;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;

namespace Ssalddel.Services.Commerce;

public interface I통신판매거래Guard
{
    Task 요구Async(string actorId, string? sellerId, 거래보호확인Request? notice, string sourceCode,
        string requestId, CancellationToken cancellationToken);
    Task 음식주문요구Async(string actorId, long restaurantId, 거래보호확인Request? notice, string requestId, CancellationToken cancellationToken);
}

public sealed class 통신판매거래Guard(판매자확인Service sellers, 통신판매안내Service notices,
    I거래신원확인Gateway gateway, IOptions<통신판매운영Options> options, ISsalddelExecutionModePolicy execution,
    I거래고지증적Store evidence, TimeProvider clock) : I통신판매거래Guard
{
    public async Task 요구Async(string actorId, string? sellerId, 거래보호확인Request? notice, string sourceCode, string requestId, CancellationToken cancellationToken)
    {
        // Simulation은 운영 자격 확인 결과나 실제 거래로 승격하지 않습니다.
        if (execution.IsSimulation) return;
        if (notices.운영부족().Count != 0 || !gateway.IsConfigured)
            throw new 거래보호Exception("CommerceOperationsNotReady", "운영 준비가 완료되지 않아 새 거래·배송 접수를 할 수 없습니다.");
        if (notice is null || !notice.NoticeAccepted || notice.NoticeVersion != 통신판매안내Service.Version)
            throw new 거래보호Exception("CommerceNoticeRequired", "현재 판매자·거래 조건과 중개 안내를 확인해 주세요.");
        // 과거 익명 글의 비밀번호 수정 권한이 판매자 확인을 대신하지 않습니다.
        if ((sourceCode is "sales-offer-publication" or "sales-offer-update") && string.IsNullOrWhiteSpace(sellerId))
            throw new 거래보호Exception("SellerVerificationRequired", "판매 글에는 현재 확인된 계약 판매자가 필요합니다.");
        var actor = await sellers.원본Async(actorId, cancellationToken);
        if (actor is null || !sellers.응답(actor).IsAdult) throw new 거래보호Exception("AdultVerificationRequired", "서버에서 성년 여부를 먼저 확인해 주세요.");
        판매자확인Record? seller = null;
        if (sellerId is not null)
        {
            seller = await sellers.원본Async(sellerId, cancellationToken);
            if (seller is null || sellers.응답(seller).StatusCode != "Verified")
                throw new 거래보호Exception("SellerVerificationRequired", "판매자의 현재 신원·유형 확인이 필요합니다.");
            if (notice.SellerRevision != seller.Revision)
                throw new 거래보호Exception("SellerDisclosureChanged", "판매자 정보가 바뀌었거나 확인되지 않았습니다. 현재 정보를 확인한 뒤 다시 신청해 주세요.");
        }
        await evidence.기록Async(new 거래고지증적
        {
            Id = 판매자확인Service.Hash(sourceCode + ":" + actorId + ":" + requestId), ActorUserId = actorId,
            SellerUserId = sellerId, SourceCode = sourceCode, ClientRequestId = requestId,
            NoticeVersion = notice.NoticeVersion, NoticeHash = 판매자확인Service.Hash(System.Text.Json.JsonSerializer.Serialize(notices.조회().Documents)),
            SellerRevision = seller?.Revision, SellerKind = seller?.SellerKind, AcceptedAtUtc = clock.GetUtcNow().UtcDateTime,
            StateCode = "PreflightVerified"
        }, cancellationToken);
    }
    public Task 음식주문요구Async(string actorId, long restaurantId, 거래보호확인Request? notice, string requestId, CancellationToken cancellationToken)
    {
        if (execution.IsSimulation) return Task.CompletedTask;
        if (!options.Value.RestaurantSellerUserIds.TryGetValue(restaurantId.ToString(System.Globalization.CultureInfo.InvariantCulture), out var sellerId)
            || string.IsNullOrWhiteSpace(sellerId))
            throw new 거래보호Exception("RestaurantSellerNotVerified", "음식점의 계약 판매자 확인이 필요합니다.");
        return 요구Async(actorId, sellerId, notice, "food-order", requestId, cancellationToken);
    }
}

public sealed class 거래고지증적
{
    public string Id { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
    public string? SellerUserId { get; set; }
    public string SourceCode { get; set; } = string.Empty;
    public string ClientRequestId { get; set; } = string.Empty;
    public string NoticeVersion { get; set; } = string.Empty;
    public string NoticeHash { get; set; } = string.Empty;
    public long? SellerRevision { get; set; }
    public string? SellerKind { get; set; }
    public string StateCode { get; set; } = string.Empty;
    public DateTime AcceptedAtUtc { get; set; }
}
public interface I거래고지증적Store { Task 기록Async(거래고지증적 record, CancellationToken cancellationToken); }
public sealed class Mongo거래고지증적Store(IMongoClient client, IOptions<MongoDbOptions> options, IIsmsPProtectedDataCryptoService crypto) : I거래고지증적Store
{
    private readonly IMongoCollection<저장Record> _collection = client.GetDatabase(options.Value.Database).GetCollection<저장Record>("commerce_transaction_disclosures");
    public async Task 기록Async(거래고지증적 record, CancellationToken cancellationToken)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(record);
        var saved = new 저장Record { Id = record.Id, Payload = crypto.EncryptAtRest("commerce.transaction.disclosure", json).StoredValue };
        // 재시도는 최초 확인 기록을 덮어쓰지 않습니다. 이는 주문 성립/공급/결제 확인 기록이 아닙니다.
        await _collection.UpdateOneAsync(x => x.Id == saved.Id,
            Builders<저장Record>.Update.SetOnInsert(x => x.Payload, saved.Payload), new UpdateOptions { IsUpsert = true }, cancellationToken);
        var stored = await _collection.Find(x => x.Id == saved.Id).SingleAsync(cancellationToken);
        var prior = System.Text.Json.JsonSerializer.Deserialize<거래고지증적>(crypto.DecryptAtRest("commerce.transaction.disclosure", stored.Payload));
        if (prior is null || prior.Id != record.Id || Fingerprint(prior) != Fingerprint(record))
            throw new 거래보호Exception("IdempotencyConflict", "같은 거래 요청의 판매자·고지 확인 내용이 바뀌었습니다. 새 요청으로 확인해 주세요.");
    }
    public static string Fingerprint(거래고지증적 value) => 판매자확인Service.Hash(System.Text.Json.JsonSerializer.Serialize(new
    { value.ActorUserId, value.SellerUserId, value.SourceCode, value.ClientRequestId, value.NoticeVersion, value.NoticeHash, value.SellerRevision, value.SellerKind, value.StateCode }));
    public sealed class 저장Record { [BsonId] public string Id { get; set; } = string.Empty; public string Payload { get; set; } = string.Empty; }
}
