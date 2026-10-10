using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.Community;
using Ssalddel.Services.PrivacySupport;
using 살뜰.Data;

namespace Ssalddel.Services.OperationsAlerts;

public sealed class SupportOperationsAlertSource(I보호지원Store store, TimeProvider clock) : IOperationsAlertSource
{
    public string Key => "support";
    public bool RepeatActiveSweep => true;
    public async Task<IReadOnlyList<OperationsObservation>> ReadChangesAsync(DateTime afterUtc, string afterId, int take, CancellationToken ct)
        => (await store.변경목록Async(afterUtc, afterId, take, ct)).SelectMany(Observe).ToArray();
    public async Task<IReadOnlyList<OperationsObservation>> ReadCurrentAsync(string id, CancellationToken ct)
        => await store.조회Async(id, ct) is { } row ? Observe(row) : throw new InvalidOperationException("SourceUnavailable");
    private OperationsObservation[] Observe(보호지원Record row)
    {
        var code = row.Kind switch { "transaction-dispute" => "TransactionDispute", "privacy-incident" => "PrivacyIncident", _ => "SupportDue" };
        var urgent = code != "SupportDue";
        var due = row.ProgressDueAtUtc <= clock.GetUtcNow().UtcDateTime || row.ResultDueAtUtc <= clock.GetUtcNow().UtcDateTime;
        return [new("support", row.CaseId, code, urgent ? "Urgent" : "Warning", row.StatusCode != "closed" && (urgent || due), row.UpdatedAtUtc)];
    }
}

public sealed class MemberOperationsAlertSource(SsalddelContext db, TimeProvider clock) : IOperationsAlertSource
{
    public string Key => "member";
    public bool RepeatActiveSweep => true;
    public async Task<IReadOnlyList<OperationsObservation>> ReadChangesAsync(DateTime afterUtc, string afterId, int take, CancellationToken ct)
    {
        // 반복 keyset sweep은 변경이 없더라도 시간창 경과를 발견한다. 완료 원장은 제외한다.
        var rows = await db.Set<NeighborhoodMemberDelivery>().AsNoTracking()
            .Where(x => x.StateCode != "Delivered" && x.StateCode != "Cancelled" &&
                (x.UpdatedAtUtc > afterUtc || (x.UpdatedAtUtc == afterUtc && string.Compare(x.RequestId, afterId) > 0)))
            .OrderBy(x => x.UpdatedAtUtc).ThenBy(x => x.RequestId).Take(take).ToListAsync(ct);
        var result = new List<OperationsObservation>();
        foreach (var row in rows) result.AddRange(await Observe(row, ct));
        return result;
    }
    public async Task<IReadOnlyList<OperationsObservation>> ReadCurrentAsync(string id, CancellationToken ct)
        => await db.Set<NeighborhoodMemberDelivery>().AsNoTracking().SingleOrDefaultAsync(x => x.RequestId == id, ct) is { } row
            ? await Observe(row, ct) : throw new InvalidOperationException("SourceUnavailable");
    private async Task<OperationsObservation[]> Observe(NeighborhoodMemberDelivery row, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var active = row.StateCode is "Pending" or "Assigned" or "PickedUp" or "Issue";
        var stalledPhoto = active && await db.Set<NeighborhoodMemberPhoto>().AnyAsync(x => x.RequestId == row.RequestId
            && x.StateCode == "Uploading" && x.CreatedAtUtc <= now.AddMinutes(-10), ct);
        var failedPush = active && await db.Set<NeighborhoodMemberOffer>().AnyAsync(x => x.RequestId == row.RequestId
            && x.PushAttempts >= 5 && x.PushStateCode != "Sent", ct);
        var queue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == row.RequestId, ct);
        var syncStalled = queue is not null && queue.UpdatedAt <= now.AddMinutes(-10)
            && (queue.커뮤니티원장동기화시각Utc == null || queue.커뮤니티원장동기화시각Utc < queue.UpdatedAt);
        return [new(Key, row.RequestId, "AfterPickupRecoveryRequired", "Urgent", row.StateCode == "Issue" && row.IssueCode == "AfterPickupRecoveryRequired", row.UpdatedAtUtc),
            new(Key, row.RequestId, "NoCandidate", "Warning", row.StateCode == "Pending" && row.WindowEndUtc <= now, row.UpdatedAtUtc),
            new(Key, row.RequestId, "PhotoUploadStalled", "Warning", stalledPhoto, row.UpdatedAtUtc),
            new(Key, row.RequestId, "NotificationExhausted", "Warning", failedPush, row.UpdatedAtUtc),
            new(Key, row.RequestId, "DispatchLedgerMissing", "Urgent", active && queue is null, row.UpdatedAtUtc),
            new(Key, row.RequestId, "LedgerSyncStalled", "Warning", syncStalled, row.UpdatedAtUtc)];
    }
}
