using Ssalddel.Contracts.Admin.Operations;

namespace Ssalddel.Ui.Common.Areas.BackOffice.ViewModels;

public interface IOperationsAlertsClient
{
    Task<OperationsAlertsPage> ListAsync(string status, int page, CancellationToken ct);
    Task<OperationsAlertDto?> DetailAsync(string id, CancellationToken ct);
    Task<OperationsAlertDto> AcknowledgeAsync(string id, OperationsAlertAcknowledgeRequest input, CancellationToken ct);
}

/// <summary>목록·상세의 읽기와 알림 확인만 담당. 로그인 세대가 바뀌면 진행 중 응답을 폐기한다.</summary>
public sealed class OperationsAlertsState(IOperationsAlertsClient client) : IDisposable
{
    private long generation;
    private CancellationTokenSource lifetime = new();
    private string? owner;
    private string? readContext;
    private bool disposed;
    public event Action? Changed;
    public bool Busy { get; private set; }
    public string? Error { get; private set; }
    public OperationsAlertsPage? Page { get; private set; }
    public OperationsAlertDto? Detail { get; private set; }
    public bool AcknowledgementOutcomeUnknown { get; private set; }
    public string ErrorContextKey => $"operations-alerts:{readContext}:{generation}";
    public bool CanAcknowledge => !disposed && !Busy && Error is null && Detail is { Status: "Open", SourceCurrent: true } && owner is not null;
    public void SetOwner(string? userId)
    {
        if (disposed || owner == userId) return;
        owner = userId; generation++; lifetime.Cancel(); lifetime.Dispose(); lifetime = new();
        Busy = false; Error = null; Page = null; Detail = null; readContext = null;
        AcknowledgementOutcomeUnknown = false; Changed?.Invoke();
    }
    public async Task LoadAsync(string? id, string status = "Unresolved", int page = 1)
    {
        if (disposed || owner is null) return;
        status = OperationsAlertWorkflowPresentation.Status(status); page = Math.Clamp(page, 1, 10000);
        var context = id is null ? $"list:{status}:{page}" : $"detail:{id}";
        if (readContext != context)
        {
            Page = null; Detail = null; AcknowledgementOutcomeUnknown = false; readContext = context;
        }
        var stamp = ++generation; var token = lifetime.Token; Busy = true; Error = null; Changed?.Invoke();
        try
        {
            if (id is null) { var data = await client.ListAsync(status, page, token); if (stamp == generation) Page = data; }
            else
            {
                var data = await client.DetailAsync(id, token);
                if (stamp == generation)
                {
                    if (data is not null && data.Id != id) throw new InvalidDataException("AlertTargetMismatch");
                    Detail = data; AcknowledgementOutcomeUnknown = false;
                    if (data is null) Error = "알림을 찾을 수 없습니다.";
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch
        {
            if (stamp == generation)
            {
                if (Detail?.SourceCurrent != true) Detail = null;
                Page = null;
                Error = AcknowledgementOutcomeUnknown ? "알림 확인 결과를 다시 조회하세요." : "알림을 불러오지 못했습니다.";
            }
        }
        finally { if (stamp == generation) { Busy = false; Changed?.Invoke(); } }
    }
    public async Task AcknowledgeAsync()
    {
        if (!CanAcknowledge) return;
        var row = Detail!; var stamp = ++generation; var token = lifetime.Token; Busy = true; Changed?.Invoke();
        try
        {
            var data = await client.AcknowledgeAsync(row.Id, new(Guid.NewGuid(), row.Revision), token);
            if (stamp == generation)
            {
                if (data.Id != row.Id) throw new InvalidDataException("AlertTargetMismatch");
                Detail = data; AcknowledgementOutcomeUnknown = false;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch
        {
            if (stamp == generation)
            {
                AcknowledgementOutcomeUnknown = true;
                Error = "알림 확인 결과를 다시 조회하세요.";
            }
        }
        finally { if (stamp == generation) { Busy = false; Changed?.Invoke(); } }
    }
    public void Dispose() { if (disposed) return; disposed = true; generation++; lifetime.Cancel(); lifetime.Dispose(); }
}
