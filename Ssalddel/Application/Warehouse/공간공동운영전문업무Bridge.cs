using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Settlement;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Services.Community;
using 살뜰.Data;
using 살뜰.Services.Options;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Warehouse;

/// <summary>공동운영 참조를 실제 전문 원장에서 읽습니다. 공동운영의 단계 이름으로 재고·배송·지급 상태를 쓰지 않습니다.</summary>
public sealed partial class 공간공동운영전문업무Bridge(SsalddelContext db,
    I커뮤니티원장저장소 ledgers, ISsalddelExecutionModePolicy execution,
    IOptions<DistributedFulfillmentOptions> options, TimeProvider clock,
    INeighborhoodSettlementUseCase? settlements = null) : I공간공동운영전문업무Bridge
{
    public async Task<IReadOnlyList<공간공동운영원천상태Dto>> ValidateAsync(string actorUserId,
        IReadOnlyList<공간공동운영전문참조Dto> references, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(references);
        if (references.Count > 32) throw new ArgumentException("전문 업무 참조는 32개 이하로 선택해 주세요.");
        if (references.Any(r => r is null)) throw new ArgumentException("전문 업무 참조를 확인해 주세요.");
        var actor = actorUserId?.Trim();
        if (string.IsNullOrWhiteSpace(actor)) throw new UnauthorizedAccessException();
        var results = new List<공간공동운영원천상태Dto>();
        foreach (var reference in references)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reference is null || string.IsNullOrWhiteSpace(reference.ReferenceId)
                || reference.ReferenceId != reference.ReferenceId.Trim() || reference.ReferenceId.Length > 191)
                throw new ArgumentException("전문 업무 참조를 확인해 주세요.");
            if (!options.Value.Enabled || !execution.IsSimulation || options.Value.Profile != Ssalddel.Contracts.Common.Warehouse.DistributedFulfillmentCodes.Profile)
            {
                results.Add(State(reference, "disabled", "전문 업무의 연습 연결이 비활성화되어 있습니다."));
                continue;
            }
            var parentKind = reference.KindCode switch { "StorageAgreement" or "InboundPlan" or "DistributedWave" => "DistributedSite",
                "DistributedOrder" => "DistributedWave", "MartOrder" => "RootOrderLedger", _ => "" };
            var parents = references.Where(x => x.KindCode == parentKind).Select(x => x.ReferenceId).Distinct().ToArray();
            if (parents.Length > 1 || parents.Length == 1 && !string.IsNullOrEmpty(reference.ParentId) && reference.ParentId != parents[0])
            { results.Add(State(reference, "lineage-mismatch", "같은 거점·주문에 속한 업무를 선택해 주세요.")); continue; }
            var scoped = new 공간공동운영전문참조Dto { KindCode = reference.KindCode, ReferenceId = reference.ReferenceId,
                ParentId = parents.Length == 1 ? parents[0] : reference.ParentId, ExpectedRevision = reference.ExpectedRevision };
            var state = await Read(actor, scoped, cancellationToken);
            results.Add(state.Verified && !await Related(actor, scoped, references, cancellationToken)
                ? State(reference, "lineage-mismatch", "선택한 거점·실물 주문에 속한 업무를 선택해 주세요.") : state);
        }
        return results;
    }

    // 각 원장을 볼 권한과 같은 회차에 함께 묶을 수 있는 관계는 별도로 확인합니다.
    // Wave 선택을 생략해도 주문의 실제 예약→재고묶음→거점 앵커를 우회할 수 없습니다.
    private async Task<bool> Related(string actor, 공간공동운영전문참조Dto reference,
        IReadOnlyList<공간공동운영전문참조Dto> references, CancellationToken ct)
    {
        var sites = references.Where(r => r.KindCode == "DistributedSite").Select(r => r.ReferenceId).Distinct().ToArray();
        if (sites.Length > 1) return false;
        if (sites.Length == 1 && reference.KindCode is "DistributedOrder" or "MartOrder")
        {
            var site = await db.Set<분산거점Record>().AsNoTracking().SingleOrDefaultAsync(s => s.SiteId == sites[0], ct);
            if (site is null) return false;
            if (reference.KindCode == "DistributedOrder")
            {
                var reservations = await db.Set<분산재고예약Record>().AsNoTracking()
                    .Where(r => r.OrderId == reference.ReferenceId).ToArrayAsync(ct);
                if (reservations.Length == 0) return false;
                var lotIds = reservations.Select(r => r.LotId).Distinct().ToArray();
                var lots = await db.Set<분산재고LotRecord>().AsNoTracking().Where(l => Enumerable.Contains(lotIds, l.LotId)).ToArrayAsync(ct);
                if (lots.Length != lotIds.Length || lots.Any(l => l.SiteId != site.SiteId)) return false;
                var stockIds = lots.Select(l => l.InboundItemId).Distinct().ToArray();
                if (await db.입고상품.AsNoTracking().CountAsync(s => Enumerable.Contains(stockIds, s.Id)
                    && s.창고Id == site.WarehouseId, ct) != stockIds.Length) return false;
            }
            else
            {
                if (!long.TryParse(reference.ReferenceId, out var id)) return false;
                var order = await db.마트주문.AsNoTracking().Include(o => o.상품목록).SingleOrDefaultAsync(o => o.Id == id, ct);
                if (order is null || order.상품목록.Count == 0 || order.상품목록.Any(l => l.출고예정Id is null or <= 0)) return false;
                var planIds = order.상품목록.Select(l => l.출고예정Id!.Value).Distinct().ToArray();
                var plans = await db.출고예정.AsNoTracking().Where(p => Enumerable.Contains(planIds, p.Id)).ToArrayAsync(ct);
                if (plans.Length != order.상품목록.Count || plans.Any(p => p.출고창고Id != site.WarehouseId
                    || p.주문참조번호 != order.주문참조번호 || p.입고상품Id is null or <= 0)) return false;
                var stockIds = plans.Select(p => p.입고상품Id!.Value).Distinct().ToArray();
                if (await db.입고상품.AsNoTracking().CountAsync(s => Enumerable.Contains(stockIds, s.Id)
                    && s.창고Id == site.WarehouseId, ct) != stockIds.Length) return false;
            }
        }
        if (reference.KindCode == "RootOrderLedger")
        {
            foreach (var selected in references.Where(r => r.KindCode == "DistributedOrder"))
            {
                var order = await db.Set<분산배송주문Record>().AsNoTracking().SingleOrDefaultAsync(o => o.OrderId == selected.ReferenceId, ct);
                var wave = order is null ? null : await db.Set<분산배송WaveRecord>().AsNoTracking().SingleOrDefaultAsync(w => w.WaveId == order.WaveId, ct);
                if (wave is null || 분산주문원장Metadata.Connections(wave).GetValueOrDefault(selected.ReferenceId)?.RootOrderLedgerId != reference.ReferenceId) return false;
            }
            foreach (var selected in references.Where(r => r.KindCode == "MartOrder"))
                if (!long.TryParse(selected.ReferenceId, out var id)
                    || !await db.마트주문.AsNoTracking().AnyAsync(o => o.Id == id && o.커뮤니티원장Id == reference.ReferenceId, ct)) return false;
        }
        if (reference.KindCode == "Settlement" && sites.Length == 1 && settlements is not null)
        {
            var source = (await settlements.GetAsync(reference.ReferenceId, actor, ct)).Agreement.SourceId;
            var allowed = references.Where(r => r.KindCode is "InboundPlan" or "DistributedOrder" or "MartOrder")
                .Select(r => r.ReferenceId).ToHashSet(StringComparer.Ordinal);
            var lots = await db.Set<분산재고LotRecord>().AsNoTracking().Where(l => l.SiteId == sites[0]).ToArrayAsync(ct);
            foreach (var lot in lots) { allowed.Add(lot.PlanId); allowed.Add(lot.LotId); }
            if (!allowed.Contains(source)) return false;
        }
        return true;
    }

    private async Task<공간공동운영원천상태Dto> Read(string actor, 공간공동운영전문참조Dto r, CancellationToken ct)
    {
        switch (r.KindCode)
        {
            case "DistributedSite":
            {
                var site = await db.Set<분산거점Record>().AsNoTracking().SingleOrDefaultAsync(x => x.SiteId == r.ReferenceId, ct);
                if (site is null || !await SiteParty(actor, site, ct)) return Hidden(r);
                return Verified(r, site.StatusCode, "공간 사용·픽업 동의", site.Revision,
                    site.StatusCode == "Active" && site.OwnerConsented && site.ManagerConsented,
                    Link("warehouse", "sites", site.SiteId));
            }
            case "StorageAgreement":
            {
                var agreement = await db.Set<분산보관합의Record>().AsNoTracking().SingleOrDefaultAsync(x => x.AgreementId == r.ReferenceId, ct);
                if (agreement is null || agreement.SpaceOwnerUserId != actor && agreement.GoodsOwnerUserId != actor
                    || !Parent(r, agreement.SiteId)) return Hidden(r);
                return Verified(r, agreement.StatusCode, "물품·공간 보관 합의", agreement.Revision,
                    agreement.StatusCode == "Active" && agreement.SpaceOwnerConsented && agreement.GoodsOwnerConsented
                    && agreement.StartsAtUtc <= clock.GetUtcNow().UtcDateTime && clock.GetUtcNow().UtcDateTime < agreement.EndsAtUtc,
                    Link("shipper", "sites", agreement.SiteId));
            }
            case "InboundPlan":
            {
                var plan = await db.Set<분산입고계획Record>().AsNoTracking().SingleOrDefaultAsync(x => x.PlanId == r.ReferenceId, ct);
                if (plan is null) return Hidden(r);
                var lots = await db.Set<분산재고LotRecord>().AsNoTracking().Where(x => x.PlanId == plan.PlanId).ToArrayAsync(ct);
                if (lots.Length == 0 || lots.Any(x => !Parent(r, x.SiteId))) return Hidden(r);
                if (plan.CreatorUserId != actor && !lots.All(x => x.OwnerUserId == actor || x.SellerUserId == actor))
                {
                    var siteIds = lots.Select(x => x.SiteId).Distinct().ToArray();
                    var managed = await db.Set<분산거점Record>().AsNoTracking().CountAsync(x => Enumerable.Contains(siteIds, x.SiteId)
                        && (x.OwnerUserId == actor || x.ManagerUserId == actor), ct);
                    if (managed != siteIds.Length) return Hidden(r);
                }
                var stockIds = lots.Select(x => x.InboundItemId).ToArray();
                var stocks = await db.입고상품.AsNoTracking().Where(x => Enumerable.Contains(stockIds, x.Id)).ToDictionaryAsync(x => x.Id, ct);
                var complete = lots.All(l => l.ExpectedQuantity > 0 && l.ReceivedQuantity == l.ExpectedQuantity
                    && stocks.TryGetValue(l.InboundItemId, out var s) && s.입고수량 >= l.ReceivedQuantity
                    && s.SKU == l.Sku && s.판매자UserId == l.SellerUserId && s.소유자UserId == l.OwnerUserId);
                return Verified(r, plan.StatusCode, "사진으로 확인한 실입고", plan.Revision, complete,
                    Link("shipper", "inbound", plan.PlanId));
            }
            case "DistributedOrder":
            {
                var order = await db.Set<분산배송주문Record>().AsNoTracking().SingleOrDefaultAsync(x => x.OrderId == r.ReferenceId, ct);
                if (order is null || !Parent(r, order.WaveId) || !await OrderParty(actor, order, ct)) return Hidden(r);
                var reservations = await db.Set<분산재고예약Record>().AsNoTracking().Where(x => x.OrderId == order.OrderId).ToArrayAsync(ct);
                var allDelivered = reservations.Length > 0 && reservations.All(x => x.Quantity > 0
                    && x.PickedQuantity == x.Quantity && x.DeliveredQuantity == x.Quantity && x.ReturnedQuantity == 0 && x.ReleasedQuantity == 0);
                return Verified(r, order.StatusCode, order.CustomerAcknowledged ? "고객 수령 확인" : "출고·기사 전달·고객 수령",
                    order.Revision, order.StatusCode == "Delivered" && order.CustomerAcknowledged && allDelivered,
                    Link(order.CustomerUserId == actor ? "orderer" : "shipper", "orders", order.WaveId));
            }
            case "DistributedWave":
            {
                var wave = await db.Set<분산배송WaveRecord>().AsNoTracking().SingleOrDefaultAsync(x => x.WaveId == r.ReferenceId, ct);
                if (wave is null) return Hidden(r);
                var orders = await db.Set<분산배송주문Record>().AsNoTracking().Where(x => x.WaveId == wave.WaveId).ToArrayAsync(ct);
                if (wave.CreatorUserId != actor && wave.DriverId != actor && !orders.Any(x => x.SellerUserId == actor || x.CustomerUserId == actor)) return Hidden(r);
                var ids = orders.Select(x => x.OrderId).ToArray();
                var reservations = await db.Set<분산재고예약Record>().AsNoTracking().Where(x => Enumerable.Contains(ids, x.OrderId)).ToArrayAsync(ct);
                if (!string.IsNullOrEmpty(r.ParentId) && reservations.Length > 0)
                {
                    var lots = reservations.Select(x => x.LotId).Distinct().ToArray();
                    if (await db.Set<분산재고LotRecord>().AsNoTracking().CountAsync(l => Enumerable.Contains(lots, l.LotId) && l.SiteId == r.ParentId, ct) != lots.Length) return Hidden(r);
                }
                var completed = orders.Length > 0 && orders.All(x => x.StatusCode == "Delivered" && x.CustomerAcknowledged)
                    && orders.All(o => reservations.Any(r => r.OrderId == o.OrderId))
                    && reservations.All(x => x.Quantity > 0 && x.PickedQuantity == x.Quantity && x.DeliveredQuantity == x.Quantity
                        && x.ReturnedQuantity == 0 && x.ReleasedQuantity == 0);
                return Verified(r, wave.StatusCode, "출고 묶음과 수령 결과", wave.Revision, completed,
                    Link(wave.DriverId == actor ? "cargo-driver" : "warehouse", wave.DriverId == actor ? "round" : "waves", wave.WaveId));
            }
            case "RootOrderLedger":
            {
                var root = await ledgers.원장조회Async(r.ReferenceId, ct);
                if (root is null || root.커뮤니티Id != "platform"
                    || root.원장템플릿Key is not (CommunityLedgerTemplateKeys.Order or CommunityLedgerTemplateKeys.SsalddelMart)
                    || root.생성자UserId != actor && !root.참여자목록.Any(p => p.UserId == actor && p.ParticipationState is not ("철회" or "취소" or "거절"))) return Hidden(r);
                // 자유 편집 가능한 원장의 완료 문구만으로 실물 순환 완료를 확정하지 않습니다.
                return Verified(r, root.상태, "기존 주문 원장 연결", root.Revision, false,
                    "");
            }
            case "MartOrder":
            {
                if (!long.TryParse(r.ReferenceId, out var id) || id <= 0) return Hidden(r);
                var order = await db.마트주문.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
                if (order is null || order.주문자UserId != actor && order.판매자UserId != actor || !Parent(r, order.커뮤니티원장Id ?? "")) return Hidden(r);
                // 수령·출고·완료 근거를 검사하는 기존 reader를 재사용합니다. 문자열 상태로 수령을 만들지 않습니다.
                var receipt = await Ssalddel.Application.Mart.마트수령근거Reader.ReadAsync(db, id, clock.GetUtcNow().UtcDateTime, ct);
                return Verified(r, receipt.PaymentReleaseReady ? "Received" : "ReceiptPending", "마트 고객 수령 근거", null,
                    receipt.PaymentReleaseReady, "");
            }
            case "Settlement":
            {
                if (settlements is null) return State(r, "unavailable", "본인 정산 조회 연결이 필요합니다.");
                try
                {
                    var summary = await settlements.GetAsync(r.ReferenceId, actor, ct);
                    if (!Parent(r, summary.Agreement.SourceId)) return Hidden(r);
                    return Verified(r, summary.Agreement.StatusCode, "본인 작업·보수와 지급 준비", summary.Agreement.Revision,
                        summary.Workflow?.CompletionConfirmed == true && summary.Workflow.PaymentReleaseReady,
                        "/activity-settlements/" + Uri.EscapeDataString(r.ReferenceId));
                }
                catch (NeighborhoodSettlementException e) when (e.StatusCode is 403 or 404) { return Hidden(r); }
            }
            default: return State(r, "unsupported", "지원하지 않는 전문 업무 참조입니다.");
        }
    }

    private async Task<bool> SiteParty(string actor, 분산거점Record site, CancellationToken ct)
        => site.OwnerUserId == actor || site.ManagerUserId == actor
            || await db.Set<분산재고LotRecord>().AsNoTracking().AnyAsync(l => l.SiteId == site.SiteId && (l.OwnerUserId == actor || l.SellerUserId == actor), ct);

    private async Task<bool> OrderParty(string actor, 분산배송주문Record order, CancellationToken ct)
    {
        if (order.SellerUserId == actor || order.CustomerUserId == actor) return true;
        var wave = await db.Set<분산배송WaveRecord>().AsNoTracking().SingleOrDefaultAsync(x => x.WaveId == order.WaveId, ct);
        if (wave?.CreatorUserId == actor) return true;
        if (wave?.DriverId == actor && wave.StatusCode == "Accepted" && clock.GetUtcNow().UtcDateTime <= wave.DeliveryEndUtc)
            return await db.운송원장.AsNoTracking().AnyAsync(q => q.의뢰Id == order.CargoRequestId && q.기사_운송자 == actor
                && q.확정기사Id == actor && q.배차큐단계 != 살뜰.도메인.공통.상태값.배차큐단계.종료, ct);
        return false;
    }

    private static bool Parent(공간공동운영전문참조Dto r, string id) => string.IsNullOrEmpty(r.ParentId) || r.ParentId == id;
    private static string Link(string role, string section, string selected)
        => "/workspace/" + role + "/distributed/" + section + "?selected=" + Uri.EscapeDataString(selected);
    private static 공간공동운영원천상태Dto Hidden(공간공동운영전문참조Dto r) => State(r, "not-visible", "본인에게 허용된 전문 원장을 선택해 주세요.");
    private static 공간공동운영원천상태Dto State(공간공동운영전문참조Dto r, string code, string label) => new()
        { KindCode = r.KindCode, ReferenceId = r.ReferenceId, StatusCode = code, Label = label, RequiresAttention = true };
    private static 공간공동운영원천상태Dto Verified(공간공동운영전문참조Dto r, string status, string label,
        long? revision, bool completed, string href)
    {
        return new() { KindCode = r.KindCode, ReferenceId = r.ReferenceId, StatusCode = status, Label = label,
            Verified = true, Completed = completed, VerifiedRevision = revision,
            RequiresAttention = status is "Cancelled" or "Rejected" or "Revoked" or "RevocationPendingCustody" or "NeedsReplan" or "Expired" or "취소" or "보류",
            NextActionLabel = string.IsNullOrEmpty(href) ? "" : "전문 업무 확인", Href = href };
    }
}
