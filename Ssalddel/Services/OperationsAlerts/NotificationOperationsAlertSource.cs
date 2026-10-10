using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.Notifications;
using 살뜰.Data;

namespace Ssalddel.Services.OperationsAlerts;
public sealed class NotificationOperationsAlertSource(SsalddelContext db) : IOperationsAlertSource
{
    public string Key=>"operator-outbox";
    public bool RepeatActiveSweep=>true;
    public async Task<IReadOnlyList<OperationsObservation>> ReadChangesAsync(DateTime at,string id,int take,CancellationToken ct)
    {
        var rows=await db.Set<OperationsAlertOutbox>().AsNoTracking().Where(x=>x.Status=="Failed" && x.Attempts>=5
            && (x.CreatedAtUtc>at||(x.CreatedAtUtc==at&&string.Compare(x.Id,id)>0))).OrderBy(x=>x.CreatedAtUtc).ThenBy(x=>x.Id).Take(take).ToListAsync(ct);
        return rows.Select(Observe).ToArray();
    }
    public async Task<IReadOnlyList<OperationsObservation>> ReadCurrentAsync(string id,CancellationToken ct)
        => await db.Set<OperationsAlertOutbox>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct) is {} row ? [Observe(row)] : throw new InvalidOperationException("SourceUnavailable");
    private OperationsObservation Observe(OperationsAlertOutbox row)=>new(Key,row.Id,"NotificationExhausted","Warning",row.Status=="Failed"&&row.Attempts>=5,row.CreatedAtUtc);
}
