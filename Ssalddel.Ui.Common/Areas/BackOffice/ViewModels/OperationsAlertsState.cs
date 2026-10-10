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
    public event Action? Changed;
    public bool Busy { get; private set; }
    public string? Error { get; private set; }
    public OperationsAlertsPage? Page { get; private set; }
    public OperationsAlertDto? Detail { get; private set; }
    public bool CanAcknowledge => !Busy && Error is null && Detail is { Status: "Open", SourceCurrent: true } && owner is not null;
    public void SetOwner(string? userId)
    {
        if (owner == userId) return;
        owner = userId; generation++; lifetime.Cancel(); lifetime.Dispose(); lifetime = new();
        Busy = false; Error = null; Page = null; Detail = null; Changed?.Invoke();
    }
    public async Task LoadAsync(string? id, string status = "Open", int page = 1)
    {
        if (owner is null) return;
        var stamp = ++generation; var token = lifetime.Token; Busy = true; Error = null; Changed?.Invoke();
        try
        {
            if (id is null) { var data = await client.ListAsync(status, page, token); if (stamp == generation) Page = data; }
            else { var data = await client.DetailAsync(id, token); if (stamp == generation) { Detail = data; if (data is null) Error = "알림을 찾을 수 없습니다."; } }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch { if (stamp == generation) Error = "알림을 불러오지 못했습니다. 다시 시도해 주세요."; }
        finally { if (stamp == generation) { Busy = false; Changed?.Invoke(); } }
    }
    public async Task AcknowledgeAsync()
    {
        if (!CanAcknowledge) return;
        var row = Detail!; var stamp = ++generation; var token = lifetime.Token; Busy = true; Changed?.Invoke();
        try { var data = await client.AcknowledgeAsync(row.Id, new(Guid.NewGuid(), row.Revision), token); if (stamp == generation) Detail = data; }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch { if (stamp == generation) Error = "상태가 바뀌었거나 확인에 실패했습니다. 최신 알림을 다시 불러오세요."; }
        finally { if (stamp == generation) { Busy = false; Changed?.Invoke(); } }
    }
    public void Dispose() { generation++; lifetime.Cancel(); lifetime.Dispose(); }
}
