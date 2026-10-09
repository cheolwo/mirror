using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Services.Community;
using 살뜰.Data;
using 살뜰.Services.Options;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Warehouse;

public sealed record DistributedRootOrderSnapshot(string RootOrderLedgerId, long Revision, string BusinessDigest);
public sealed record DistributedOrderLedgerConnectionState(string StatusCode, long? RootRevision = null, string? ErrorCode = null);

/// <summary>기존 분산 순회의 보호된 근거 JSON에 저장하는 연결 계획입니다. 원장의 업무 상태를 복제하지 않습니다.</summary>
public sealed class DistributedOrderLedgerConnection
{
    public string OrderId { get; set; } = "";
    public string RootOrderLedgerId { get; set; } = "";
    public long ExpectedRootRevision { get; set; }
    public string RootBusinessDigest { get; set; } = "";
    public string SellerUserId { get; set; } = "";
    public string CustomerUserId { get; set; } = "";
    public string CargoRequestId { get; set; } = "";
    public IReadOnlyList<long> OutboundPlanIds { get; set; } = [];
    public Guid RequestId { get; set; }
    public Guid OrderRequestId { get; set; }
    public DateTime RecordedAtUtc { get; set; }
}

public interface I분산주문원장연결Adapter
{
    Task<IReadOnlyList<DistributedRootOrderCandidateDto>> CandidatesAsync(string actorUserId,
        string sellerUserId, string customerUserId, CancellationToken ct)
        => throw new DistributedFulfillmentException("DistributedOrderLedgerAdapterUnavailable", 503);
    Task<DistributedRootOrderSnapshot> ValidateAsync(string actorUserId, string rootId, long expectedRevision,
        string sellerUserId, string customerUserId, CancellationToken ct);
    Task<DistributedOrderLedgerConnectionState> SynchronizeAsync(string actorUserId, DistributedOrderLedgerConnection connection, CancellationToken ct);
    Task<DistributedOrderLedgerConnectionState> ReadAsync(string actorUserId, DistributedOrderLedgerConnection connection, CancellationToken ct);
    Task<DistributedOrderLedgerReferenceDto?> FindForChildAsync(string actorUserId, string rootOrderLedgerId,
        string childLedgerId, CancellationToken ct);
}

/// <summary>SQL 업무가 확정된 뒤 기존 Mongo 원장 동기화와 주문 포함 연결을 재개합니다. 조회에는 쓰기 효과가 없습니다.</summary>
public sealed class 분산주문원장연결Adapter(SsalddelContext db, I커뮤니티원장저장소 ledgers,
    I주문원장통합UseCase orderLedgers, I음식마트원장Mongo동기화Service outboundSync,
    I운송원장Mongo동기화Service cargoSync, ISsalddelExecutionModePolicy execution,
    IOptions<DistributedFulfillmentOptions> options) : I분산주문원장연결Adapter
{
    private static void Require(bool ok, string code, int status = 409)
        => 분산물류UseCase.Require(ok, code, status);
    private static bool Participant(커뮤니티원장Dto root, string user)
        => root.참여자목록.Any(x => x.UserId == user && Active(x.ParticipationState))
            || root.생성자UserId == user && !root.참여자목록.Any(x => x.UserId == user);
    private static bool Customer(커뮤니티원장Dto root, string user)
        => Participant(root, user) && (root.생성자UserId == user || root.참여자목록.Any(x => x.UserId == user
            && (x.RoleLabel is "주문자" or "구매자" or "Buyer")
            && Active(x.ParticipationState)));
    private static bool Active(string? state) => state is not "철회" and not "취소" and not "거절";
    private static bool Open(커뮤니티원장Dto root) => root.상태 is 커뮤니티원장상태.초안 or 커뮤니티원장상태.진행중;
    public async Task<IReadOnlyList<DistributedRootOrderCandidateDto>> CandidatesAsync(string actorUserId,
        string sellerUserId, string customerUserId, CancellationToken ct)
    {
        Require(actorUserId == sellerUserId && !string.IsNullOrWhiteSpace(customerUserId), "DistributedOrderLedgerPartyRequired", 403);
        var roots = await ledgers.원장목록조회Async(new()
        {
            커뮤니티Id = "platform", 접근UserId = actorUserId,
            원장템플릿Keys = [CommunityLedgerTemplateKeys.Order, CommunityLedgerTemplateKeys.SsalddelMart], Limit = 200
        }, ct);
        return roots.Where(root => root.커뮤니티Id == "platform"
                && root.원장템플릿Key is CommunityLedgerTemplateKeys.Order or CommunityLedgerTemplateKeys.SsalddelMart)
            .Where(root => Open(root) && Participant(root, actorUserId) && Participant(root, sellerUserId) && Customer(root, customerUserId))
            .Where(root => root.Revision >= 0 && !string.IsNullOrWhiteSpace(root.원장Id))
            .Select(root => new DistributedRootOrderCandidateDto(root.원장Id, root.Revision,
                string.IsNullOrWhiteSpace(root.제목) ? "제목 없는 주문" : root.제목, root.상태)).ToArray();
    }
    private static string BusinessDigest(커뮤니티원장Dto root)
    {
        var input = new
        {
            root.원장Id, root.커뮤니티Id, root.원장템플릿Key, root.생성자UserId,
            Parties = root.참여자목록.OrderBy(x => x.UserId, StringComparer.Ordinal).ThenBy(x => x.RoleLabel, StringComparer.Ordinal)
                .Select(x => new { x.UserId, x.RoleLabel, x.ParticipationState }),
            Items = root.블록목록.Where(x => x.BlockType is CommunityLedgerBlockTypes.Order or CommunityLedgerBlockTypes.Item)
                .OrderBy(x => x.BlockId, StringComparer.Ordinal).Select(x => new { x.BlockId, x.BlockType,
                    Data = x.Data.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToArray() })
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input))));
    }
    private async Task<커뮤니티원장Dto> Root(string actor, string rootId, string seller, string customer, CancellationToken ct)
    {
        Require(actor == seller || actor == customer, "DistributedOrderLedgerPartyRequired", 403);
        var root = await ledgers.원장조회Async(rootId, ct)
            ?? throw new DistributedFulfillmentException("DistributedRootOrderLedgerNotFound", 404);
        Require((root.원장템플릿Key is CommunityLedgerTemplateKeys.Order or CommunityLedgerTemplateKeys.SsalddelMart)
            && root.커뮤니티Id == "platform", "DistributedRootOrderLedgerKindInvalid", 400);
        Require(Participant(root, actor) && Participant(root, seller) && Customer(root, customer),
            "DistributedRootOrderLedgerPartyMismatch", 403);
        return root;
    }
    public async Task<DistributedRootOrderSnapshot> ValidateAsync(string actorUserId, string rootId, long expectedRevision,
        string sellerUserId, string customerUserId, CancellationToken ct)
    {
        Require(expectedRevision >= 0 && !string.IsNullOrWhiteSpace(rootId) && rootId == rootId.Trim() && rootId.Length <= 191,
            "DistributedRootOrderLedgerInputInvalid", 400);
        var root = await Root(actorUserId, rootId, sellerUserId, customerUserId, ct);
        Require(Open(root), "DistributedRootOrderLedgerClosed");
        Require(root.Revision == expectedRevision, "DistributedRootOrderLedgerRevisionConflict");
        return new(root.원장Id, root.Revision, BusinessDigest(root));
    }
    private async Task<출고예정[]> Source(string actor, DistributedOrderLedgerConnection c, CancellationToken ct)
    {
        Require(actor == c.SellerUserId || actor == c.CustomerUserId, "DistributedOrderLedgerPartyRequired", 403);
        var order = await db.Set<분산배송주문Record>().AsNoTracking().SingleOrDefaultAsync(x => x.OrderId == c.OrderId, ct);
        Require(order is not null && order.CargoRequestId == c.CargoRequestId && order.SellerUserId == c.SellerUserId
            && order.CustomerUserId == c.CustomerUserId, "DistributedOrderLedgerSourceChanged");
        var cargo = await db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == c.CargoRequestId, ct);
        Require(cargo is not null && cargo.화주Id == c.SellerUserId && cargo.주문자UserId == c.CustomerUserId
            && 분산물류UseCase.CargoDigest(cargo) == order!.CargoEvidenceDigest, "DistributedOrderLedgerSourceChanged");
        var ids = await db.Set<분산재고예약Record>().AsNoTracking().Where(x => x.OrderId == c.OrderId).Select(x => x.OutboundPlanId).ToArrayAsync(ct);
        Require(ids.Length > 0 && ids.Order().SequenceEqual(c.OutboundPlanIds.Order()), "DistributedOrderLedgerSourceChanged");
        var outbounds = await db.출고예정.AsNoTracking().Where(x => Enumerable.Contains(ids, x.Id)).ToArrayAsync(ct);
        Require(outbounds.Length == ids.Length && outbounds.All(x => x.판매자UserId == c.SellerUserId
            && x.주문자UserId == c.CustomerUserId && x.운송의뢰Id == c.CargoRequestId), "DistributedOrderLedgerSourceChanged");
        // 과거 표시 참조를 영구 ID로 바꾸거나, 같은 참조의 다른 출고를 덮어쓰지 않습니다.
        foreach (var group in outbounds.GroupBy(x => new { x.주문참조번호, Template = OutboundTemplate(x) }))
        {
            var referenced = await db.출고예정.AsNoTracking().Where(x => x.주문참조번호 == group.Key.주문참조번호).ToArrayAsync(ct);
            var template = OutboundTemplate(group.First());
            var referencedIds = referenced.Where(x => OutboundTemplate(x) == template).Select(x => x.Id).ToArray();
            Require(referencedIds.Order().SequenceEqual(group.Select(x => x.Id).Order()), "DistributedLegacyOutboundReferenceAmbiguous");
        }
        return outbounds;
    }
    private static string OutboundTemplate(출고예정 outbound)
        => 음식마트원장Mongo동기화Builder.출고원장저장요청생성([outbound], [], 원장템플릿Key: outbound.커뮤니티원장템플릿Key).원장템플릿Key;
    private static string OutboundId(출고예정 outbound)
        => 음식마트원장Mongo동기화Builder.출고원장Id생성(outbound.주문참조번호, OutboundTemplate(outbound));
    private async Task<커뮤니티원장Dto> FrozenRoot(string actor, DistributedOrderLedgerConnection c, CancellationToken ct)
    {
        var root = await Root(actor, c.RootOrderLedgerId, c.SellerUserId, c.CustomerUserId, ct);
        Require(BusinessDigest(root) == c.RootBusinessDigest, "DistributedRootOrderLedgerChanged");
        return root;
    }
    private static bool Linked(커뮤니티원장Dto root, string id, string role)
        => root.포함원장목록.Any(x => x.원장Id == id && x.역할 == role && x.필수여부);
    private static DistributedOrderLedgerConnectionState Failure(DistributedFulfillmentException error)
        => new(error.Status is 400 or 401 or 403 or 404 ? "Blocked" : "Pending", ErrorCode: error.Code);
    public async Task<DistributedOrderLedgerConnectionState> ReadAsync(string actor, DistributedOrderLedgerConnection c, CancellationToken ct)
    {
        try
        {
            var outbounds = await Source(actor, c, ct); var root = await FrozenRoot(actor, c, ct);
            var transport = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == c.CargoRequestId, ct);
            var transportId = transport?.커뮤니티원장Id ?? 운송원장Mongo동기화Builder.원장Id생성(c.CargoRequestId);
            var outboundIds = outbounds.Select(OutboundId).Distinct().ToArray();
            var referencesMatch = Linked(root, transportId, 주문원장포함역할.운송) && outboundIds.All(x => Linked(root, x, 주문원장포함역할.창고출고));
            if (!referencesMatch) return new("Pending", root.Revision);
            foreach (var childId in outboundIds.Append(transportId))
                if (await ledgers.원장조회Async(childId, ct) is null) return new("Pending", root.Revision, "DistributedChildLedgerNotFound");
            return new("Connected", root.Revision);
        }
        catch (DistributedFulfillmentException e) { return Failure(e); }
        catch (Exception) when (!ct.IsCancellationRequested) { return new("Pending", ErrorCode: "DistributedOrderLedgerQueryPending"); }
    }
    public async Task<DistributedOrderLedgerConnectionState> SynchronizeAsync(string actor, DistributedOrderLedgerConnection c, CancellationToken ct)
    {
        // 재접수 권한은 저장 당시 판정이 아니라 현재 당사자 관계에서 다시 확인합니다.
        try
        {
            var outbounds = await Source(actor, c, ct);
            await FrozenRoot(actor, c, ct);
            var children = new List<(커뮤니티원장Dto Ledger, string Role)>();
            foreach (var group in outbounds.GroupBy(x => new { x.주문참조번호, Template = OutboundTemplate(x) }))
            {
                var inboundIds = group.Where(x => x.입고요청Id.HasValue).Select(x => x.입고요청Id!.Value).Distinct().ToArray();
                var inbounds = await db.입고요청.AsNoTracking().Where(x => Enumerable.Contains(inboundIds, x.Id)).ToArrayAsync(ct);
                var child = await outboundSync.출고원장동기화Async(group.ToArray(), inbounds, actor,
                    원장템플릿Key: OutboundTemplate(group.First()),
                    cancellationToken: ct) ?? throw new DistributedFulfillmentException("DistributedOutboundLedgerSyncPending");
                children.Add((child, 주문원장포함역할.창고출고));
            }
            var transport = await db.운송원장.AsNoTracking().SingleAsync(x => x.의뢰Id == c.CargoRequestId, ct);
            var cargoLedger = await cargoSync.운송실행투영동기화Async(transport, actor, ct)
                ?? throw new DistributedFulfillmentException("DistributedCargoLedgerSyncPending");
            children.Add((cargoLedger, 주문원장포함역할.운송));
            foreach (var child in children)
            {
                var root = await FrozenRoot(actor, c, ct);
                Require(child.Ledger.커뮤니티Id == root.커뮤니티Id, "DistributedRootOrderLedgerCommunityMismatch");
                if (Linked(root, child.Ledger.원장Id, child.Role)) continue;
                var result = await orderLedgers.하위원장연결Async(root.원장Id, new()
                {
                    하위원장Id = child.Ledger.원장Id, 역할 = child.Role, 필수여부 = true, 기대Revision = root.Revision
                }, actor, ct);
                Require(result.IsSuccess, "DistributedRootOrderLedgerLinkPending");
            }
            return await ReadAsync(actor, c, ct);
        }
        catch (DistributedFulfillmentException e) { return Failure(e); }
        catch (Exception) when (!ct.IsCancellationRequested) { return new("Pending", ErrorCode: "DistributedOrderLedgerSyncPending"); }
    }
    public async Task<DistributedOrderLedgerReferenceDto?> FindForChildAsync(string actor, string rootId, string childId, CancellationToken ct)
    {
        // 일반 주문 조회는 아직 활성화하지 않은 분산 기능의 테이블이나 설정을 요구하지 않습니다.
        if (!options.Value.Enabled || !execution.IsSimulation || options.Value.Profile != DistributedFulfillmentCodes.Profile
            || !db.Database.IsRelational()) return null;
        var ownOrders = await db.Set<분산배송주문Record>().AsNoTracking()
            .Where(x => x.CustomerUserId == actor || x.SellerUserId == actor).ToArrayAsync(ct);
        var waveIds = ownOrders.Select(x => x.WaveId).Distinct().ToArray();
        var waves = await db.Set<분산배송WaveRecord>().AsNoTracking().Where(x => Enumerable.Contains(waveIds, x.WaveId)).ToArrayAsync(ct);
        foreach (var wave in waves)
        foreach (var c in 분산주문원장Metadata.Connections(wave).Values.Where(x => x.RootOrderLedgerId == rootId
            && ownOrders.Any(o => o.OrderId == x.OrderId && o.WaveId == wave.WaveId)))
        {
            try
            {
                var outbounds = await Source(actor, c, ct); var root = await Root(actor, rootId, c.SellerUserId, c.CustomerUserId, ct);
                var transport = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == c.CargoRequestId, ct);
                var transportId = transport?.커뮤니티원장Id ?? 운송원장Mongo동기화Builder.원장Id생성(c.CargoRequestId);
                var ownChild = childId == transportId && Linked(root, childId, 주문원장포함역할.운송)
                    || outbounds.Any(x => OutboundId(x) == childId)
                        && Linked(root, childId, 주문원장포함역할.창고출고);
                if (!ownChild) continue;
                return new(wave.WaveId, c.OrderId, rootId, (await ReadAsync(actor, c, ct)).StatusCode);
            }
            catch (DistributedFulfillmentException) { }
        }
        return null;
    }
}
