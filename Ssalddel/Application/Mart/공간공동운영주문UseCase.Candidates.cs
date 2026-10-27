using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Mart;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Mart;

public sealed partial class 공간공동운영주문UseCase
{
    public async Task<공간공동운영주문후보목록Response> 후보Async(string ledgerId, string cycleId, CancellationToken ct = default)
    {
        var (record, actor) = await Owned(ledgerId, ct);
        var response = new 공간공동운영주문후보목록Response { LedgerId = record.LedgerId, Revision = record.Revision };
        var cycle = record.Cycles.SingleOrDefault(c => c.CycleId == cycleId && c.TermsRevision == record.TermsRevision && c.StatusCode is "prepared" or "running");
        if (cycle is null || !record.Participants.Any(p => p.UserId == actor && p.RoleCode == 공간공동운영역할Codes.Resident
            && p.StateCode == "accepted" && p.AgreedTermsRevision == record.TermsRevision)) return response;
        var siteIds = cycle.References.Where(r => r.KindCode == "DistributedSite").Select(r => r.ReferenceId).Distinct().ToArray();
        var waveIds = cycle.References.Where(r => r.KindCode == "DistributedWave").Select(r => r.ReferenceId).Distinct().ToArray();
        // 첫 회차는 명시적으로 선택한 한 거점·한 출고 묶음에 한정합니다.
        if (siteIds.Length != 1 || waveIds.Length != 1) return response;
        var site = await db.Set<분산거점Record>().AsNoTracking().SingleOrDefaultAsync(s => s.SiteId == siteIds[0] && s.StatusCode == "Active", ct);
        var wave = await db.Set<분산배송WaveRecord>().AsNoTracking().SingleOrDefaultAsync(w => w.WaveId == waveIds[0] && w.StatusCode == "Open", ct);
        if (site is null || wave is null || Now.UtcDateTime >= wave.CutoffUtc) return response;
        var sellers = record.Participants.Where(p => p.RoleCode == 공간공동운영역할Codes.Supplier && p.StateCode == "accepted"
            && p.AgreedTermsRevision == record.TermsRevision && p.UserId != actor).Select(p => p.UserId).Distinct().ToArray();
        var ownRequests = await db.마트주문요청.AsNoTracking().Where(q => q.요청자UserId == actor && q.상태코드 == 마트주문요청상태코드.제출됨)
            .OrderByDescending(q => q.CreatedAtUtc).Take(50).ToArrayAsync(ct);
        var requestedIds = ownRequests.Select(q => q.공개상품Id).Distinct().ToArray();
        var publicProducts = await db.마트공개상품.AsNoTracking().Where(p => Enumerable.Contains(requestedIds, p.Id)
            && p.공개여부 && p.판매허용여부 && p.판매상품Id.HasValue).ToArrayAsync(ct);
        var salesIds = publicProducts.Select(p => p.판매상품Id!.Value).ToArray();
        var sales = await db.판매상품.AsNoTracking().Where(p => Enumerable.Contains(salesIds, p.Id) && Enumerable.Contains(sellers, p.소유자UserId)).ToArrayAsync(ct);
        var stockIds = sales.Select(s => s.입고상품Id).ToArray();
        var lots = await db.Set<분산재고LotRecord>().AsNoTracking().Where(l => l.SiteId == site.SiteId
            && Enumerable.Contains(stockIds, l.InboundItemId) && Enumerable.Contains(sellers, l.SellerUserId)).ToArrayAsync(ct);
        var cargo = await db.화주운송의뢰.AsNoTracking().Where(c => c.주문자UserId == actor && c.화주Id != null && Enumerable.Contains(sellers, c.화주Id)
            && c.하차_시간창_종료일시.HasValue && c.하차_시간창_종료일시.Value > Now.UtcDateTime).OrderBy(c => c.Id).Take(100).ToArrayAsync(ct);
        var result = new List<공간공동운영주문후보Dto>();
        foreach (var request in ownRequests)
        {
            if (Extension(record).Orders.Any(o => o.Terms.OrderRequestId == request.Id)) continue;
            var product = publicProducts.SingleOrDefault(p => p.Id == request.공개상품Id && p.판매가 == request.단가Snapshot);
            if (product is null) continue;
            var sale = sales.SingleOrDefault(s => s.Id == product.판매상품Id); if (sale is null) continue;
            foreach (var lot in lots.Where(l => l.InboundItemId == sale.입고상품Id && l.SellerUserId == sale.소유자UserId))
            foreach (var delivery in cargo.Where(c => c.화주Id == sale.소유자UserId && c.화물수량 == request.수량))
            {
                var terms = new 공간공동운영주문조건Dto { OrderRequestId = request.Id, PublicProductId = product.Id,
                    Quantity = request.수량, UnitPriceKrw = request.단가Snapshot, CycleId = cycleId,
                    SiteId = site.SiteId, LotId = lot.LotId, WaveId = wave.WaveId, CargoRequestId = delivery.의뢰Id,
                    DeadlineUtc = DateTime.SpecifyKind(delivery.하차_시간창_종료일시!.Value > wave.DeliveryEndUtc
                        ? delivery.하차_시간창_종료일시.Value : wave.DeliveryEndUtc, DateTimeKind.Utc) };
                try
                {
                    await ReadSource(record, actor, terms, true, ct);
                    result.Add(new() { ProductName = product.상품명, Terms = terms,
                        Label = $"{product.상품명} {request.수량}개 · {request.합계Snapshot:N0}원 · {site.PublicLabel} · {terms.DeadlineUtc.AddHours(9):MM/dd HH:mm}까지" });
                }
                catch (SpaceOrderException) { /* 현재 조건이 맞지 않는 후보는 확정 가능한 것처럼 노출하지 않습니다. */ }
                if (result.Count == 30) { response.Items = result; return response; }
            }
        }
        response.Items = result;
        return response;
    }
}
