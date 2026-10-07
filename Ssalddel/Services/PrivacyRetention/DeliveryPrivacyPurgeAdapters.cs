using System.Text.Json;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.PrivacyRetention;
using 살뜰.Data;
using 살뜰.도메인.공통;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;
using 살뜰.도메인.설정;
using Ssalddel.Services.Outbox;

namespace Ssalddel.Services.PrivacyRetention;

public sealed class 음식배송개인정보파기Adapter(SsalddelContext db, I개인정보보존Store store,
    I배송원장개인정보파기Service ledgers) : I개인정보파기Adapter
{
    public string SourceCode => 개인정보파기원천Codes.FoodOrder;
    public async Task<IReadOnlyList<개인정보파기대상>> DiscoverAsync(DateTime cutoff, int limit, CancellationToken ct, long afterSequence = 0)
    {
        var orders = await db.음식주문.AsNoTracking().Where(x => (x.상태 == "전달완료" || x.상태 == "수령확인" || x.상태 == "취소" || x.상태 == "거절")
            && x.Id > afterSequence && x.UpdatedAt <= cutoff && (x.수령인명 != "" || x.수령인연락처 != "" || x.수령지주소 != "" || x.수령지상세주소 != ""
                || x.수령요청사항 != "" || x.수락메모 != null
                || db.음식마트원장동기화Outbox.Any(o => o.원천Id == x.주문번호 && o.동기화유형 != 음식마트원장동기화유형코드.창고출고 && o.PayloadJson != "{}")
                || db.운송원장.Any(t => t.원본의뢰Id == x.주문번호 && t.배차업무유형 == 상태값.배차업무유형.음식배달
                    && (t.하차_도로명주소 != "" || t.하차_상세주소 != "" || t.하차_위도 != null || t.하차_경도 != null || t.메모 != ""))))
            .OrderBy(x => x.Id).Take(limit).ToListAsync(ct);
        return orders.Select(x => new 개인정보파기대상(SourceCode, x.주문번호, x.UpdatedAt, x.주문자UserId, x.Id)).ToArray();
    }
    public async Task<개인정보파기대상?> FindAsync(string recordId, CancellationToken ct)
    {
        var x = await db.음식주문.AsNoTracking().SingleOrDefaultAsync(x => x.주문번호 == recordId, ct);
        if (x is null) return null;
        return new(SourceCode, recordId, Terminal(x) ? x.UpdatedAt : DateTime.MaxValue, x.주문자UserId);
    }
    public async Task<개인정보파기Adapter결과> PurgeAsync(개인정보파기Job job, DateTime now, CancellationToken ct)
    {
        await using var transaction = db.Database.IsRelational() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var order = await db.음식주문.Include(x => x.상품목록).Include(x => x.상태이력).SingleAsync(x => x.주문번호 == job.RecordId, ct);
        if (!Terminal(order)) return new(false, "OrderStillActive", []);
        var transports = await db.운송원장.Where(x => x.원본의뢰Id == job.RecordId && x.배차업무유형 == 상태값.배차업무유형.음식배달).ToListAsync(ct);
        if (transports.Any(x => x.원본의뢰유형 is not ("FoodOrder" or "RestaurantFoodOrder" or "음식점주문")))
            return new(false, "TransportSourceUnverified", []);
        if (transports.Any(x => x.배차큐단계 != 상태값.배차큐단계.종료)) return new(false, "TransportStillActive", []);
        if (transports.Any(x => x.첨부_json != "[]" && !string.IsNullOrWhiteSpace(x.첨부_json)))
            return new(false, "AttachmentsNeedOwnedObjectInventory", []);
        // A worker may already hold an old private payload. Do not report deletion while that lease is processing.
        var outboxes = await db.음식마트원장동기화Outbox.Where(x => x.원천Id == job.RecordId
            && x.동기화유형 != 음식마트원장동기화유형코드.창고출고).ToListAsync(ct);
        if (outboxes.Any(x => x.동기화유형 is not (음식마트원장동기화유형코드.음식주문 or 음식마트원장동기화유형코드.음식배차요청 or 음식마트원장동기화유형코드.음식배달완료WorldProjection)))
            return new(false, "SourceOutboxTypeUnverified", []);
        if (outboxes.Any(x => x.처리상태 == OutboxProcessingStatuses.Processing))
            return new(false, "SourceOutboxStillProcessing", []);
        var terminalAt = order.UpdatedAt;
        await store.SaveEvidenceAsync(job.Id, SourceCode, job.RecordId, JsonSerializer.Serialize(new
        {
            order.주문번호, order.음식점Id, order.주문자UserId, order.총주문금액, order.상태, order.결제수단,
            order.결제승인Id, order.결제승인금액, order.결제승인시각Utc, order.CreatedAt,
            Items = order.상품목록.Select(x => new { x.상품명, x.수량, x.단가 }),
            Transport = transports.Select(x => new { x.의뢰Id, x.운임, x.기사지급예정액, x.기사_운송자 })
        }), terminalAt.AddYears(5), ct);
        order.수령인명 = order.수령인연락처 = order.수령지주소 = order.수령지상세주소 = order.수령요청사항 = string.Empty;
        order.수락메모 = null;
        foreach (var history in order.상태이력) history.사유 = string.Empty;
        var attempts = await db.음식배달시도.Where(x => x.주문번호 == job.RecordId).ToListAsync(ct);
        foreach (var attempt in attempts) { attempt.중단메모 = null; attempt.검토사유 = null; }
        foreach (var t in transports) ScrubTransport(t, preserveBusinessPickup: true);
        foreach (var item in outboxes)
        {
            item.PayloadJson = "{}";
            item.마지막오류 = string.Empty;
            item.처리상태 = 음식개인정보OutboxPolicy.PrivacyExpired;
            item.UpdatedAtUtc = now;
        }
        var ledgerIds = new[] { order.커뮤니티원장Id ?? "food-order:" + job.RecordId }
            .Concat(transports.Select(x => x.커뮤니티원장Id ?? "transport:" + x.의뢰Id)).Distinct().ToArray();
        await 배송개인정보이력파기.파기Async(db, transports, ledgerIds, ct);
        await db.SaveChangesAsync(ct);
        foreach (var id in ledgerIds)
            if (!await ledgers.파기Async(id, SourceCode, job.RecordId, ct)) return new(false, "LinkedLedgerSourceOrRevisionUnverified", []);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return new(true, string.Empty, ["RDB.Food", "RDB.Transport", "RDB.LinkedHistory", "RDB.FoodOutbox", "Mongo.community_ledgers"]);
    }
    private static bool Terminal(음식주문 x) => x.상태 is "전달완료" or "수령확인" or "취소" or "거절";
    internal static void ScrubTransport(운송원장 t, bool preserveBusinessPickup)
    {
        t.하차_도로명주소 = t.하차_상세주소 = t.도착지 = t.메모 = string.Empty;
        t.하차_위도 = t.하차_경도 = null;
        if (!preserveBusinessPickup)
        { t.픽업_도로명주소 = t.픽업_상세주소 = t.출발지 = string.Empty; t.픽업_위도 = t.픽업_경도 = null; }
        // Fare snapshots may embed provider coordinates; retain fare amounts, not arbitrary raw request JSON.
        t.기사제안요금계산근거Json = null;
    }
}

public sealed class 생활배송개인정보파기Adapter(SsalddelContext db, I개인정보보존Store store,
    I배송원장개인정보파기Service ledgers) : I개인정보파기Adapter
{
    public string SourceCode => 개인정보파기원천Codes.NeighborhoodDelivery;
    public async Task<IReadOnlyList<개인정보파기대상>> DiscoverAsync(DateTime cutoff, int limit, CancellationToken ct, long afterSequence = 0)
    {
        var rows = await (from r in db.화주운송의뢰.AsNoTracking()
                          join t in db.운송원장.AsNoTracking() on r.의뢰Id equals t.의뢰Id
                          where r.클라이언트요청Id.StartsWith(NeighborhoodDeliveryRoutes.ClientRequestPrefix)
                          && t.Id > afterSequence && t.배차큐단계 == 상태값.배차큐단계.종료 && t.UpdatedAt <= cutoff
                          && (r.픽업_도로명주소 != "" || r.픽업_연락처_전화번호 != "" || r.하차_도로명주소 != "" || r.하차_연락처_전화번호 != ""
                              || r.요청사항 != "" || r.현장지급메모 != "" || r.화물설명 != "" || t.픽업_도로명주소 != "" || t.하차_도로명주소 != "")
                          orderby t.Id select new { r.의뢰Id, r.주문자UserId, t.UpdatedAt, t.Id }).Take(limit).ToListAsync(ct);
        return rows.Select(x => new 개인정보파기대상(SourceCode, x.의뢰Id, x.UpdatedAt, x.주문자UserId, x.Id)).ToArray();
    }
    public async Task<개인정보파기대상?> FindAsync(string recordId, CancellationToken ct)
    {
        var r = await db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == recordId
            && x.클라이언트요청Id.StartsWith(NeighborhoodDeliveryRoutes.ClientRequestPrefix), ct);
        if (r is null) return null;
        var t = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == recordId, ct);
        return new(SourceCode, recordId, t?.배차큐단계 == 상태값.배차큐단계.종료 ? t.UpdatedAt : DateTime.MaxValue, r.주문자UserId);
    }
    public async Task<개인정보파기Adapter결과> PurgeAsync(개인정보파기Job job, DateTime now, CancellationToken ct)
    {
        await using var transaction = db.Database.IsRelational() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var r = await db.화주운송의뢰.SingleAsync(x => x.의뢰Id == job.RecordId && x.클라이언트요청Id.StartsWith(NeighborhoodDeliveryRoutes.ClientRequestPrefix), ct);
        var t = await db.운송원장.SingleAsync(x => x.의뢰Id == job.RecordId, ct);
        if (t.배차업무유형 != 상태값.배차업무유형.용달운송 || t.원본의뢰유형 != "CargoTransport" || t.원본의뢰Id != r.의뢰Id)
            return new(false, "TransportSourceUnverified", []);
        if (t.배차큐단계 != 상태값.배차큐단계.종료) return new(false, "TransportStillActive", []);
        if (t.첨부_json != "[]" && !string.IsNullOrWhiteSpace(t.첨부_json)) return new(false, "AttachmentsNeedOwnedObjectInventory", []);
        await store.SaveEvidenceAsync(job.Id, SourceCode, job.RecordId, JsonSerializer.Serialize(new
        { r.의뢰Id, r.주문자UserId, r.화주Id, r.최종운임, r.결제상태, r.정산상태, r.현장수금확인일시, r.CreatedAt, t.기사_운송자, t.기사지급예정액 }), t.UpdatedAt.AddYears(5), ct);
        r.픽업_도로명주소 = r.픽업_상세주소 = r.픽업_연락처_이름 = r.픽업_연락처_전화번호 = string.Empty;
        r.하차_도로명주소 = r.하차_상세주소 = r.하차_연락처_이름 = r.하차_연락처_전화번호 = string.Empty;
        r.픽업_위도 = r.픽업_경도 = r.하차_위도 = r.하차_경도 = null;
        r.요청사항 = r.현장지급메모 = r.화물설명 = r.인수증번호 = string.Empty;
        // This field holds the deduplication/consent marker. Preserve it; it is not a user-entered delivery note.
        음식배송개인정보파기Adapter.ScrubTransport(t, false);
        await 배송개인정보이력파기.파기Async(db, [t], [t.커뮤니티원장Id ?? "transport:" + r.의뢰Id], ct);
        await db.SaveChangesAsync(ct);
        if (!await ledgers.파기Async(t.커뮤니티원장Id ?? "transport:" + r.의뢰Id, SourceCode, r.의뢰Id, ct))
            return new(false, "LinkedLedgerSourceOrRevisionUnverified", []);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return new(true, string.Empty, ["RDB.Request", "RDB.Transport", "RDB.LinkedHistory", "Mongo.community_ledgers"]);
    }
}
