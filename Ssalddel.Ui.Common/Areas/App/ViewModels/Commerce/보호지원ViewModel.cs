using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.Services.Commerce;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels.Commerce;

public sealed class 보호지원ViewModel(I통신판매보호Client client, ISsalddel현재사용자Context user) : NeighborhoodWorkflowViewModel(user)
{
    public bool Privacy { get; private set; }
    public string SourceKind { get; private set; } = "";
    public string SourceId { get; private set; } = "";
    public string RequestKind { get; set; } = "access";
    public string Summary { get; set; } = "";
    public string AdditionalSummary { get; set; } = "";
    public bool HasPending => _pendingDispute is not null || _pendingRights is not null || _pendingCommand is not null;
    public IReadOnlyList<보호지원CaseResponse> Items { get; private set; } = [];
    public 보호지원CaseResponse? Detail { get; private set; }
    public bool Loaded { get; private set; }
    public int Page { get; private set; } = 1;
    public bool HasMore { get; private set; }
    private 거래분쟁접수Request? _pendingDispute;
    private 개인정보권리접수Request? _pendingRights;
    private 보호지원CommandRequest? _pendingCommand;
    private string? _pendingCaseId;
    private string _caseId = "";
    private (string CaseId, int Page)? _loadTarget;
    public void Initialize(bool privacy, string? sourceKind, string? sourceId, string? caseId = null)
    {
        SynchronizeOwner();
        if (Privacy == privacy && SourceKind == (sourceKind ?? "") && SourceId == (sourceId ?? "") && _caseId == (caseId ?? "")) return;
        Begin(); IsLoading = false; IsSending = false; Error = null; Notice = null;
        Privacy = privacy; SourceKind = sourceKind ?? ""; SourceId = sourceId ?? ""; _caseId = caseId ?? ""; ResetForOwner(); Changed();
    }
    public async Task LoadAsync(string? caseId = null, int page = 1)
    {
        if (!CheckOwner()) return;
        if (_caseId != (caseId ?? ""))
        {
            // 이동은 이전 전송의 완료를 기다리지 않습니다. 같은 사건의 재시도만 원 요청을 유지합니다.
            Begin(); _caseId = caseId ?? ""; IsLoading = false; IsSending = false; Error = null; Notice = null;
            ResetForOwner(); Changed();
        }
        if (IsSending) return;
        var target = (caseId ?? "", page);
        if (_loadTarget != target) { Begin(); IsLoading = false; Detail = null; AdditionalSummary = ""; }
        else if (IsLoading) return;
        _loadTarget = target;
        var privacy = Privacy;
        var op = Begin(); IsLoading = true; Error = null; Changed();
        try
        {
            var list = await client.목록Async(privacy, page, Token);
            if (!Current(op)) return;
            var detail = string.IsNullOrWhiteSpace(caseId) ? null : await client.상세Async(privacy, caseId, Token);
            if (!Current(op)) return; Items = list.Items; Page = list.Page; HasMore = list.HasMore; Detail = detail; Loaded = true;
        }
        catch (Exception ex) { if (Current(op)) ApplySupportError(ex); }
        finally { if (Current(op)) { IsLoading = false; Changed(); } }
    }
    public async Task SubmitAsync()
    {
        if (!CheckOwner() || IsSending || _pendingCommand is not null || string.IsNullOrWhiteSpace(Summary)) return;
        if (Privacy) _pendingRights ??= new() { ClientRequestId = Guid.NewGuid(), RequestKind = RequestKind, Summary = Summary };
        else _pendingDispute ??= new() { ClientRequestId = Guid.NewGuid(), SourceKind = SourceKind, SourceId = SourceId, Summary = Summary };
        await SendAsync();
    }
    public async Task CommandAsync(string action)
    {
        if (!CheckOwner() || IsLoading || IsSending || _pendingDispute is not null || _pendingRights is not null || Detail is null || Detail.RetentionPending || !Detail.AllowedActions.Contains(action) || action is not ("add-evidence" or "appeal") || string.IsNullOrWhiteSpace(AdditionalSummary)) return;
        _pendingCommand ??= new()
        {
            ClientRequestId = Guid.NewGuid(), ExpectedRevision = Detail.Revision, Action = action,
            Summary = action == "appeal" ? "이의제기 접수" : "추가 내용 제출", PrivateEvidence = AdditionalSummary
        };
        _pendingCaseId ??= Detail.CaseId;
        await SendAsync();
    }
    public Task RetryAsync() => SendAsync();
    private async Task SendAsync()
    {
        if (!CheckOwner() || IsSending || !HasPending) return;
        var op = Begin(); IsSending = true; Error = null; Notice = null; Changed();
        try
        {
            var result = _pendingRights is not null ? await client.권리Async(_pendingRights, Token)
                : _pendingDispute is not null ? await client.분쟁Async(_pendingDispute, Token)
                : await client.변경Async(Privacy, _pendingCaseId!, _pendingCommand!, Token);
            if (!Current(op)) return;
            Detail = result ?? throw new InvalidOperationException();
            _pendingRights = null; _pendingDispute = null; _pendingCommand = null; _pendingCaseId = null;
            Summary = ""; AdditionalSummary = ""; Notice = "요청을 접수했습니다. 처리 진행을 확인할 수 있습니다.";
        }
        catch (Exception ex) { if (Current(op)) { ApplySupportError(ex, true); if (DefinitiveRejection(ex)) { _pendingRights = null; _pendingDispute = null; _pendingCommand = null; _pendingCaseId = null; } } }
        finally { if (Current(op)) { IsSending = false; Changed(); } }
    }
    protected override void ResetForOwner() { Items = []; Detail = null; Loaded = false; Page = 1; HasMore = false; Summary = ""; AdditionalSummary = ""; _pendingDispute = null; _pendingRights = null; _pendingCommand = null; _pendingCaseId = null; _loadTarget = null; }
    private void ApplySupportError(Exception exception, bool writing = false)
    {
        if (exception is SsalddelApiException { StatusCode: 401 or 403 })
        {
            // Losing authentication or case access ends this input's lifetime, including uncertain retries.
            // Advance the generation so an older read cannot restore the discarded private state.
            Begin(); ResetForOwner(); IsLoading = false; IsSending = false; Notice = null;
            ApplyError(exception, writing); Changed();
            return;
        }
        ApplyError(exception, writing);
    }
    public override void Dispose()
    {
        if (Disposed) return;
        base.Dispose(); ResetForOwner(); IsLoading = false; IsSending = false; Error = null; Notice = null;
    }
}
