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
    public bool CanToggleWork => IsAuthenticated && _pendingRecoveryReady && !IsBusy && !(IsOnDuty && HasActiveWork);
    public bool CanToggleDispatchIntent => IsAuthenticated && _pendingRecoveryReady && !IsBusy && !ExceptionEditor.IsOpen
        && (DispatchIntentKnown || _pendingDispatchIntent is not null);
    public string DispatchIntentButtonText => _pendingDispatchIntent is not null ? "수신 변경 다시 확인"
        : !DispatchIntentKnown ? "신규 배차 상태 확인 중"
        : ReceivesNewDispatches ? "신규 배차 받기 ON" : "신규 배차 받기 OFF";
    public bool CanRecordArrival => IsAuthenticated && _pendingRecoveryReady && !IsBusy && !ExceptionEditor.IsOpen
        && !ExceptionEditor.HasPendingRequest
        && IsArrivalPrimaryAction
        && ActiveDelivery?.Can(음식배달가능행동Ids.기사가게도착) == true
        && (_pendingArrival is null || _pendingArrivalAttemptId == ActiveDelivery.DeliveryAttemptId)
        && !string.IsNullOrWhiteSpace(ActiveDelivery.DeliveryAttemptId);
    public bool CanOpenInterruption => IsAuthenticated && _pendingRecoveryReady && !IsBusy && !ExceptionEditor.IsOpen
        && ActiveDelivery?.Can(음식배달가능행동Ids.기사배달중단) == true
        && !string.IsNullOrWhiteSpace(ActiveDelivery.DeliveryAttemptId);
    public bool CanSubmitInterruption => IsAuthenticated && _pendingRecoveryReady && !IsBusy && ExceptionEditor.IsOpen
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

    private async Task LoadDispatchAvailabilityAsync(CancellationToken cancellationToken, long? workspaceGeneration = null)
    {
        try
        {
            var status = await _api.GetDispatchAvailabilityAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (workspaceGeneration is { } generation
                && generation != Interlocked.Read(ref _workspaceSnapshotGeneration)) return;
            ReceivesNewDispatches = status.수신의사Code == 운영배차수신의사Code.On;
            DispatchIntentKnown = true;
            ApplyWaitingDispatchAvailability(status);
            DispatchIntentNotice = ReceivesNewDispatches ? "신규 배차를 받습니다." : "신규 배차를 받지 않습니다. 현재 배달과 위치 갱신은 유지합니다.";
            if (ReceivesNewDispatches && status.실효상태Code != 운영배차실효상태Code.배차가능)
                DispatchIntentNotice += " 운행·배차 조건을 확인해 주세요.";
            if (_pendingDispatchIntent?.수신의사Code == status.수신의사Code)
                _pendingDispatchIntent = null;
            NotifyExceptionState();
        }
        catch (Exception) when (workspaceGeneration is { } generation
            && generation != Interlocked.Read(ref _workspaceSnapshotGeneration)
            && !cancellationToken.IsCancellationRequested)
        {
            // A newer command/read already owns both the work card and the dispatch intent.
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
        await RunApiAsync(async cancellationToken =>
        {
            if (_pendingDispatchIntent is not null)
            {
                await LoadWorkspaceAsync(cancellationToken: cancellationToken);
                if (!DispatchIntentKnown || _pendingDispatchIntent is null) return;
            }
            _pendingDispatchIntent ??= new()
            {
                클라이언트요청Id = Guid.NewGuid(),
                수신의사Code = ReceivesNewDispatches ? 운영배차수신의사Code.Off : 운영배차수신의사Code.On
            };
            await PersistPendingOperationsAsync(cancellationToken);
            var request = CopyIntent(_pendingDispatchIntent);
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
        await RunApiAsync(async cancellationToken =>
        {
            if (_pendingArrival is not null)
            {
                await LoadWorkspaceAsync(cancellationToken: cancellationToken);
                if (_pendingArrival is null || !ActiveDeliveryItems.Any(x => x.DeliveryAttemptId == delivery.DeliveryAttemptId)) return;
                if (!ActiveDeliveryItems.Any(x => x.DeliveryAttemptId == delivery.DeliveryAttemptId && x.Can(음식배달가능행동Ids.기사가게도착))) return;
            }
            if (_pendingArrivalAttemptId != delivery.DeliveryAttemptId)
            {
                _pendingArrival = new() { 클라이언트요청Id = Guid.NewGuid(), 예상시도Revision = delivery.AttemptRevision };
                _pendingArrivalAttemptId = delivery.DeliveryAttemptId;
                _pendingArrivalOfferId = delivery.OfferId;
            }
            await PersistPendingOperationsAsync(cancellationToken);
            var request = CopyArrival(_pendingArrival!);
            var rejected = false;
            try
            {
                var result = await _api.RecordRestaurantArrivalAsync(delivery.OfferId, request, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                StatusMessage = result.Message;
            }
            catch (FDriverApiException ex) when (ex.StatusCode != HttpStatusCode.Unauthorized && !cancellationToken.IsCancellationRequested)
            {
                rejected = ex.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest;
                StatusMessage = $"가게 도착 결과를 다시 조회합니다. {ShortMessage(ex.Message)}";
            }
            await LoadWorkspaceAsync(cancellationToken: cancellationToken);
            if (rejected)
            {
                _pendingArrival = null; _pendingArrivalAttemptId = null; _pendingArrivalOfferId = null;
                await PersistPendingOperationsAsync(cancellationToken);
            }
        });
    }

    [RelayCommand]
    private async Task SubmitInterruption()
    {
        if (!CanSubmitInterruption || !_workspaceActive || ExceptionEditor.Delivery is not { } delivery) return;
        await RunApiAsync(async cancellationToken =>
        {
            if (ExceptionEditor.HasPendingRequest)
            {
                await LoadWorkspaceAsync(cancellationToken: cancellationToken);
                if (!ExceptionEditor.HasPendingRequest || ExceptionEditor.Delivery?.DeliveryAttemptId != delivery.DeliveryAttemptId) return;
                if (ExceptionEditor.Delivery.Can(음식배달가능행동Ids.기사배달중단) != true) return;
            }
            var request = ExceptionEditor.Prepare(DateTime.UtcNow);
            if (request is null) return;
            await PersistPendingOperationsAsync(cancellationToken);
            var acknowledged = false;
            var rejected = false;
            try
            {
                var result = await _api.InterruptAsync(delivery.OfferId, request, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                StatusMessage = result.Message;
                acknowledged = true;
            }
            catch (FDriverApiException ex) when (ex.StatusCode != HttpStatusCode.Unauthorized && !cancellationToken.IsCancellationRequested)
            {
                rejected = ex.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest;
                ExceptionEditor.ConfirmFailure($"{ShortMessage(ex.Message)} 상태를 다시 조회합니다.",
                    allowEdit: false);
            }
            await LoadWorkspaceAsync(cancellationToken: cancellationToken);
            if (rejected) ExceptionEditor.ConfirmFailure("요청이 적용되지 않았습니다. 현재 상태와 입력을 확인한 뒤 다시 요청해 주세요.", allowEdit: true);
            if (acknowledged) ExceptionEditor.Reset();
            await PersistPendingOperationsAsync(cancellationToken);
        });
    }

    private async Task ReconcileExceptionWorkspaceAsync(CancellationToken cancellationToken)
    {
        if (_restoredInterruption is { } saved)
        {
            var restored = ActiveDeliveryItems.FirstOrDefault(x => x.OfferId == saved.OfferId && x.DeliveryAttemptId == saved.AttemptId);
            if (restored is not null) ExceptionEditor.RestorePending(restored, saved.Request);
            else StatusMessage = "이전 중단 요청 이후 배달 상태가 변경되었습니다. 현재 배달을 확인해 주세요.";
            _restoredInterruption = null;
        }
        var current = ActiveDeliveryItems.FirstOrDefault(x => x.OfferId == ExceptionEditor.Delivery?.OfferId);
        ExceptionEditor.Reconcile(current);
        var arrival = ActiveDeliveryItems.FirstOrDefault(x => x.DeliveryAttemptId == _pendingArrivalAttemptId);
        if (arrival is null || arrival.RestaurantArrivedAtUtc.HasValue)
        { _pendingArrival = null; _pendingArrivalAttemptId = null; _pendingArrivalOfferId = null; }
        await PersistPendingOperationsAsync(cancellationToken);
        NotifyExceptionState();
    }

    private void ClearExceptionWorkspace()
    {
        ExceptionEditor.Reset(); _pendingArrival = null; _pendingArrivalAttemptId = null; _pendingDispatchIntent = null;
        _pendingArrivalOfferId = null; _restoredInterruption = null; _pendingLoadedOwner = null; _pendingMemoryOwner = null;
        _pendingPersistedFingerprint = null; _pendingRecoveryReady = false;
        DispatchIntentKnown = false; ReceivesNewDispatches = false;
        ApplyWaitingDispatchAvailability(null);
        DispatchIntentNotice = "신규 배차 수신 상태 확인 전";
        NotifyExceptionState();
    }
}
