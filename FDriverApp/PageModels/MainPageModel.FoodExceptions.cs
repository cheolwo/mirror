using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FDriverApp.Services;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Food;

namespace FDriverApp.PageModels;

public sealed partial class MainPageModel
{
    public FDriverDeliveryExceptionState ExceptionEditor { get; } = new();
    private 음식배달가게도착요청? _pendingArrival;
    private string? _pendingArrivalAttemptId;
    private 운영배차수신의사변경요청? _pendingDispatchIntent;
    [ObservableProperty] private bool _dispatchIntentKnown;
    [ObservableProperty] private bool _receivesNewDispatches;
    [ObservableProperty] private string _dispatchIntentNotice = "신규 배차 수신 상태 확인 전";

    public bool IsRegularWorkspaceVisible => IsAuthenticated && !ExceptionEditor.IsOpen;
    public bool IsExceptionWorkspaceVisible => IsAuthenticated && ExceptionEditor.IsOpen;
    public bool CanToggleWork => IsAuthenticated && !IsBusy && !(IsOnDuty && HasActiveWork);
    public bool CanToggleDispatchIntent => IsAuthenticated && !IsBusy && !ExceptionEditor.IsOpen
        && (DispatchIntentKnown || _pendingDispatchIntent is not null);
    public string DispatchIntentButtonText => _pendingDispatchIntent is not null ? "수신 변경 다시 확인"
        : !DispatchIntentKnown ? "신규 배차 상태 확인 중"
        : ReceivesNewDispatches ? "신규 배차 받기 ON" : "신규 배차 받기 OFF";
    public bool CanRecordArrival => IsAuthenticated && !IsBusy && !ExceptionEditor.IsOpen
        && !ExceptionEditor.HasPendingRequest
        && ActiveDelivery?.Can(음식배달가능행동Ids.기사가게도착) == true
        && !string.IsNullOrWhiteSpace(ActiveDelivery.DeliveryAttemptId);
    public bool CanOpenInterruption => IsAuthenticated && !IsBusy && !ExceptionEditor.IsOpen
        && ActiveDelivery?.Can(음식배달가능행동Ids.기사배달중단) == true
        && !string.IsNullOrWhiteSpace(ActiveDelivery.DeliveryAttemptId);
    public bool CanSubmitInterruption => IsAuthenticated && !IsBusy && ExceptionEditor.IsOpen
        && ExceptionEditor.Delivery?.Can(음식배달가능행동Ids.기사배달중단) == true;
    public bool CanEditInterruption => CanSubmitInterruption && ExceptionEditor.IsInputUnlocked;

    private void ExceptionEditorChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
        => NotifyExceptionState();

    private void NotifyExceptionState()
    {
        OnPropertyChanged(nameof(IsRegularWorkspaceVisible)); OnPropertyChanged(nameof(IsExceptionWorkspaceVisible));
        OnPropertyChanged(nameof(CanRecordArrival)); OnPropertyChanged(nameof(CanOpenInterruption));
        OnPropertyChanged(nameof(CanSubmitInterruption)); OnPropertyChanged(nameof(CanEditInterruption));
        OnPropertyChanged(nameof(CanToggleDispatchIntent)); OnPropertyChanged(nameof(DispatchIntentButtonText));
        OnPropertyChanged(nameof(CanToggleWork));
        OnPropertyChanged(nameof(CanConfirmPickup)); OnPropertyChanged(nameof(CanCompleteDelivery));
    }

    partial void OnDispatchIntentKnownChanged(bool value) => NotifyExceptionState();
    partial void OnReceivesNewDispatchesChanged(bool value) => NotifyExceptionState();

    private async Task LoadDispatchAvailabilityAsync(CancellationToken cancellationToken)
    {
        try
        {
            var status = await _api.GetDispatchAvailabilityAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ReceivesNewDispatches = status.수신의사Code == 운영배차수신의사Code.On;
            DispatchIntentKnown = true;
            DispatchIntentNotice = ReceivesNewDispatches ? "신규 배차를 받습니다." : "신규 배차를 받지 않습니다. 현재 배달과 위치 갱신은 유지합니다.";
            if (ReceivesNewDispatches && status.실효상태Code != 운영배차실효상태Code.배차가능)
                DispatchIntentNotice += " 운행·배차 조건을 확인해 주세요.";
            if (_pendingDispatchIntent?.수신의사Code == status.수신의사Code)
                _pendingDispatchIntent = null;
            NotifyExceptionState();
        }
        catch (FDriverApiException ex) when (ex.StatusCode != HttpStatusCode.Unauthorized && !cancellationToken.IsCancellationRequested)
        {
            DispatchIntentKnown = false;
            DispatchIntentNotice = "신규 배차 수신 상태를 확인하지 못했습니다. 새로고침해 주세요.";
            NotifyExceptionState();
        }
    }

    [RelayCommand]
    private async Task ToggleDispatchIntent()
    {
        if (!CanToggleDispatchIntent || !_workspaceActive) return;
        _pendingDispatchIntent ??= new()
        {
            클라이언트요청Id = Guid.NewGuid(),
            수신의사Code = ReceivesNewDispatches ? 운영배차수신의사Code.Off : 운영배차수신의사Code.On
        };
        var request = new 운영배차수신의사변경요청
        { 클라이언트요청Id = _pendingDispatchIntent.클라이언트요청Id, 수신의사Code = _pendingDispatchIntent.수신의사Code };
        await RunApiAsync(async cancellationToken =>
        {
            try { await _api.ChangeDispatchIntentAsync(request, cancellationToken); }
            catch (FDriverApiException ex) when (ex.StatusCode != HttpStatusCode.Unauthorized && !cancellationToken.IsCancellationRequested)
            {
                DispatchIntentNotice = "변경 결과를 다시 조회합니다. 응답을 확인할 때까지 같은 요청으로 재시도합니다.";
            }
            cancellationToken.ThrowIfCancellationRequested();
            await LoadDispatchAvailabilityAsync(cancellationToken);
            await LoadWorkspaceAsync(cancellationToken: cancellationToken);
        });
    }

    [RelayCommand]
    private void OpenInterruption()
    {
        if (!CanOpenInterruption || !_workspaceActive || ActiveDelivery is null) return;
        ExceptionEditor.Open(ActiveDelivery);
    }

    [RelayCommand]
    private void CloseInterruption()
    {
        if (IsBusy) return;
        ExceptionEditor.Hide();
    }

    [RelayCommand]
    private async Task RecordRestaurantArrival()
    {
        if (!CanRecordArrival || !_workspaceActive || ActiveDelivery is null) return;
        var delivery = ActiveDelivery;
        if (_pendingArrivalAttemptId != delivery.DeliveryAttemptId)
        {
            _pendingArrival = new() { 클라이언트요청Id = Guid.NewGuid(), 예상시도Revision = delivery.AttemptRevision };
            _pendingArrivalAttemptId = delivery.DeliveryAttemptId;
        }
        var request = new 음식배달가게도착요청
        { 클라이언트요청Id = _pendingArrival!.클라이언트요청Id, 예상시도Revision = _pendingArrival.예상시도Revision };
        await RunApiAsync(async cancellationToken =>
        {
            try
            {
                var result = await _api.RecordRestaurantArrivalAsync(delivery.OfferId, request, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                StatusMessage = result.Message;
            }
            catch (FDriverApiException ex) when (ex.StatusCode != HttpStatusCode.Unauthorized && !cancellationToken.IsCancellationRequested)
            {
                StatusMessage = $"가게 도착 결과를 다시 조회합니다. {ShortMessage(ex.Message)}";
            }
            await LoadWorkspaceAsync(cancellationToken: cancellationToken);
        });
    }

    [RelayCommand]
    private async Task SubmitInterruption()
    {
        if (!CanSubmitInterruption || !_workspaceActive || ExceptionEditor.Delivery is not { } delivery) return;
        var request = ExceptionEditor.Prepare(DateTime.UtcNow);
        if (request is null) return;
        await RunApiAsync(async cancellationToken =>
        {
            var acknowledged = false;
            try
            {
                var result = await _api.InterruptAsync(delivery.OfferId, request, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                StatusMessage = result.Message;
                acknowledged = true;
            }
            catch (FDriverApiException ex) when (ex.StatusCode != HttpStatusCode.Unauthorized && !cancellationToken.IsCancellationRequested)
            {
                ExceptionEditor.ConfirmFailure($"{ShortMessage(ex.Message)} 상태를 다시 조회합니다.",
                    ex.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest);
            }
            await LoadWorkspaceAsync(cancellationToken: cancellationToken);
            if (acknowledged) ExceptionEditor.Reset();
        });
    }

    private void ReconcileExceptionWorkspace()
    {
        var current = ActiveDeliveryItems.FirstOrDefault(x => x.OfferId == ExceptionEditor.Delivery?.OfferId);
        ExceptionEditor.Reconcile(current);
        var arrival = ActiveDeliveryItems.FirstOrDefault(x => x.DeliveryAttemptId == _pendingArrivalAttemptId);
        if (arrival is null || arrival.RestaurantArrivedAtUtc.HasValue || arrival.AttemptRevision != _pendingArrival?.예상시도Revision)
        { _pendingArrival = null; _pendingArrivalAttemptId = null; }
        NotifyExceptionState();
    }

    private void ClearExceptionWorkspace()
    {
        ExceptionEditor.Reset(); _pendingArrival = null; _pendingArrivalAttemptId = null; _pendingDispatchIntent = null;
        DispatchIntentKnown = false; ReceivesNewDispatches = false;
        DispatchIntentNotice = "신규 배차 수신 상태 확인 전";
        NotifyExceptionState();
    }
}
