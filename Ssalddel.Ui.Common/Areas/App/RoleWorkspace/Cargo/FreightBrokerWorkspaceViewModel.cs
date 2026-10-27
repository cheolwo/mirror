using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;

/// <summary>본인 협업 조회의 계정·서버 수명만 소유합니다. 업체 조직이나 운송 계약을 생성하지 않습니다.</summary>
public sealed class FreightBrokerWorkspaceViewModel : IDisposable
{
    private readonly IFreightBrokerWorkspaceClient client;
    private readonly ISsalddel현재사용자Context user;
    private readonly IWorkflowRecoveryContext context;
    private WorkflowRecoverySnapshot? snapshot;
    private IReadOnlyList<FreightBrokerLedgerItem> items = [];
    private FreightBrokerLedgerDetail? detail;
    private CancellationTokenSource? request;
    private long generation;
    private bool disposed;

    public FreightBrokerWorkspaceViewModel(IFreightBrokerWorkspaceClient client,
        ISsalddel현재사용자Context user, IWorkflowRecoveryContext context)
    {
        this.client = client; this.user = user; this.context = context;
        if (user is ISsalddel현재사용자변경알림 signal) signal.Changed += IdentityChanged;
    }

    public event Action? Changed;
    public bool IsAuthenticated => !disposed && user.현재사용자.인증됨;
    public bool IsBusy { get; private set; }
    public bool HasLoaded { get; private set; }
    public bool RequiresLogin { get; private set; }
    public bool AccessDenied { get; private set; }
    public string? Error { get; private set; }
    public string? DetailMessage { get; private set; }
    public IReadOnlyList<FreightBrokerLedgerItem> Items => Current ? items : [];
    public FreightBrokerLedgerDetail? Detail => Current ? detail : null;
    public string? SelectedId { get; private set; }
    private bool Current => !disposed && IsAuthenticated && snapshot is not null
        && string.Equals(snapshot.Owner, user.현재사용자.UserId, StringComparison.Ordinal) && context.IsCurrent(snapshot);

    public async Task LoadAsync()
    {
        if (disposed || IsBusy) return;
        ClearState();
        RequiresLogin = !IsAuthenticated;
        if (RequiresLogin) { Changed?.Invoke(); return; }
        var ticket = ++generation;
        request = new(); IsBusy = true; Changed?.Invoke();
        try
        {
            var captured = Capture(); snapshot = captured;
            var result = await client.MineAsync(request.Token);
            if (!Valid(ticket, captured)) return;
            items = result; HasLoaded = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { if (ticket == generation && !disposed) Fail(error); }
        finally { if (ticket == generation && !disposed) { IsBusy = false; Changed?.Invoke(); } }
    }

    public async Task SelectAsync(string ledgerId)
    {
        if (disposed || IsBusy || !Current || !HasLoaded) return;
        if (!items.Any(row => string.Equals(row.Id, ledgerId, StringComparison.Ordinal)))
        { detail = null; SelectedId = null; DetailMessage = "현재 본인 목록에서 원장을 다시 선택해 주세요."; Changed?.Invoke(); return; }
        var captured = snapshot!; var ticket = ++generation;
        request?.Dispose(); request = new(); IsBusy = true; detail = null; SelectedId = ledgerId;
        Error = null; AccessDenied = false; DetailMessage = null; Changed?.Invoke();
        try
        {
            var result = await client.DetailAsync(ledgerId, request.Token);
            if (!Valid(ticket, captured)) return;
            if (result is null) { DetailMessage = "원장을 찾을 수 없거나 접근 권한이 변경됐습니다. 목록을 다시 확인해 주세요."; return; }
            if (!string.Equals(result.Id, ledgerId, StringComparison.Ordinal)) throw new InvalidDataException();
            detail = result;
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { if (ticket == generation && !disposed) Fail(error); }
        finally { if (ticket == generation && !disposed) { IsBusy = false; Changed?.Invoke(); } }
    }

    private WorkflowRecoverySnapshot Capture()
    {
        var captured = context.Capture();
        if (!IsAuthenticated || !string.Equals(captured.Owner, user.현재사용자.UserId, StringComparison.Ordinal))
            throw new InvalidOperationException("로그인 계정을 다시 확인해 주세요.");
        return captured;
    }

    private bool Valid(long ticket, WorkflowRecoverySnapshot captured)
        => ticket == generation && !disposed && IsAuthenticated && snapshot == captured && Current;

    private void Fail(Exception error)
    {
        items = []; detail = null; SelectedId = null; HasLoaded = false;
        RequiresLogin = !IsAuthenticated || error is SsalddelApiException { StatusCode: 401 };
        AccessDenied = error is SsalddelApiException { StatusCode: 403 };
        Error = RequiresLogin ? "다시 로그인한 뒤 본인 업무를 확인해 주세요."
            : AccessDenied ? "이 원장을 조회할 권한이 없습니다. 계정과 참여 범위를 확인해 주세요."
            : error is InvalidDataException ? "화물 원장의 조회 범위를 확인하지 못했습니다. 목록을 다시 조회해 주세요."
            : "내 화물 협업 원장을 불러오지 못했습니다. 연결을 확인하고 다시 조회해 주세요.";
    }

    private void ClearState()
    {
        generation++; request?.Cancel(); request?.Dispose(); request = null; snapshot = null;
        items = []; detail = null; SelectedId = null; IsBusy = false; HasLoaded = false;
        RequiresLogin = false; AccessDenied = false; Error = null; DetailMessage = null;
    }

    private void IdentityChanged()
    { if (disposed) return; ClearState(); RequiresLogin = !IsAuthenticated; Changed?.Invoke(); }

    public void Dispose()
    {
        if (disposed) return;
        if (user is ISsalddel현재사용자변경알림 signal) signal.Changed -= IdentityChanged;
        ClearState(); disposed = true; Changed = null;
    }
}
