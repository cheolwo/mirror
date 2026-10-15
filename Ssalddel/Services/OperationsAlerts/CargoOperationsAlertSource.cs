using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.도메인.운송;
using Ssalddel.Contracts.Shipper.Request;

namespace Ssalddel.Services.OperationsAlerts;

public sealed class CargoOperationsAlertSource(SsalddelContext db, TimeProvider clock) : IOperationsAlertSource
{
    public string Key => "cargo";
    public bool RepeatActiveSweep => true;
    public async Task<IReadOnlyList<OperationsObservation>> ReadChangesAsync(DateTime afterUtc, string afterId, int take, CancellationToken ct)
    {
        var recent = clock.GetUtcNow().UtcDateTime.AddDays(-2);
        var rows = await db.Set<비정상운송사건>().AsNoTracking()
            .Where(x => (x.상태Code == 비정상운송사건상태Codes.운영검토대기 || x.UpdatedAt >= recent
                || db.화주운송의뢰.Any(r => r.의뢰Id == x.운송의뢰Id && r.정산상태 == nameof(운임정산상태.비정상운송검토보류)))
                && (x.UpdatedAt > afterUtc || (x.UpdatedAt == afterUtc && string.Compare(x.사건StableId, afterId) > 0)))
            .OrderBy(x => x.UpdatedAt).ThenBy(x => x.사건StableId).Take(take).ToListAsync(ct);
        var result = new List<OperationsObservation>();
        foreach (var row in rows) result.AddRange(await ObserveAsync(row, ct));
        return result;
    }
    public async Task<IReadOnlyList<OperationsObservation>> ReadCurrentAsync(string id, CancellationToken ct)
        => await db.Set<비정상운송사건>().AsNoTracking().SingleOrDefaultAsync(x => x.사건StableId == id, ct) is { } row
            ? await ObserveAsync(row, ct) : throw new InvalidOperationException("SourceUnavailable");
    private async Task<OperationsObservation[]> ObserveAsync(비정상운송사건 row, CancellationToken ct)
    {
        var financialHeld = await db.화주운송의뢰.AsNoTracking().AnyAsync(x => x.의뢰Id == row.운송의뢰Id
            && x.정산상태 == nameof(운임정산상태.비정상운송검토보류), ct);
        var activeHold = await db.비정상운송사건.AsNoTracking().AnyAsync(x => x.운송의뢰Id == row.운송의뢰Id
            && (x.정산보류적용여부 || x.상태Code == 비정상운송사건상태Codes.운영검토대기), ct);
        return [new(Key, row.사건StableId, "CargoIncidentReviewRequired",
        row.현장진행불가 || row.업무통제상태Code == 비정상운송업무통제상태Codes.전체보류 ? "Urgent" : "Warning",
        비정상운송사건상태Codes.열림인가(row.상태Code), row.UpdatedAt),
        new(Key, row.사건StableId, "CollectionEvidenceReviewRequired", "Warning", financialHeld && !activeHold, row.UpdatedAt)];
    }
}
