using CommunityToolkit.Mvvm.ComponentModel;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

public sealed class NeighborhoodDeliveryQueryViewModel(
    INeighborhoodDeliveryClient client, ISsalddel현재사용자Context user) : ObservableObject, IDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private string? _owner;
    private long _generation;
    private bool _disposed;
    private Guid _disclosureRequestId = Guid.NewGuid();
    private string? _disclosureInput;
    private NeighborhoodDispatchChoiceRequest? _pendingChoice;
    public string SelectedDispatchMode { get; set; } = NeighborhoodDispatchModes.Automatic;
    public bool HasPendingChoice => _pendingChoice is not null;
    public bool IsLoading { get; private set; }
    public bool IsSending { get; private set; }
    public bool DisclosureAgreement { get; set; }
    public bool RequiresLogin { get; private set; }
    public bool IsAuthenticated => user.현재사용자.인증됨 && !RequiresLogin;
    public string? Error { get; private set; }
    public bool Loaded { get; private set; }
    public int Page { get; private set; } = 1;
    public bool HasNext => Items.Count == 20;
    public IReadOnlyList<NeighborhoodDeliveryResponse> Items { get; private set; } = [];
    public NeighborhoodDeliveryResponse? Detail { get; private set; }

    public void SynchronizeOwner()
    {
        if (_disposed) return;
        var owner = CurrentOwner;
        if (string.Equals(_owner, owner, StringComparison.Ordinal)) return;
        ++_generation; _pendingChoice = null; _owner = owner; Items = []; Detail = null; Loaded = false;
        Error = null; IsLoading = false; IsSending = false; RequiresLogin = false; ResetDisclosure(); Changed();
    }
    public Task LoadMineAsync() => LoadAsync(null, Page);
    public Task LoadMinePageAsync(int page) => LoadAsync(null, Math.Clamp(page, 1, 10000));
    public Task LoadDetailAsync(string requestId) => LoadAsync(requestId);
    private async Task LoadAsync(string? requestId, int page = 1)
    {
        SynchronizeOwner();
        if (_disposed || IsSending) return;
        if (!IsAuthenticated) { RequiresLogin = true; Changed(); return; }
        var generation = ++_generation; var owner = _owner;
        Items = []; Detail = null; Loaded = false; Error = null; IsLoading = true; Page = page; Changed();
        try
        {
            if (requestId is null)
            {
                var items = await client.MineAsync(page, _lifetime.Token);
                if (!IsCurrent(generation, owner)) return;
                Items = items;
            }
            else
            {
                var detail = await client.ReadAsync(requestId, _lifetime.Token);
                if (!IsCurrent(generation, owner)) return;
                Detail = detail;
                if (detail?.DispatchMode is { } mode && _pendingChoice is null) SelectedDispatchMode = mode;
                DisclosureAgreement = false;
                if (detail is null) Error = "내 배송 의뢰를 찾을 수 없습니다.";
                else ResetDisclosure();
            }
            Loaded = true;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!IsCurrent(generation, owner)) return;
            Error = NeighborhoodDeliveryPresentation.Error(ex);
            if (ex is SsalddelApiException { StatusCode: 401 }) RequiresLogin = true;
        }
        finally { if (IsCurrent(generation, owner)) { IsLoading = false; Changed(); } }
    }
    public async Task ChangeDispatchAsync()
    {
        SynchronizeOwner();
        if (_disposed || IsSending || IsLoading || !IsAuthenticated || Detail is not { } detail || !detail.CanChangeDispatchMode && _pendingChoice is null) return;
        if (_pendingChoice is not null && _pendingChoice.DispatchMode != SelectedDispatchMode)
        { Error = "원 배차 선택의 처리 결과를 먼저 확인해 주세요."; Changed(); return; }
        _pendingChoice ??= new() { ClientRequestId = Guid.NewGuid(), ExpectedDispatchRevision = detail.DispatchRevision, DispatchMode = SelectedDispatchMode };
        var owner = _owner; var generation = ++_generation; IsSending = true; Error = null; Changed();
        try
        {
            var result = await client.ChangeDispatchAsync(detail.RequestId, _pendingChoice, _lifetime.Token);
            if (!IsCurrent(generation, owner)) return;
            if (result is null || result.RequestId != detail.RequestId) { Error = "배차 선택 결과를 확인하지 못했습니다. 같은 요청으로 확인해 주세요."; return; }
            Detail = result; SelectedDispatchMode = result.DispatchMode ?? SelectedDispatchMode; _pendingChoice = null;
        }
        catch (Exception ex)
        {
            if (IsCurrent(generation, owner))
            {
                Error = NeighborhoodDeliveryPresentation.Error(ex, writing: true);
                if (ex is SsalddelApiException { StatusCode: 400 or 401 or 403 or 404 or 409 or 422 }) _pendingChoice = null;
            }
        }
        finally { if (IsCurrent(generation, owner)) { IsSending = false; Changed(); } }
    }
    public async Task RecordDisclosureAsync(bool consented)
    {
        SynchronizeOwner();
        if (_disposed || IsSending || IsLoading || !IsAuthenticated || Detail?.DriverDisclosure is not { } disclosure) return;
        if (string.IsNullOrWhiteSpace(disclosure.ConfirmedDriverId)
            || (consented && (!DisclosureAgreement || !disclosure.CanRecordConsent || string.IsNullOrWhiteSpace(disclosure.ConfirmedDriverName)
                || disclosure.Fields.Count == 0 || disclosure.Fields.Any(field => NeighborhoodDeliveryPresentation.DisclosureField(field) is null))))
        { Error = "선정 기사와 제공 항목을 확인하고 동의를 선택해 주세요."; Changed(); return; }
        var requestId = Detail.RequestId;
        var input = $"{requestId}|{disclosure.ConfirmedDriverId}|{disclosure.RecommendationRound}|{disclosure.NoticeVersion}|{consented}";
        if (_disclosureInput is not null && _disclosureInput != input)
        { Error = "이전 정보 제공 동의의 결과를 먼저 새로고침으로 확인해 주세요."; Changed(); return; }
        _disclosureInput = input;
        var request = new NeighborhoodDeliveryDisclosureRequest
        {
            ClientRequestId = _disclosureRequestId, ConfirmedDriverId = disclosure.ConfirmedDriverId,
            ExpectedRecommendationRound = disclosure.RecommendationRound, Consented = consented, NoticeVersion = disclosure.NoticeVersion
        };
        var owner = _owner; var generation = ++_generation; IsSending = true; Error = null; Changed();
        try
        {
            var result = await client.RecordDisclosureAsync(requestId, request, _lifetime.Token);
            if (!IsCurrent(generation, owner)) return;
            if (result is null || result.RequestId != requestId)
            { Error = "정보 제공 동의 결과를 확인하지 못했습니다. 새로고침으로 현재 상태를 확인해 주세요."; return; }
            Detail.DriverDisclosure = result; ResetDisclosure();
            var refreshed = await client.ReadAsync(requestId, _lifetime.Token);
            if (!IsCurrent(generation, owner)) return;
            if (refreshed is null) { Error = "정보 제공 동의는 저장됐지만 배송 진행을 다시 확인하지 못했습니다. 새로고침해 주세요."; return; }
            Detail = refreshed;
        }
        catch (Exception ex)
        {
            if (!IsCurrent(generation, owner)) return;
            Error = NeighborhoodDeliveryPresentation.Error(ex, writing: true);
            if (ex is SsalddelApiException { StatusCode: 400 or 409 or 422 }) ResetDisclosure();
            if (ex is SsalddelApiException { StatusCode: 401 }) { RequiresLogin = true; Items = []; Detail = null; ResetDisclosure(); }
        }
        finally { if (IsCurrent(generation, owner)) { IsSending = false; Changed(); } }
    }
    private void ResetDisclosure() { _disclosureRequestId = Guid.NewGuid(); _disclosureInput = null; DisclosureAgreement = false; }
    private bool IsCurrent(long generation, string? owner)
    {
        if (_disposed) return false;
        if (!string.Equals(owner, CurrentOwner, StringComparison.Ordinal))
        { SynchronizeOwner(); return false; }
        return generation == _generation;
    }
    private string? CurrentOwner => user.현재사용자.인증됨 ? user.현재사용자.UserId : null;
    private void Changed() => OnPropertyChanged(string.Empty);
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; ++_generation; _lifetime.Cancel(); _lifetime.Dispose(); Items = []; Detail = null; ResetDisclosure();
    }
}
