using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Admin.Operations;
using Ssalddel.Domain.Notifications;
using 살뜰.Data;

namespace Ssalddel.Services.OperationsAlerts;

public sealed class OperationsAlertConflictException : Exception;
public sealed class OperationsAlertCoordinator(SsalddelContext db, IOptionsMonitor<OperationsAlertsOptions> options, TimeProvider clock)
{
    public static string Id(params object[] parts) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", parts)))).ToLowerInvariant();
    public DateTime Now => clock.GetUtcNow().UtcDateTime;
    public async Task ObserveAsync(OperationsObservation observation, DateTime startedAtUtc, CancellationToken ct = default)
    {
        if (observation.ProblemCode.Length == 0) return;
        var id = Id(observation.SourceKind, observation.SourceId, observation.ProblemCode);
        var alert = await db.Set<OperationsAlert>().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (alert is null && !observation.Active) return;
        var notify = false;
        if (alert is null)
        {
            alert = new() { Id = id, SourceKind = observation.SourceKind, SourceId = observation.SourceId, ProblemCode = observation.ProblemCode,
                Severity = observation.Severity, CreatedAtUtc = Now };
            db.Add(alert); notify = observation.ChangedAtUtc >= startedAtUtc;
            alert.ReminderQueued = !notify; // 기존 미해결 건은 최초 일괄 알림·요약으로만 전달한다.
        }
        else if (observation.Active && (alert.Status == "Resolved" || (alert.Severity != "Urgent" && observation.Severity == "Urgent")))
        {
            alert.Episode++; alert.Status = "Open"; alert.CreatedAtUtc = Now; alert.AcknowledgedAtUtc = null;
            alert.AcknowledgedBy = null; alert.ReminderQueued = false; notify = true;
        }
        alert.Severity = observation.Severity; alert.LastVerifiedAtUtc = Now; alert.Revision++;
        if (!observation.Active) alert.Status = "Resolved";
        if (notify && alert.Severity == "Urgent") Queue(alert.Id, alert.Episode, "Initial", Now);
        // SaveChanges is atomic: no alert without its matching outbox after a process interruption.
        await db.SaveChangesAsync(ct);
    }
    public void Queue(string? alertId, int episode, string kind, DateTime at)
    {
        var recipient = options.CurrentValue.OperatorUserId;
        foreach (var channel in new[] { "Push", "Email" })
        {
            var id = Id(alertId ?? at.ToString("yyyyMMdd"), episode, kind, channel, recipient);
            db.Add(new OperationsAlertOutbox { Id = id, AlertId = alertId, Episode = episode, Kind = kind, Channel = channel,
                RecipientUserId = recipient, CreatedAtUtc = Now, NextAttemptUtc = Now });
        }
    }
    public async Task ScheduleAsync(CancellationToken ct = default)
    {
        var now = Now;
        var reminders = await db.Set<OperationsAlert>().Where(x => x.Status == "Open" && x.Severity == "Urgent"
            && !x.ReminderQueued && x.CreatedAtUtc <= now.AddMinutes(-15)).OrderBy(x => x.CreatedAtUtc).Take(50).ToListAsync(ct);
        foreach (var row in reminders) { row.ReminderQueued = true; row.Revision++; Queue(row.Id, row.Episode, "Reminder", now); }
        // KST has no DST. Persisted daily ID prevents restart/overlapping worker duplicate digest creation.
        var local = now.AddHours(9);
        var date = local.Date;
        if (local.Hour >= 9 && !await db.Set<OperationsAlertOutbox>().AnyAsync(x => x.Id == Id(date.ToString("yyyyMMdd"), 0, "Digest", "Push", options.CurrentValue.OperatorUserId), ct))
            Queue(null, 0, "Digest", date);
        await db.SaveChangesAsync(ct);
    }
    public async Task AcknowledgeAsync(string id, string actor, OperationsAlertAcknowledgeRequest input, CancellationToken ct = default)
    {
        if (input.ClientRequestId == Guid.Empty || input.ExpectedRevision < 1) throw new OperationsAlertConflictException();
        var receiptId = Id(actor, input.ClientRequestId);
        var receipt = await db.Set<OperationsAlertReceipt>().SingleOrDefaultAsync(x => x.Id == receiptId, ct);
        var row = await db.Set<OperationsAlert>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException();
        if (receipt is not null)
        {
            if (receipt.AlertId != id || receipt.ExpectedRevision != input.ExpectedRevision || receipt.Episode != row.Episode) throw new OperationsAlertConflictException();
            return;
        }
        if (row.Revision != input.ExpectedRevision || row.Status != "Open" || row.LastVerifiedAtUtc < Now.AddMinutes(-2)) throw new OperationsAlertConflictException();
        row.Status = "Acknowledged"; row.AcknowledgedBy = actor; row.AcknowledgedAtUtc = Now; row.Revision++;
        db.Add(new OperationsAlertReceipt { Id = receiptId, AlertId = id, ActorUserId = actor, RequestId = input.ClientRequestId,
            ExpectedRevision = input.ExpectedRevision, Episode = row.Episode });
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { throw new OperationsAlertConflictException(); }
    }
    public async Task<OperationsAlertsPage> ListAsync(string? status, int page, CancellationToken ct = default)
    {
        page = Math.Clamp(page, 1, 10000);
        var query = db.Set<OperationsAlert>().AsNoTracking();
        if (status is "Open" or "Acknowledged" or "Resolved") query = query.Where(x => x.Status == status);
        var rows = await query.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip((page - 1) * 20).Take(21).ToListAsync(ct);
        var dtos = new List<OperationsAlertDto>();
        foreach (var row in rows.Take(20)) dtos.Add(await DtoAsync(row, ct));
        return new(options.CurrentValue.Enabled, options.CurrentValue.ExternalSendEnabled, dtos, page, rows.Count > 20);
    }
    public async Task<OperationsAlertDto?> DetailAsync(string id, CancellationToken ct = default)
        => await db.Set<OperationsAlert>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) is { } row ? await DtoAsync(row, ct) : null;
    private async Task<OperationsAlertDto> DtoAsync(OperationsAlert row, CancellationToken ct)
        => new(row.Id, row.Episode, row.Revision, row.Severity, row.Status, OperationsAlertPresentation.Title(row.ProblemCode),
            "해당 업무를 열어 진행 상태와 처리 근거를 확인하세요.", OperationsAlertPresentation.Href(row.SourceKind, row.SourceId), row.CreatedAtUtc,
            row.LastVerifiedAtUtc, row.LastVerifiedAtUtc >= Now.AddMinutes(-2),
            await db.Set<OperationsAlertOutbox>().Where(x => x.AlertId == row.Id && x.Episode == row.Episode)
                .Select(x => x.Channel + ":" + x.Status).ToArrayAsync(ct));
}
