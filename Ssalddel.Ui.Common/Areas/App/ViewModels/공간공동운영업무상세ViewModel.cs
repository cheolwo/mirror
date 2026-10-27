using CommunityToolkit.Mvvm.ComponentModel;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Components.Community;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>독립 업무 상세의 조회·연락처 확인만 소유합니다. 기존 초안·원 요청·작업 상태를 변경하지 않습니다.</summary>
public sealed class 공간공동운영업무상세ViewModel : ObservableObject, IDisposable
{
    private readonly I공간공동운영Client cooperation;
    private readonly I공간공동운영통합Client integrated;
    private readonly I공간공동운영업무연락Client contacts;
    private readonly I업무전화앱Adapter dialer;
    private readonly ISsalddel현재사용자Context user;
    private readonly IWorkflowRecoveryContext recovery;
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? viewCancellation;
    private long generation;
    private bool disposed;
    private WorkflowRecoverySnapshot? viewScope;
    public 공간공동운영업무상세ViewModel(I공간공동운영Client cooperation, I공간공동운영통합Client integrated,
        I공간공동운영업무연락Client contacts, I업무전화앱Adapter dialer, ISsalddel현재사용자Context user, IWorkflowRecoveryContext recovery)
    {
        this.cooperation = cooperation; this.integrated = integrated; this.contacts = contacts; this.dialer = dialer; this.user = user; this.recovery = recovery;
        if (user is ISsalddel현재사용자변경알림 changes) changes.Changed += NotifyAuthenticationChanged;
    }
    private string? Owner => user.현재사용자.인증됨 ? user.현재사용자.UserId : null;
    public bool IsAuthenticated => Owner is not null;
    public string LedgerId { get; private set; } = "";
    public string StageKey { get; private set; } = "";
    public string CycleId { get; private set; } = "";
    public bool IsLoading { get; private set; }
    public bool IsLoadingContacts { get; private set; }
    public bool IsOpeningDialer { get; private set; }
    public string? Error { get; private set; }
    public string? ContactError { get; private set; }
    public string? Notice { get; private set; }
    public bool DialerSupported => dialer.IsSupported;
    public 공간공동운영상세Response? Detail { get; private set; }
    public 공간공동운영통합Response? Snapshot { get; private set; }
    public 공간공동운영업무연결Model Flow { get; private set; } = new();
    public 공간공동운영흐름현황 Progress { get; private set; } = new();
    public 공간공동운영업무노드? Node => Flow.Nodes.FirstOrDefault(node => node.Stage.Key == StageKey);
    public 공간공동운영업무연락Response? Contacts { get; private set; }
    public bool IsCurrentView => !disposed && viewScope is { } scope && scope.Owner == Owner && recovery.IsCurrent(scope);
    public bool CanReadContacts => IsCurrentView && Node is not null && !IsLoading && !IsLoadingContacts && !IsOpeningDialer;
    public string BackHref
    {
        get { try { return 공간공동운영업무Routes.Overview(LedgerId, string.IsNullOrWhiteSpace(CycleId) ? null : CycleId); } catch (ArgumentException) { return 공간공동운영Routes.Home; } }
    }
    public async Task LoadAsync(string? ledgerId, string? stageKey, string? cycleId)
    {
        if (disposed) return;
        var current = ++generation;
        CancelView(); viewCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        var ct = viewCancellation.Token;
        Clear(); LedgerId = ledgerId ?? ""; StageKey = stageKey ?? ""; CycleId = cycleId ?? "";
        try { _ = 공간공동운영업무Routes.Detail(LedgerId, StageKey, CycleId); }
        catch (ArgumentException) { Error = "선택한 업무와 회차를 확인해 주세요. 요약 화면에서 다시 선택할 수 있어요."; Changed(); return; }
        var scope = Capture();
        if (scope is null) { Error = "로그인한 계정에서 이 업무를 확인해 주세요."; Changed(); return; }
        viewScope = scope; IsLoading = true; Changed();
        try
        {
            var detail = await cooperation.ReadAsync(LedgerId, ct);
            if (!Current(current, scope)) return;
            if (detail is null) { Error = "현재 계정에서 이 공동운영을 확인할 수 없어요."; return; }
            var snapshot = await integrated.ReadAsync(LedgerId, ct);
            if (!Current(current, scope)) return;
            var progress = 공간공동운영흐름현황Presentation.Build(detail, snapshot, CycleId);
            var flow = progress.Flow;
            if (!flow.Available) { Error = flow.Message; return; }
            Detail = detail; Snapshot = snapshot; Flow = flow; Progress = progress;
        }
        catch (OperationCanceledException) when (disposed) { }
        catch (Exception exception) { if (Current(current, scope)) Error = ReadError(exception); }
        finally { if (Current(current, scope)) { IsLoading = false; Changed(); } }
    }
    public async Task ReadContactsAsync()
    {
        if (!CanReadContacts || viewScope is not { } scope || Snapshot is null) return;
        var current = generation; IsLoadingContacts = true; Contacts = null; ContactError = Notice = null; Changed();
        try
        {
            var result = await contacts.ReadAsync(LedgerId, CycleId, StageKey, Snapshot.Revision, viewCancellation!.Token);
            if (!Current(current, scope)) return;
            Contacts = Validate(result);
        }
        catch (OperationCanceledException) when (disposed) { }
        catch (Exception exception) { if (Current(current, scope)) { Contacts = null; ContactError = ReadError(exception); } }
        finally { if (Current(current, scope)) { IsLoadingContacts = false; Changed(); } }
    }
    public bool CanOpen(공간공동운영업무연락Dto contact) => CanReadContacts && DialerSupported && Contacts?.StatusCode == "available" && Contacts.Contacts.Contains(contact)
        && 업무전화번호Policy.TryNormalize(contact.PhoneNumber, out _);
    public async Task OpenDialerAsync(string contactKey)
    {
        if (!CanReadContacts || viewScope is not { } scope || Snapshot is null || !DialerSupported) return;
        var original = Contacts?.Contacts.SingleOrDefault(contact => contact.ContactKey == contactKey);
        if (original is null || !CanOpen(original) || !업무전화번호Policy.TryNormalize(original.PhoneNumber, out var number)) return;
        var current = generation; IsOpeningDialer = true; ContactError = Notice = null; Changed();
        var ct = viewCancellation!.Token;
        try
        {
            // 입력창을 열기 직전에도 현 권한·현재 업무와 연락처를 다시 조회합니다.
            var latest = await contacts.ReadAsync(LedgerId, CycleId, StageKey, Snapshot.Revision, ct);
            if (!Current(current, scope)) return;
            latest = Validate(latest);
            var confirmed = latest.Contacts.SingleOrDefault(contact => contact.ContactKey == contactKey);
            if (latest.StatusCode != "available" || confirmed is null || confirmed.SourceNodeKey != original.SourceNodeKey || confirmed.SourceRevision != original.SourceRevision
                || confirmed.PhoneNumber != original.PhoneNumber || confirmed.DisplayName != original.DisplayName || confirmed.Label != original.Label
                || confirmed.PurposeCode != original.PurposeCode || confirmed.Href != original.Href
                || !업무전화번호Policy.TryNormalize(confirmed.PhoneNumber, out var currentNumber) || currentNumber != number)
            { Contacts = latest; Notice = "연락처나 업무 정보가 바뀌었어요. 내용을 다시 확인한 뒤 선택해 주세요."; return; }
            if (!Current(current, scope)) return;
            var result = await dialer.OpenAsync(number, ct);
            if (!Current(current, scope)) return;
            Notice = result switch
            {
                업무전화앱결과.Opened => "전화 앱에 번호를 전달했어요. 통화는 전화 앱에서 직접 선택해 주세요.",
                업무전화앱결과.NotSupported => "이 기기에서는 전화 앱을 열 수 없어요.",
                업무전화앱결과.InvalidNumber => "등록된 번호 형식을 확인해 주세요.",
                _ => "전화 앱을 열지 못했어요. 기기의 전화 앱 설정을 확인해 주세요."
            };
        }
        catch (OperationCanceledException) when (disposed) { }
        catch (Exception exception) { if (Current(current, scope)) { Contacts = null; ContactError = ReadError(exception); } }
        finally { if (Current(current, scope)) { IsOpeningDialer = false; Changed(); } }
    }
    private 공간공동운영업무연락Response Validate(공간공동운영업무연락Response value)
    {
        if (value.LedgerId != LedgerId || value.CycleId != CycleId || value.StageKey != StageKey || value.Revision != Snapshot?.Revision
            || value.AsOfUtc == default || value.Contacts is null || value.Sources is null
            || value.StatusCode != "available" && value.Contacts.Count > 0
            || value.Contacts.Any(contact => contact is null || string.IsNullOrWhiteSpace(contact.ContactKey) || contact.SourceRevision < 1
                || Node?.Sources.Any(source => source.Verified && source.NodeKey == contact.SourceNodeKey) != true)
            || value.Contacts.GroupBy(contact => contact.ContactKey, StringComparer.Ordinal).Any(group => group.Count() != 1)) throw new InvalidDataException();
        return value;
    }
    private WorkflowRecoverySnapshot? Capture()
    { try { var scope = recovery.Capture(); return Owner is not null && scope.Owner == Owner ? scope : null; } catch (Exception) { return null; } }
    private bool Current(long current, WorkflowRecoverySnapshot scope)
    { if (disposed || generation != current) return false; if (scope.Owner == Owner && recovery.IsCurrent(scope)) return true; NotifyAuthenticationChanged(); return false; }
    public void NotifyAuthenticationChanged()
    { if (disposed) return; ++generation; CancelView(); Clear(); Error = IsAuthenticated ? "계정이나 서버가 바뀌었어요. 다시 불러와 주세요." : null; Changed(); }
    private void CancelView() { viewCancellation?.Cancel(); viewCancellation?.Dispose(); viewCancellation = null; }
    private void Clear()
    { Detail = null; Snapshot = null; Flow = new(); Progress = new(); Contacts = null; viewScope = null; IsLoading = IsLoadingContacts = IsOpeningDialer = false; Error = ContactError = Notice = null; }
    private static string ReadError(Exception exception) => exception is SsalddelApiException { StatusCode: 401 } ? "로그인이 만료됐어요. 로그인 후 다시 확인해 주세요."
        : exception is SsalddelApiException { StatusCode: 403 or 404 } ? "현재 계정에서 이 업무 정보를 확인할 수 없어요."
        : exception is SsalddelApiException { StatusCode: 409 } ? "업무 정보가 먼저 바뀌었어요. 상세 내용을 다시 불러와 주세요."
        : "업무 정보를 확인하지 못했어요. 연결을 확인하고 다시 시도해 주세요.";
    private void Changed() { if (!disposed) OnPropertyChanged(string.Empty); }
    public void Dispose()
    { if (disposed) return; disposed = true; ++generation; if (user is ISsalddel현재사용자변경알림 changes) changes.Changed -= NotifyAuthenticationChanged; CancelView(); lifetime.Cancel(); lifetime.Dispose(); Clear(); }
}
