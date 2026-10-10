using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.도메인.운송;

namespace Ssalddel.Services.OperationsAlerts;

public sealed class CargoOperationsAlertSource(SsalddelContext db, TimeProvider clock) : IOperationsAlertSource
{
    public string Key => "cargo";
    public bool RepeatActiveSweep => true;
    public async Task<IReadOnlyList<OperationsObservation>> ReadChangesAsync(DateTime afterUtc, string afterId, int take, CancellationToken ct)
    {
        var recent = clock.GetUtcNow().UtcDateTime.AddDays(-2);
        var rows = await db.Set<비정상운송사건>().AsNoTracking()
            .Where(x => (x.상태Code == 비정상운송사건상태Codes.운영검토대기 || x.UpdatedAt >= recent)
                && (x.UpdatedAt > afterUtc || (x.UpdatedAt == afterUtc && string.Compare(x.사건StableId, afterId) > 0)))
            .OrderBy(x => x.UpdatedAt).ThenBy(x => x.사건StableId).Take(take).ToListAsync(ct);
        return rows.Select(Observe).ToArray();
    }
    public async Task<IReadOnlyList<OperationsObservation>> ReadCurrentAsync(string id, CancellationToken ct)
        => await db.Set<비정상운송사건>().AsNoTracking().SingleOrDefaultAsync(x => x.사건StableId == id, ct) is { } row
            ? [Observe(row)] : throw new InvalidOperationException("SourceUnavailable");
    private OperationsObservation Observe(비정상운송사건 row) => new(Key, row.사건StableId, "CargoIncidentReviewRequired",
        row.현장진행불가 || row.업무통제상태Code == 비정상운송업무통제상태Codes.전체보류 ? "Urgent" : "Warning",
        비정상운송사건상태Codes.열림인가(row.상태Code), row.UpdatedAt);
}
