using Ssalddel.Contracts.Admin.Operations;
using Ssalddel.Domain.Notifications;
using Ssalddel.Services.OperationsAlerts;
using Ssalddel.Ui.Common.Areas.BackOffice.ViewModels;

namespace RoleWorkspacePreview;

/// <summary>기존 운영 알림 화면의 메모리 내 예시. 제품 DB·외부 알림·원장 명령을 실행하지 않습니다.</summary>
public sealed class CargoOperatingR21PreviewFixture : IOperationsAlertsClient
{
    public static string AlertId => new('a', 64);
    private string scenario = "";
    private bool failedRead;
    private bool lostConfirmation;
    private OperationsAlert[] rows = [];
    public int ReadCount { get; private set; }
    public int ConfirmCount { get; private set; }

    public void SelectScenario(string? value)
    {
        var next = value is "read-failure" or "lost-response" or "empty" or "stale" ? value : "normal";
        if (next == scenario) return;
        scenario = next; failedRead = false; lostConfirmation = false;
        ReadCount = 0; ConfirmCount = 0;
        var now = DateTime.UtcNow;
        rows = next == "empty" ? [] :
        [
            Row(AlertId, "TransactionDispute", "support", "Open", "Urgent", now.AddHours(-2), now),
            Row(new('b', 64), "AfterPickupRecoveryRequired", "cargo", "Acknowledged", "Urgent", now.AddHours(-1), now),
            Row(new('c', 64), "PhotoUploadStalled", "monitor", "Open", "Warning", now.AddMinutes(-30), now),
            Row(new('d', 64), "DriverAssignmentDelay", "food", "Resolved", "Warning", now.AddHours(-3), now)
        ];
    }

    public Task<OperationsAlertsPage> ListAsync(string status, int page, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); MaybeFailRead();
        var selected = rows.Where(row => status == "Unresolved" ? row.Status != "Resolved"
                : status is "Open" or "Acknowledged" or "Resolved" ? row.Status == status : true)
            .OrderBy(row => row.Status == "Resolved").ThenBy(row => row.Severity != "Urgent")
            .ThenBy(row => row.CreatedAtUtc).ThenBy(row => row.Id).ToArray();
        page = Math.Max(1, page);
        return Task.FromResult(new OperationsAlertsPage(true, false,
            selected.Skip((page - 1) * 20).Take(20).Select(Dto).ToArray(), page, selected.Length > page * 20));
    }

    public Task<OperationsAlertDto?> DetailAsync(string id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); MaybeFailRead();
        return Task.FromResult(rows.SingleOrDefault(row => row.Id == id) is { } row ? Dto(row) : null);
    }

    public Task<OperationsAlertDto> AcknowledgeAsync(string id, OperationsAlertAcknowledgeRequest input, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); ConfirmCount++;
        var row = rows.Single(item => item.Id == id);
        if (input.ClientRequestId == Guid.Empty || input.ExpectedRevision != row.Revision
            || row.Status != "Open" || scenario == "stale") throw new InvalidOperationException("PreviewRevisionConflict");
        row.Status = "Acknowledged"; row.Revision++; row.AcknowledgedBy = "preview-operator";
        row.AcknowledgedAtUtc = DateTime.UtcNow;
        if (scenario == "lost-response" && !lostConfirmation)
        {
            lostConfirmation = true;
            throw new HttpRequestException("PreviewResponseLostAfterConfirmation");
        }
        return Task.FromResult(Dto(row));
    }

    private void MaybeFailRead()
    {
        ReadCount++;
        if (scenario == "read-failure" && !failedRead)
        {
            failedRead = true;
            throw new HttpRequestException("PreviewReadUnavailable");
        }
    }

    private OperationsAlertDto Dto(OperationsAlert row)
    {
        row.LastVerifiedAtUtc = DateTime.UtcNow.AddMinutes(scenario == "stale" ? -10 : 0);
        var now = DateTime.UtcNow;
        return new(row.Id, row.Episode, row.Revision, row.Severity, row.Status,
            OperationsAlertPresentation.Title(row.ProblemCode), OperationsAlertPresentation.Action(row.ProblemCode),
            OperationsAlertPresentation.Href(row.SourceKind, row.SourceId), row.CreatedAtUtc, row.LastVerifiedAtUtc,
            OperationsAlertHandlingPolicy.IsSourceCurrent(row, now), ["Push:Suppressed", "Email:Suppressed"])
        { Handling = OperationsAlertHandlingPolicy.Describe(row, now) };
    }

    private static OperationsAlert Row(string id, string code, string kind, string status,
        string severity, DateTime created, DateTime verified)
        => new() { Id = id, SourceId = "r21-preview-order", SourceKind = kind, ProblemCode = code,
            Status = status, Severity = severity, CreatedAtUtc = created, LastVerifiedAtUtc = verified,
            AcknowledgedBy = status == "Acknowledged" ? "preview-operator" : null };
}
