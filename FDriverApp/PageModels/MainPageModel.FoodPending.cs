using FDriverApp.Services;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Food;

namespace FDriverApp.PageModels;

public sealed partial class MainPageModel
{
    private readonly FDriverPendingOperationCoordinator _pendingOperations;
    private string? _pendingArrivalOfferId;
    private FDriverPendingInterruption? _restoredInterruption;
    private bool _pendingRecoveryReady;

    private (string Owner, CancellationTokenSource Lifetime) CapturePendingActor()
    {
        if (!HasDriverSession() || string.IsNullOrWhiteSpace(_authSession.UserId))
            throw new FDriverApiException("기사 계정으로 다시 로그인해 주세요.", System.Net.HttpStatusCode.Unauthorized);
        return (_authSession.UserId, _workspaceCancellation);
    }

    private void EnsurePendingActor((string Owner, CancellationTokenSource Lifetime) actor, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (actor.Owner != _authSession.UserId || !HasDriverSession() || !IsWorkspaceLifetimeCurrent(actor.Lifetime))
            throw new OperationCanceledException("계정이나 수행 화면이 변경되었습니다.", cancellationToken);
    }

    private async Task RestorePendingOperationsAsync(CancellationToken cancellationToken)
    {
        var actor = CapturePendingActor();
        await _pendingOperations.RestoreAsync(actor.Owner,
            () => EnsurePendingActor(actor, cancellationToken),
            resetMemory: () =>
            {
                ExceptionEditor.Reset();
                _pendingArrival = null; _pendingArrivalAttemptId = null; _pendingArrivalOfferId = null;
                _pendingDispatchIntent = null; _pendingStartReceptionRequestId = null; _pendingStartRequestId = null; _verifiedStartRequestId = null; _restoredInterruption = null;
            },
            markRecoveryUnready: () => _pendingRecoveryReady = false,
            applySnapshot: saved =>
            {
                _pendingArrival = saved?.Arrival?.Request;
                _pendingArrivalAttemptId = saved?.Arrival?.AttemptId;
                _pendingArrivalOfferId = saved?.Arrival?.OfferId;
                _pendingDispatchIntent = saved?.DispatchIntent;
                _pendingStartReceptionRequestId = saved?.StartReceptionRequestId;
                _pendingStartRequestId = saved?.StartRequestId;
                _verifiedStartRequestId = null;
                _restoredInterruption = saved?.Interruption;
            }, cancellationToken);
    }

    private FDriverPendingOperationsSnapshot CapturePendingSnapshot(string owner)
    {
        var interruption = ExceptionEditor.CapturePending();
        var savedInterruption = interruption is not null && ExceptionEditor.Delivery is { } delivery
            ? new FDriverPendingInterruption(delivery.OfferId, delivery.DeliveryAttemptId!, interruption)
            : _restoredInterruption;
        return new(owner,
            _pendingArrival is not null && _pendingArrivalOfferId is not null && _pendingArrivalAttemptId is not null
                ? new(_pendingArrivalOfferId, _pendingArrivalAttemptId, CopyArrival(_pendingArrival)) : null,
            _pendingDispatchIntent is null ? null : CopyIntent(_pendingDispatchIntent), savedInterruption,
            _pendingStartReceptionRequestId, _pendingStartRequestId);
    }

    private async Task PersistPendingOperationsAsync(CancellationToken cancellationToken)
    {
        var actor = CapturePendingActor();
        await _pendingOperations.PersistAsync(actor.Owner,
            () => EnsurePendingActor(actor, cancellationToken),
            () => CapturePendingSnapshot(actor.Owner), () => _authSession.UserId,
            () => _pendingRecoveryReady = false, cancellationToken);
    }

    private async Task ClearPendingAccountAsync(CancellationToken cancellationToken)
        => await _pendingOperations.ClearAccountAsync(_authSession.UserId, cancellationToken);

    private static 음식배달가게도착요청 CopyArrival(음식배달가게도착요청 value) => new()
    { 클라이언트요청Id = value.클라이언트요청Id, 예상시도Revision = value.예상시도Revision };
    private static 운영배차수신의사변경요청 CopyIntent(운영배차수신의사변경요청 value) => new()
    { 클라이언트요청Id = value.클라이언트요청Id, 수신의사Code = value.수신의사Code, 미응답자동중지해제 = value.미응답자동중지해제 };
}
