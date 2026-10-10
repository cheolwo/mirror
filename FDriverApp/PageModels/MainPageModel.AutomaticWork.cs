using System.Net;
using FDriverApp.Services;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

namespace FDriverApp.PageModels;

public sealed partial class MainPageModel
{
    // 시작과 수신 ON은 별도 요청입니다. 응답 유실 뒤 시작 POST는 자동 재전송하지 않습니다.
    private Guid? _pendingStartReceptionRequestId;
    private Guid? _pendingStartRequestId;
    private Guid? _verifiedStartRequestId;
    private bool _workStatusKnown;

    private bool ApplyCanonicalWorkStatus(string? status)
    {
        var canonical = FoodDriverWorkRecoveryPolicy.WorkState(status);
        _workStatusKnown = canonical != FoodDriverCanonicalWorkState.Unknown;
        if (_workStatusKnown) IsOnDuty = canonical == FoodDriverCanonicalWorkState.Driving;
        if (_workStatusKnown && StatusMessage is "운행 상태를 자동으로 확인하고 있습니다."
            or "운행·배차 수신 결과를 자동으로 다시 확인하고 있습니다.")
            StatusMessage = string.Empty;
        NotifyCommandState();
        return _workStatusKnown;
    }

    private async Task StartAutomaticWorkAsync(CancellationToken token)
    {
        var actor = CapturePendingActor();
        if (_pendingDispatchIntent is not null)
        {
            await ReconcileAutomaticWorkAsync(token);
            EnsurePendingActor(actor, token);
            if (_pendingDispatchIntent is not null)
            {
                StatusMessage = "이전 배차 수신 변경을 자동으로 확인하고 있습니다. 확인 뒤 운행을 시작해 주세요.";
                return;
            }
        }

        _pendingDispatchIntent = new()
        {
            클라이언트요청Id = Guid.NewGuid(),
            수신의사Code = 운영배차수신의사Code.On
        };
        _pendingStartReceptionRequestId = _pendingDispatchIntent.클라이언트요청Id;
        _pendingStartRequestId = Guid.NewGuid();
        _verifiedStartRequestId = null;
        await PersistPendingOperationsAsync(token);
        EnsurePendingActor(actor, token);
        NotifyExceptionState();
        var location = await CaptureLocationAsync(sendToServer: false, token);
        EnsurePendingActor(actor, token);
        var startLocation = location is null
            ? "음식 배달 앱 운행 시작"
            : FormattableString.Invariant($"{location.Latitude:0.000000},{location.Longitude:0.000000}");
        try
        {
            // Never automatically repeat this POST after an uncertain response.
            await _api.StartWorkAsync(startLocation, _pendingStartRequestId.Value, token);
        }
        catch (FDriverApiException ex) when (FoodDriverWorkRecoveryPolicy.StartWasRejected((int?)ex.StatusCode)
            && !token.IsCancellationRequested)
        {
            EnsurePendingActor(actor, token);
            // 이 요청이 거절됐다면 다른 요청의 기존 운행/수신 OFF를 변경하지 않습니다.
            await ClearAutomaticStartReceptionAsync(token);
            throw;
        }
        catch (FDriverApiException ex) when (ex.StatusCode != HttpStatusCode.Unauthorized && !token.IsCancellationRequested)
        {
            EnsurePendingActor(actor, token);
            StatusMessage = "운행 시작 응답을 확인하지 못했습니다. 실제 운행 상태를 자동으로 확인합니다.";
        }
        EnsurePendingActor(actor, token);
        await ReconcileAutomaticWorkAsync(token);
        if (StatusMessage == FoodDriverWorkRecoveryPolicy.StartOwnershipNotice) return;
        if (IsOnDuty)
        {
            try { await CaptureLocationAsync(sendToServer: true, token); }
            catch (FDriverApiException ex) when (ex.StatusCode != HttpStatusCode.Unauthorized && !token.IsCancellationRequested)
            {
                EnsurePendingActor(actor, token);
                LocationSyncText = "기사 위치 전송 지연 · 자동으로 다시 전송합니다.";
            }
            StatusMessage = _pendingStartReceptionRequestId is null && ReceivesNewDispatches
                ? "운행을 시작했습니다. 현재 위치에 맞는 새 배달 요청을 자동으로 확인합니다."
                : "운행 중입니다. 신규 배차 수신 결과를 자동으로 확인합니다.";
        }
    }

    private async Task ReconcileAutomaticWorkAsync(CancellationToken token)
    {
        if (_pendingDispatchIntent is null && _workStatusKnown) return;
        var actor = CapturePendingActor();
        var requestId = _pendingDispatchIntent?.클라이언트요청Id;
        var generation = Interlocked.Read(ref _workspaceSnapshotGeneration);
        void EnsureCurrent()
        {
            EnsurePendingActor(actor, token);
            if (generation != Interlocked.Read(ref _workspaceSnapshotGeneration)
                || _pendingDispatchIntent?.클라이언트요청Id != requestId)
                throw new OperationCanceledException("더 최신 배차 수신 확인이 진행 중입니다.", token);
        }

        if (!_workStatusKnown || (_pendingStartReceptionRequestId is not null && _pendingStartReceptionRequestId == requestId))
        {
            _workStatusKnown = false;
            NotifyCommandState();
            var work = await _api.GetWorkStatusAsync(token);
            EnsureCurrent();
            // Only recognized canonical terminal states unlock another explicit Start.
            // Unknown/future values must not turn an uncertain POST into a second shift.
            if (!ApplyCanonicalWorkStatus(work?.Status))
                throw new FDriverApiException("운행 상태를 확인하지 못했습니다. 자동으로 다시 확인합니다.", null);
            if (FoodDriverWorkRecoveryPolicy.ResolveStart(_pendingStartReceptionRequestId is not null, work?.Status, null)
                == FoodDriverStartRecoveryDecision.ClearStopped)
            {
                await ClearAutomaticStartReceptionAsync(token);
                StatusMessage = "운행 시작이 확인되지 않았습니다. 운행 시작을 다시 눌러 주세요.";
                return;
            }
            if (_pendingStartReceptionRequestId is not null
                && !FoodDriverWorkRecoveryPolicy.StartOwnershipMatches(_pendingStartRequestId,
                    work?.WorkStartClientRequestId, work?.운송실행유형))
            {
                await ClearAutomaticStartReceptionAsync(token);
                StatusMessage = FoodDriverWorkRecoveryPolicy.StartOwnershipNotice;
                return;
            }
            _verifiedStartRequestId = _pendingStartRequestId;
        }

        if (_pendingDispatchIntent is null) return;

        await LoadDispatchAvailabilityAsync(token, generation);
        EnsurePendingActor(actor, token);
        if (generation != Interlocked.Read(ref _workspaceSnapshotGeneration)) return;
        // A canonical matching read completes the saved request without a second PUT.
        if (_pendingDispatchIntent is null)
        {
            await PersistPendingOperationsAsync(token);
            return;
        }
        EnsureCurrent();
        if (!DispatchIntentKnown) return;
        var request = CopyIntent(_pendingDispatchIntent);
        try
        {
            await _api.ChangeDispatchIntentAsync(request, token);
            EnsureCurrent();
        }
        catch (FDriverApiException ex) when (ex.StatusCode != HttpStatusCode.Unauthorized && !token.IsCancellationRequested)
        {
            EnsureCurrent();
            DispatchIntentKnown = false;
            DispatchIntentNotice = "배차 수신 변경 결과를 자동으로 다시 확인합니다.";
        }
        await LoadDispatchAvailabilityAsync(token, generation);
        EnsurePendingActor(actor, token);
        if (generation != Interlocked.Read(ref _workspaceSnapshotGeneration)) return;
        await PersistPendingOperationsAsync(token);
        NotifyExceptionState();
    }

    private async Task ClearAutomaticStartReceptionAsync(CancellationToken token)
    {
        if (_pendingStartReceptionRequestId is null) return;
        if (_pendingDispatchIntent?.클라이언트요청Id == _pendingStartReceptionRequestId)
            _pendingDispatchIntent = null;
        _pendingStartReceptionRequestId = null;
        _pendingStartRequestId = null;
        _verifiedStartRequestId = null;
        await PersistPendingOperationsAsync(token);
        NotifyExceptionState();
    }
}
