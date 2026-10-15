using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Mart;
using 살뜰.Data;
using 살뜰.도메인.마트;
using 살뜰.도메인.창고;

namespace Ssalddel.Services.OperationsAlerts;

/// <summary>개인 주소·사진·당사자 진술 없이 보류 여부와 전문 업무 식별자만 관찰한다.</summary>
public sealed class MartReceiptOperationsAlertSource(SsalddelContext db, TimeProvider clock) : IOperationsAlertSource
{
    public string Key => "mart-receipt";
    public bool RepeatActiveSweep => true;
    public async Task<IReadOnlyList<OperationsObservation>> ReadChangesAsync(DateTime at, string id, int take, CancellationToken ct)
    {
        var rows = await db.Set<마트배송시도>().AsNoTracking().Where(x => x.고객수령근거Json != null
            && (x.UpdatedAtUtc > at || x.UpdatedAtUtc == at && string.Compare(x.시도StableId, id) > 0))
            .OrderBy(x => x.UpdatedAtUtc).ThenBy(x => x.시도StableId).Take(take).ToArrayAsync(ct);
        var result = new List<OperationsObservation>();
        foreach (var row in rows) result.Add(await Observe(row, ct));
        return result;
    }
    public async Task<IReadOnlyList<OperationsObservation>> ReadCurrentAsync(string id, CancellationToken ct)
        => await db.Set<마트배송시도>().AsNoTracking().SingleOrDefaultAsync(x => x.시도StableId == id, ct) is { } row
            ? [await Observe(row, ct)] : throw new InvalidOperationException("SourceUnavailable");
    private async Task<OperationsObservation> Observe(마트배송시도 row, CancellationToken ct)
    {
        var ids = await db.마트주문.AsNoTracking().Where(x => x.주문참조번호 == row.주문참조번호).Select(x => x.Id).Take(2).ToArrayAsync(ct);
        if (ids.Length != 1) throw new InvalidOperationException("SourceUnavailable");
        var snapshot = await 마트수령근거Reader.ReadAsync(db, ids[0], clock.GetUtcNow().UtcDateTime, ct);
        if (!snapshot.StoredEvidenceScopeValid && snapshot.StoredEvidenceValid)
            throw new InvalidOperationException("SourceUnavailable");
        // 근거가 훼손됐거나 연결이 바뀌어도 해결된 것으로 기록하지 않는다.
        var pending = snapshot.Hold is not null && !snapshot.PaymentReleaseReady;
        if (!snapshot.StoredEvidenceValid) pending = true;
        return new(Key, row.시도StableId, "MartReceiptHold", "Warning", pending, row.UpdatedAtUtc);
    }
}

public sealed class DistributedSiteOperationsAlertSource(SsalddelContext db) : IOperationsAlertSource
{
    public string Key => "distributed-site";
    public bool RepeatActiveSweep => true;
    // 기존 거점에는 변경시각이 없어 안정된 ID 순회로 매 주기 현재 상태를 읽는다.
    public async Task<IReadOnlyList<OperationsObservation>> ReadChangesAsync(DateTime at, string id, int take, CancellationToken ct)
        => (await db.Set<분산거점Record>().AsNoTracking().Where(x => string.Compare(x.SiteId, id) > 0)
            .OrderBy(x => x.SiteId).Take(take).ToArrayAsync(ct)).Select(Observe).ToArray();
    public async Task<IReadOnlyList<OperationsObservation>> ReadCurrentAsync(string id, CancellationToken ct)
        => await db.Set<분산거점Record>().AsNoTracking().SingleOrDefaultAsync(x => x.SiteId == id, ct) is { } row
            ? [Observe(row)] : throw new InvalidOperationException("SourceUnavailable");
    private OperationsObservation Observe(분산거점Record row)
        => new(Key, row.SiteId, "StockReleasePending", "Warning", row.StatusCode == "RevocationPendingCustody", DateTime.UnixEpoch);
}
