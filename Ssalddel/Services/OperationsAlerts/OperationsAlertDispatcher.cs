using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Domain.Notifications;
using 살뜰.Data;

namespace Ssalddel.Services.OperationsAlerts;

public sealed class OperationsAlertDispatcher(SsalddelContext db, IOptionsMonitor<OperationsAlertsOptions> options,
    TimeProvider clock, IEnumerable<IOperationsAlertChannel> channels, 살뜰.Services.Options.ISsalddelExecutionModePolicy execution)
{
    public async Task SendPendingAsync(CancellationToken ct = default)
    {
        var o = options.CurrentValue;
        if (!o.Enabled || !o.ExternalSendEnabled || !execution.IsOperational) return;
        var now = clock.GetUtcNow().UtcDateTime;
        var pending = await db.Set<OperationsAlertOutbox>().AsNoTracking().Where(x =>
            (x.Status == "Pending" || x.Status == "ConfigurationRequired" || (x.Status == "Processing" && x.LeaseUntilUtc < now))
            && x.Attempts < 5 && x.NextAttemptUtc <= now).OrderBy(x => x.CreatedAtUtc).Take(50).ToListAsync(ct);
        foreach (var row in pending)
        {
            // Recheck recipient's server role at send time; appKey/token registration alone grants no authority.
            var admin = await (from membership in db.UserRoles join role in db.Roles on membership.RoleId equals role.Id
                where membership.UserId == row.RecipientUserId && role.Name == "서버관리자" select membership.UserId).AnyAsync(ct);
            var alert = row.AlertId is null ? null : await db.Set<OperationsAlert>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == row.AlertId, ct);
            if (alert is not null && alert.LastVerifiedAtUtc < now.AddMinutes(-2)) continue;
            var invalid = row.RecipientUserId != o.OperatorUserId || !admin || (row.AlertId is not null
                && (alert is null || alert.Status == "Resolved" || alert.Episode != row.Episode || (row.Kind == "Reminder" && alert.Status != "Open")));
            if (row.AlertId is null && !await db.Set<OperationsAlert>().AnyAsync(x => x.Status != "Resolved", ct)) invalid = true;
            var claimed = await db.Set<OperationsAlertOutbox>().Where(x => x.Id == row.Id && x.Revision == row.Revision
                && (x.LeaseUntilUtc == null || x.LeaseUntilUtc < now))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, invalid ? "Suppressed" : "Processing")
                    .SetProperty(x => x.Revision, x => x.Revision + 1).SetProperty(x => x.LeaseUntilUtc, now.AddMinutes(2)), ct);
            if (claimed != 1 || invalid) continue;
            string status;
            try { status = await channels.Single(x => x.Key == row.Channel).SendAsync(row, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch { status = "Failed"; }
            var attempts = row.Attempts + (status == "ConfigurationRequired" ? 0 : 1);
            await db.Set<OperationsAlertOutbox>().Where(x => x.Id == row.Id && x.Revision == row.Revision + 1)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, status == "Failed" && attempts < 5 ? "Pending" : status)
                    .SetProperty(x => x.Attempts, attempts).SetProperty(x => x.ErrorCode, status == "Sent" ? null : status)
                    .SetProperty(x => x.LeaseUntilUtc, (DateTime?)null)
                    .SetProperty(x => x.NextAttemptUtc, now.AddSeconds(status == "ConfigurationRequired" ? 300 : 15 * Math.Pow(2, attempts)))
                    .SetProperty(x => x.Revision, x => x.Revision + 1), ct);
        }
    }
}
