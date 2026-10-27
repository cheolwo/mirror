using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

public sealed class 공간공동운영주문Session
{
    private string? owner;
    public string? LedgerId { get; internal set; }
    public Guid OrderId { get; internal set; }
    public 공간공동운영주문명령Request? Pending { get; internal set; }
    public WorkflowRecoverySnapshot? Scope { get; internal set; }
    public void Bind(string? current) { if (current is null || current == owner) return; owner = current; Clear(); }
    internal void Clear() { Pending = null; Scope = null; LedgerId = null; OrderId = default; }
}

/// <summary>주문자·공급자의 명시 확인 후 기존 주문·재고·배송으로 인계합니다. 응답 유실은 원 요청부터 조회합니다.</summary>
public sealed class 공간공동운영주문ViewModel : ObservableObject, IDisposable
{
    private readonly I공간공동운영주문Client client;
    private readonly ISsalddel현재사용자Context user;
    private readonly IWorkflowRecoveryContext recovery;
    private readonly 공간공동운영주문Session session;
    private readonly CancellationTokenSource lifetime = new();
    private long generation;
    private bool disposed;
    private string? owner;
    private long candidateLedgerRevision;
    private long draftLedgerRevision;
    public 공간공동운영주문ViewModel(I공간공동운영주문Client client, ISsalddel현재사용자Context user,
        IWorkflowRecoveryContext recovery, 공간공동운영주문Session session)
    {
        this.client = client; this.user = user; this.recovery = recovery; this.session = session;
        owner = CurrentOwner; session.Bind(owner);
        if (user is ISsalddel현재사용자변경알림 changes) changes.Changed += AuthenticationChanged;
    }
    private string? CurrentOwner => user.현재사용자.인증됨 ? user.현재사용자.UserId : null;
    public string LedgerId { get; private set; } = "";
    public string CycleId { get; private set; } = "";
    public bool IsLoading { get; private set; }
    public bool IsSending { get; private set; }
    public bool Loaded { get; private set; }
    public bool HasPending => session.Pending is not null;
    public bool CanRetry { get; private set; }
    public string? Error { get; private set; }
    public string? Notice { get; private set; }
    public string? CandidatesError { get; private set; }
    public IReadOnlyList<공간공동운영주문Response> Items { get; private set; } = [];
    public IReadOnlyList<공간공동운영주문후보Dto> Candidates { get; private set; } = [];
    public int CandidateIndex { get; private set; } = -1;
    public 공간공동운영주문조건Dto? Draft { get; private set; }
    public DateTime DeadlineLocal { get; set; }
    public bool ConfirmTerms { get; set; }
    public Guid? ConfirmedOrderId { get; private set; }
    public void ConfirmOrder(Guid id, bool confirmed) { ConfirmedOrderId = confirmed ? id : null; Changed(); }
    public string CandidateValue { get => CandidateIndex.ToString(); set { if (int.TryParse(value, out var index)) SelectCandidate(index); } }
    public void SelectCandidate(int index)
    {
        if (IsSending || HasPending || index < 0 || index >= Candidates.Count) return;
        CandidateIndex = index; Draft = Clone(Candidates[index].Terms);
        draftLedgerRevision = candidateLedgerRevision;
        DeadlineLocal = DateTime.SpecifyKind(Draft.DeadlineUtc, DateTimeKind.Utc).ToLocalTime(); ConfirmTerms = false; Changed();
    }
    private void AuthenticationChanged()
    { if (disposed) return; ++generation; owner = CurrentOwner; session.Bind(owner); Items = []; Candidates = []; Draft = null; candidateLedgerRevision = 0; draftLedgerRevision = 0; CandidateIndex = -1; IsSending = false; IsLoading = false; Loaded = false; ConfirmTerms = false; ConfirmedOrderId = null; Notice = null; Error = null; Changed(); }
    private bool Current(long value) { if (disposed) return false; if (owner != CurrentOwner) AuthenticationChanged(); return value == generation; }
    public async Task LoadAsync(string ledgerId, string cycleId)
    {
        if (disposed || IsSending) return;
        if (owner != CurrentOwner) AuthenticationChanged();
        LedgerId = ledgerId; CycleId = cycleId; var current = ++generation;
        IsLoading = true; Loaded = false; Error = null; CandidatesError = null; Items = []; Candidates = [];
        Draft = null; candidateLedgerRevision = 0; draftLedgerRevision = 0; CandidateIndex = -1; ConfirmTerms = false; ConfirmedOrderId = null; Changed();
        try
        {
            if (CurrentOwner is null) { Error = "로그인한 계정의 주문만 확인할 수 있습니다."; return; }
            var values = await client.ListAsync(ledgerId, lifetime.Token);
            if (!Current(current)) return;
            Items = values.Items; Loaded = true;
            if (!string.IsNullOrWhiteSpace(cycleId))
            {
                try
                {
                    var options = await client.CandidatesAsync(ledgerId, cycleId, lifetime.Token);
                    if (!Current(current)) return;
                    if (options.LedgerId != ledgerId || options.Revision < 1 || options.Items is null) throw new InvalidDataException();
                    Candidates = options.Items; candidateLedgerRevision = options.Revision;
                }
                catch (Exception) { if (Current(current)) CandidatesError = "확인할 수 있는 내 수요를 불러오지 못했습니다."; }
            }
        }
        catch (Exception) { if (Current(current)) Error = "주문 목록을 확인하지 못했습니다. 다시 불러와 주세요."; }
        finally { if (Current(current)) { IsLoading = false; Changed(); } }
    }
    public async Task ProposeAsync()
    {
        if (Draft is null || CandidateIndex < 0 || CandidateIndex >= Candidates.Count || !CanWrite()) return;
        if (!ConfirmTerms || draftLedgerRevision < 1 || Draft.Quantity < 1 || Draft.UnitPriceKrw < 0 || DeadlineLocal <= DateTime.Now)
        { Error = "상품·수량·가격·수령 기한과 확인 항목을 살펴봐 주세요."; Changed(); return; }
        var terms = Clone(Draft); terms.DeadlineUtc = DateTime.SpecifyKind(DeadlineLocal, DateTimeKind.Local).ToUniversalTime();
        var scope = Capture(); if (scope is null) return;
        session.Pending = new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = draftLedgerRevision,
            ExpectedTermsVersion = 0, Action = "propose", ConfirmTerms = true, Terms = terms };
        session.LedgerId = LedgerId; session.OrderId = terms.OrderRequestId; session.Scope = scope;
        await SendAsync(scope);
    }
    public async Task ExecuteAsync(공간공동운영주문Response order, string action)
    {
        if (!CanWrite()) return;
        if (ConfirmedOrderId != order.OrderRequestId || !order.AllowedActions.Contains(action)) { Error = "해당 주문의 현재 조건을 확인한 뒤 가능한 행동을 선택해 주세요."; Changed(); return; }
        var scope = Capture(); if (scope is null) return;
        session.Pending = new() { ClientRequestId = Guid.NewGuid(), Action = action, ConfirmTerms = true, ExpectedRevision = order.Revision, ExpectedTermsVersion = order.TermsVersion };
        session.LedgerId = order.LedgerId; session.OrderId = order.OrderRequestId; session.Scope = scope;
        await SendAsync(scope);
    }
    private bool CanWrite()
    {
        if (disposed || IsSending || IsLoading) return false;
        if (CurrentOwner is null) { Error = "로그인 후 돌아와 직접 확인해 주세요."; Changed(); return false; }
        if (HasPending) { Error = "이전 주문 요청의 결과를 먼저 확인해 주세요."; Changed(); return false; }
        return true;
    }
    public async Task RecoverAsync()
    {
        if (disposed || IsSending || session.Pending is not { } pending || session.LedgerId is not { } ledger) return;
        var scope = Capture(); if (scope is null || !SameScope(scope)) return;
        var current = ++generation; IsSending = true; Error = null; CanRetry = false; Changed();
        try
        {
            var result = await client.ReceiptAsync(ledger, pending.ClientRequestId, lifetime.Token);
            if (!Current(current) || !recovery.IsCurrent(scope)) return;
            if (result is null) { Notice = "원 요청의 처리 기록이 아직 없습니다. 결과를 다시 조회하거나 원래 요청을 직접 재전송할 수 있습니다."; CanRetry = true; return; }
            await CanonicalAsync(result, scope, current);
        }
        catch (Exception) { if (Current(current)) Error = "원 주문 요청의 결과를 확인하지 못했습니다."; }
        finally { if (Current(current)) { IsSending = false; Changed(); } }
    }
    public Task RetryAsync()
    { var scope = Capture(); return !CanRetry || scope is null || !SameScope(scope) ? Task.CompletedTask : SendAsync(scope); }
    private async Task SendAsync(WorkflowRecoverySnapshot scope)
    {
        if (session.Pending is not { } pending || session.LedgerId is not { } ledger) return;
        var current = ++generation; IsSending = true; Error = null; Notice = null; CanRetry = false; Changed();
        try
        {
            var response = await client.CommandAsync(ledger, session.OrderId, Clone(pending), lifetime.Token);
            if (!Current(current) || !recovery.IsCurrent(scope)) return;
            if (response is null) { Error = "처리 결과를 확인하지 못했습니다. 원 요청의 결과를 먼저 조회해 주세요."; return; }
            await CanonicalAsync(response, scope, current);
        }
        catch (Exception exception)
        {
            if (!Current(current)) return;
            if (exception is SsalddelApiException { StatusCode: 400 or 403 or 409 or 422 }) session.Clear();
            if (exception is SsalddelApiException { StatusCode: 409 })
            { Items = []; Candidates = []; Draft = null; CandidateIndex = -1; draftLedgerRevision = 0; candidateLedgerRevision = 0; ConfirmTerms = false; ConfirmedOrderId = null; Loaded = false; }
            Error = exception is SsalddelApiException { StatusCode: 409 } ? "주문 조건이 바뀌었습니다. 다시 불러와 확인해 주세요."
                : "처리 결과를 확인하지 못했습니다. 원 요청의 결과를 먼저 조회해 주세요.";
        }
        finally { if (Current(current)) { IsSending = false; Changed(); } }
    }
    private async Task CanonicalAsync(공간공동운영주문Response result, WorkflowRecoverySnapshot scope, long current)
    {
        if (result.LedgerId != session.LedgerId || result.OrderRequestId != session.OrderId)
        { Error = "원 요청과 다른 주문 응답입니다. 결과를 다시 확인해 주세요."; return; }
        var canonical = await client.ReadAsync(result.LedgerId, result.OrderRequestId, lifetime.Token);
        if (!Current(current) || !recovery.IsCurrent(scope)) return;
        if (canonical is null || canonical.OrderRequestId != result.OrderRequestId || canonical.LedgerId != result.LedgerId || canonical.Revision < result.Revision)
        { Error = "최신 주문 조건을 다시 확인하지 못했습니다. 원 요청을 보존했습니다."; return; }
        if (canonical.LedgerId == LedgerId) Items = Items.Where(value => value.OrderRequestId != canonical.OrderRequestId).Append(canonical).ToArray();
        session.Clear(); ConfirmTerms = false; ConfirmedOrderId = null; CanRetry = false; Notice = "주문 조건과 현재 진행을 다시 확인했습니다.";
    }
    private WorkflowRecoverySnapshot? Capture()
    { try { var value = recovery.Capture(); if (value.Owner != CurrentOwner) throw new InvalidOperationException(); return value; } catch (Exception) { Error = "원래 계정으로 로그인한 뒤 확인해 주세요."; Changed(); return null; } }
    private bool SameScope(WorkflowRecoverySnapshot scope)
    { if (session.Scope is { } saved && saved.Owner == scope.Owner && saved.EndpointKey == scope.EndpointKey) return true; Error = "원래 요청을 보낸 계정과 서버에서 확인해 주세요."; Changed(); return false; }
    private void Changed() { if (!disposed) OnPropertyChanged(string.Empty); }
    private static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    public void Dispose()
    { if (disposed) return; disposed = true; ++generation; if (user is ISsalddel현재사용자변경알림 changes) changes.Changed -= AuthenticationChanged; lifetime.Cancel(); lifetime.Dispose(); }
}
