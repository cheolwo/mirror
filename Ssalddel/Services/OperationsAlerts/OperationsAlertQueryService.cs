using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Admin.Operations;
using Ssalddel.Domain.Notifications;
using 살뜰.Data;

namespace Ssalddel.Services.OperationsAlerts;

/// <summary>운영 알림 목록·상세 조회만 소유합니다. 읽기로 알림 확인·업무 처리·발송을 실행하지 않습니다.</summary>
public sealed class OperationsAlertQueryService(SsalddelContext db, IOptionsMonitor<OperationsAlertsOptions> options,
    TimeProvider clock)
{
    public async Task<OperationsAlertsPage> ListAsync(string? status, int page, CancellationToken ct = default)
    {
        page = Math.Clamp(page, 1, 10000);
        var query = db.Set<OperationsAlert>().AsNoTracking();
        if (status == "Unresolved") query = query.Where(x => x.Status != "Resolved");
        else if (status is "Open" or "Acknowledged" or "Resolved") query = query.Where(x => x.Status == status);

        // 실제 처리 기한은 미설정입니다. 알림 발생 시각을 고객 접수/응답 시각으로 바꾸지 않습니다.
        var rows = await query.OrderBy(x => x.Status == "Resolved" ? 1 : 0)
            .ThenBy(x => x.Severity == "Urgent" ? 0 : 1)
            .ThenBy(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .Skip((page - 1) * 20).Take(21).ToListAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var dtos = new List<OperationsAlertDto>();
        foreach (var row in rows.Take(20)) dtos.Add(await DtoAsync(row, now, ct));
        return new(options.CurrentValue.Enabled, options.CurrentValue.ExternalSendEnabled, dtos, page, rows.Count > 20);
    }

    public async Task<OperationsAlertDto?> DetailAsync(string id, CancellationToken ct = default)
        => await db.Set<OperationsAlert>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) is { } row
            ? await DtoAsync(row, clock.GetUtcNow().UtcDateTime, ct) : null;

    private async Task<OperationsAlertDto> DtoAsync(OperationsAlert row, DateTime nowUtc, CancellationToken ct)
        => new(row.Id, row.Episode, row.Revision, row.Severity, row.Status,
            OperationsAlertPresentation.Title(row.ProblemCode), OperationsAlertPresentation.Action(row.ProblemCode),
            OperationsAlertPresentation.Href(row.SourceKind, row.SourceId), row.CreatedAtUtc,
            row.LastVerifiedAtUtc, OperationsAlertHandlingPolicy.IsSourceCurrent(row, nowUtc),
            await db.Set<OperationsAlertOutbox>().AsNoTracking()
                .Where(x => x.AlertId == row.Id && x.Episode == row.Episode)
                .Select(x => x.Channel + ":" + x.Status).ToArrayAsync(ct))
        {
            Handling = OperationsAlertHandlingPolicy.Describe(row, nowUtc)
        };
}
