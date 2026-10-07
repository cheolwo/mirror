using System.Text.Json;
using FDriverApp.Services;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Food;

namespace FDriverApp.PageModels;

public sealed partial class MainPageModel
{
    private readonly IFDriverPendingOperationStore _pendingOperationStore;
    private readonly SemaphoreSlim _pendingOperationGate = new(1, 1);
    private string? _pendingLoadedOwner;
    private string? _pendingMemoryOwner;
    private string? _pendingPersistedFingerprint;
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
        await _pendingOperationGate.WaitAsync(cancellationToken);
        try
        {
            EnsurePendingActor(actor, cancellationToken);
            if (_pendingLoadedOwner == actor.Owner) return;
            if (_pendingMemoryOwner != actor.Owner)
            {
                ExceptionEditor.Reset();
                _pendingArrival = null; _pendingArrivalAttemptId = null; _pendingArrivalOfferId = null;
                _pendingDispatchIntent = null; _restoredInterruption = null;
                _pendingMemoryOwner = actor.Owner;
            }
            _pendingRecoveryReady = false;
            var saved = await _pendingOperationStore.LoadAsync(cancellationToken);
            EnsurePendingActor(actor, cancellationToken);
            if (saved is not null && saved.OwnerId != actor.Owner)
            {
                await _pendingOperationStore.ClearAsync(saved.OwnerId, cancellationToken);
                EnsurePendingActor(actor, cancellationToken);
                saved = null;
            }
            _pendingArrival = saved?.Arrival?.Request;
            _pendingArrivalAttemptId = saved?.Arrival?.AttemptId;
            _pendingArrivalOfferId = saved?.Arrival?.OfferId;
            _pendingDispatchIntent = saved?.DispatchIntent;
            _restoredInterruption = saved?.Interruption;
            _pendingLoadedOwner = actor.Owner;
            _pendingPersistedFingerprint = Fingerprint(saved ?? new(actor.Owner, null, null, null));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { throw new FDriverPendingStorageException(ex); }
        finally { _pendingOperationGate.Release(); }
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
            _pendingDispatchIntent is null ? null : CopyIntent(_pendingDispatchIntent), savedInterruption);
    }

    private async Task PersistPendingOperationsAsync(CancellationToken cancellationToken)
    {
        var actor = CapturePendingActor();
        await _pendingOperationGate.WaitAsync(cancellationToken);
        try
        {
            EnsurePendingActor(actor, cancellationToken);
            var snapshot = CapturePendingSnapshot(actor.Owner);
            var fingerprint = Fingerprint(snapshot);
            if (fingerprint == _pendingPersistedFingerprint) return;
            if (snapshot.Arrival is null && snapshot.DispatchIntent is null && snapshot.Interruption is null)
                await _pendingOperationStore.ClearAsync(actor.Owner, cancellationToken);
            else
                await _pendingOperationStore.SaveAsync(snapshot, cancellationToken);
            EnsurePendingActor(actor, cancellationToken);
            _pendingPersistedFingerprint = fingerprint;
        }
        catch (OperationCanceledException)
        {
            _pendingLoadedOwner = null;
            if (_authSession.UserId is { } nextOwner && nextOwner != actor.Owner)
                await _pendingOperationStore.ClearAsync(actor.Owner, CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            _pendingLoadedOwner = null;
            _pendingRecoveryReady = false;
            throw new FDriverPendingStorageException(ex);
        }
        finally { _pendingOperationGate.Release(); }
    }

    private async Task ClearPendingAccountAsync(CancellationToken cancellationToken)
    {
        var owner = _authSession.UserId;
        if (string.IsNullOrWhiteSpace(owner)) return;
        await _pendingOperationGate.WaitAsync(cancellationToken);
        try
        {
            await _pendingOperationStore.ClearAsync(owner, cancellationToken);
            _pendingPersistedFingerprint = null;
            _pendingLoadedOwner = null;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { throw new FDriverPendingStorageException(ex); }
        finally { _pendingOperationGate.Release(); }
    }

    private static string Fingerprint(FDriverPendingOperationsSnapshot value) => JsonSerializer.Serialize(value);
    private static 음식배달가게도착요청 CopyArrival(음식배달가게도착요청 value) => new()
    { 클라이언트요청Id = value.클라이언트요청Id, 예상시도Revision = value.예상시도Revision };
    private static 운영배차수신의사변경요청 CopyIntent(운영배차수신의사변경요청 value) => new()
    { 클라이언트요청Id = value.클라이언트요청Id, 수신의사Code = value.수신의사Code };
}
