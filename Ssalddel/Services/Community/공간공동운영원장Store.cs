using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Ssalddel.Contracts.Common.Community;
using 살뜰.Services.Options;

namespace Ssalddel.Services.Community;

/// <summary>
/// 기존 community_ledgers의 전용 블록에만 보호 상태를 저장합니다. 최초 생성은 InsertOne으로
/// 동일 요청의 경합 덮어쓰기를 막고, 변경은 기존 원장 저장소의 revision/관계 fence를 재사용합니다.
/// </summary>
public sealed class 공간공동운영원장Store : I공간공동운영Store
{
    public const string BlockId = "space-cooperation-private-v1";
    public const string CanonicalMarker = "SpaceCooperationCanonical";
    private const string PayloadKey = "ProtectedPayload";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly I커뮤니티원장저장소 ledgers;
    private readonly IMongoCollection<커뮤니티원장문서> collection;
    private readonly IDataProtector protector;

    public 공간공동운영원장Store(I커뮤니티원장저장소 ledgers, IMongoClient mongo, IOptions<MongoDbOptions> options,
        IDataProtectionProvider protection)
    {
        this.ledgers = ledgers;
        collection = 커뮤니티원장MongoCollectionFactory.Create(mongo, options);
        protector = protection.CreateProtector("Ssalddel.SpaceCooperation.LedgerBlock.v1");
    }

    public async Task<공간공동운영Record?> 조회Async(string ledgerId, CancellationToken cancellationToken = default)
    {
        var ledger = await ledgers.원장조회Async(ledgerId, cancellationToken);
        return ledger is null || ledger.원장템플릿Key != CommunityLedgerTemplateKeys.SpaceCooperation
            || ledger.외부참조.GetValueOrDefault(CanonicalMarker) != "v1" ? null : Read(ledger);
    }

    public async Task<IReadOnlyList<공간공동운영Record>> 목록Async(int skip = 0, int take = 200, CancellationToken cancellationToken = default)
        => (await ledgers.원장목록조회Async(new() { 원장템플릿Key = CommunityLedgerTemplateKeys.SpaceCooperation,
            외부참조조건 = new Dictionary<string, string> { [CanonicalMarker] = "v1" },
            Skip = Math.Max(0, skip), Limit = Math.Clamp(take, 1, 200) }, cancellationToken)).Select(Read).ToArray();

    public async Task<bool> 생성Async(공간공동운영Record record, CancellationToken cancellationToken = default)
    {
        if (record.Revision != 1) throw new InvalidOperationException("최초 원장 판본은 1이어야 합니다.");
        // 기존 저장소가 소유하는 unique 원장 ID 인덱스를 먼저 확인합니다.
        await ledgers.원장목록조회Async(new() { 원장템플릿Key = CommunityLedgerTemplateKeys.SpaceCooperation, Limit = 1 }, cancellationToken);
        var eventId = 커뮤니티원장EventIdFactory.Create(record.LedgerId, record.Revision);
        var document = new 커뮤니티원장문서
        {
            Id = ObjectId.GenerateNewId(), 원장Id = record.LedgerId, Revision = record.Revision,
            커뮤니티Id = "platform", 원장템플릿Key = CommunityLedgerTemplateKeys.SpaceCooperation,
            제목 = "공간 공동운영 제안", 원함 = "작은 공간의 자발적 공동 운영을 검토합니다.",
            상태 = 커뮤니티원장상태.초안, 현재단계Key = "gathering",
            대상OsCode = CommunityLedgerOperatingSystemCodes.CommunityTrust, 대상OsName = "커뮤니티 신뢰 OS",
            생성자UserId = record.OwnerUserId, 생성자표시명 = "공간 제안자",
            외부참조 = new() { [CanonicalMarker] = "v1" },
            블록목록 = [new() { BlockId = BlockId, BlockType = CommunityLedgerBlockTypes.Generic,
                Title = "공간 공동운영 보호 기록", Data = new() { [PayloadKey] = Protect(record) } }],
            참여자목록 = [new() { UserId = record.OwnerUserId, DisplayName = "공간 제안자", RoleLabel = "제안자", ParticipationState = "비구속 관심" }],
            생성시각Utc = record.CreatedAtUtc.UtcDateTime, 수정시각Utc = record.UpdatedAtUtc.UtcDateTime,
            수정자 = record.OwnerUserId, 투영상태 = 커뮤니티원장투영상태.대기, 투영EventId = eventId,
            투영변경유형 = 커뮤니티원장변경유형값.저장, 투영변경메모 = "공간 공동운영 제안 생성",
            투영발생시각Utc = record.UpdatedAtUtc.UtcDateTime, 투영다음시도시각Utc = record.UpdatedAtUtc.UtcDateTime.AddSeconds(5)
        };
        try { await collection.InsertOneAsync(document, cancellationToken: cancellationToken); return true; }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey) { return false; }
    }

    public async Task<bool> 교체Async(공간공동운영Record record, long expectedRevision, string actorUserId, CancellationToken cancellationToken = default)
    {
        if (record.Revision != expectedRevision + 1) throw new InvalidOperationException("원장 판본은 한 번 증가해야 합니다.");
        var current = await ledgers.원장조회Async(record.LedgerId, cancellationToken);
        if (current is null || current.Revision != expectedRevision || current.원장템플릿Key != CommunityLedgerTemplateKeys.SpaceCooperation
            || current.외부참조.GetValueOrDefault(CanonicalMarker) != "v1") return false;
        var prior = Read(current);
        if (prior.OwnerUserId != record.OwnerUserId || prior.ExecutionMode != record.ExecutionMode
            || prior.CreateRequestId != record.CreateRequestId || prior.CreateFingerprint != record.CreateFingerprint)
            throw new InvalidOperationException("공간 제안 원본의 소유자·실행 모드·생성 근거는 바꿀 수 없습니다.");
        var blocks = current.블록목록.Select(block => block.BlockId != BlockId ? block : new 커뮤니티원장블록Dto
        {
            BlockId = block.BlockId, BlockType = block.BlockType, Title = block.Title, State = block.State,
            담당자목록 = block.담당자목록, Data = new Dictionary<string, string>(block.Data) { [PayloadKey] = Protect(record) }
        }).ToArray();
        try
        {
            await ledgers.원장저장Async(new()
            {
                원장Id = current.원장Id, 기대Revision = expectedRevision, 커뮤니티Id = current.커뮤니티Id,
                원장템플릿Key = current.원장템플릿Key, 제목 = current.제목, 원함 = current.원함,
                상태 = current.상태, 현재단계Key = current.현재단계Key,
                대상OsCode = current.대상OsCode, 대상OsName = current.대상OsName,
                생성자UserId = current.생성자UserId, 생성자표시명 = current.생성자표시명,
                블록목록 = blocks, 참여자목록 = current.참여자목록, 포함원장목록 = current.포함원장목록,
                다이어그램스냅샷 = current.다이어그램스냅샷, 외부참조 = current.외부참조, 확장속성 = current.확장속성
            }, actorUserId, cancellationToken);
            return true;
        }
        catch (InvalidOperationException)
        {
            var latest = await ledgers.원장조회Async(record.LedgerId, cancellationToken);
            if (latest?.Revision != expectedRevision) return false;
            throw;
        }
    }

    private string Protect(공간공동운영Record record)
    {
        var json = JsonSerializer.Serialize(record, Json);
        if (json.Length > 512_000) throw new InvalidOperationException("공간 공동운영 원장 기록 한도를 초과했습니다.");
        return protector.Protect(json);
    }

    private 공간공동운영Record Read(커뮤니티원장Dto ledger)
    {
        var blocks = ledger.블록목록.Where(block => block.BlockId == BlockId).ToArray();
        if (blocks.Length != 1 || !blocks[0].Data.TryGetValue(PayloadKey, out var payload))
            throw new InvalidDataException("공간 공동운영 보호 블록을 확인할 수 없습니다.");
        var record = JsonSerializer.Deserialize<공간공동운영Record>(protector.Unprotect(payload), Json)
            ?? throw new InvalidDataException("공간 공동운영 보호 상태가 없습니다.");
        if (record.LedgerId != ledger.원장Id || record.Revision != ledger.Revision || record.OwnerUserId != ledger.생성자UserId)
            throw new InvalidDataException("공간 공동운영 보호 상태와 현재 원장 판본이 다릅니다.");
        return record;
    }
}
