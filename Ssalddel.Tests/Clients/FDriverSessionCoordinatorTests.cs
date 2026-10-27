using System.Net;
using FDriverApp.PageModels;
using FDriverApp.Services;
using Ssalddel.Contracts.Common.Dispatch;

namespace Ssalddel.Tests.Clients;

// Foreground ownership and durable ordering only; no native GPS/storage/UI evidence.
public sealed class FDriverSessionCoordinatorTests
{
    [Fact]
    public void WorkspaceRecovery_RequiresEarlierReadFromCurrentOwnerAndLifetime()
    {
        var session = new FDriverWorkspaceRefreshSession();
        var oldLifetime = session.Lifetime;
        var oldRead = session.AdvanceSnapshot();
        session.CancelLifetime();
        session.EnsureLifetime();
        var failedRead = session.AdvanceSnapshot();
        var error = new FDriverApiException("read unavailable", HttpStatusCode.ServiceUnavailable);
        session.RecordFailure("private-owner", failedRead, "retry notice", error);
        var recoveryRead = session.AdvanceSnapshot();

        Assert.False(session.IsLifetimeCurrent(oldLifetime));
        Assert.True(session.IsLifetimeCurrent(session.Lifetime));
        Assert.True(oldRead < failedRead);
        Assert.Null(session.EarlierFailure("private-owner", recoveryRead, oldLifetime));
        Assert.Null(session.EarlierFailure("different-owner", recoveryRead, session.Lifetime));
        Assert.Null(session.EarlierFailure("private-owner", failedRead, session.Lifetime));
        var failure = Assert.IsType<FDriverWorkspaceRefreshSession.ReadFailure>(
            session.EarlierFailure("private-owner", recoveryRead, session.Lifetime));
        Assert.Same(error, failure.Error);
        Assert.DoesNotContain("private-owner", failure.ToString());
        session.ClearFailure();
        Assert.Null(session.Failure);
    }

    [Fact]
    public void WorkspaceResume_DoesNotAllowOverlappingRefreshUntilOldRefreshEnds()
    {
        var session = new FDriverWorkspaceRefreshSession();
        Assert.True(session.TryBeginBackgroundRefresh());
        session.CancelLifetime();
        session.EnsureLifetime();
        Assert.False(session.TryBeginBackgroundRefresh());
        session.EndBackgroundRefresh();
        Assert.True(session.TryBeginBackgroundRefresh());
    }

    [Fact]
    public async Task AccountChangeDuringSave_ClearsOldOwnerBeforeQueuedNewOwnerRestore()
    {
        var saveEntered = Signal(); var saveFinished = Signal();
        var clearEntered = Signal(); var clearFinished = Signal();
        var store = new PendingStore();
        store.Save = async (snapshot, _) =>
        {
            saveEntered.TrySetResult();
            await saveFinished.Task;
            store.Snapshot = snapshot;
        };
        CancellationToken cleanupToken = new(canceled: true);
        store.Clear = async (owner, token) =>
        {
            Assert.Equal("old-owner", owner);
            cleanupToken = token;
            clearEntered.TrySetResult();
            await clearFinished.Task;
            if (store.Snapshot?.OwnerId == owner) store.Snapshot = null;
        };
        var coordinator = new FDriverPendingOperationCoordinator(store);
        var owner = "old-owner";
        var snapshot = Pending(owner);
        var persist = coordinator.PersistAsync(owner,
            () => { if (owner != "old-owner") throw new OperationCanceledException(); },
            () => snapshot, () => owner, () => { }, CancellationToken.None);
        await saveEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        owner = "new-owner";
        var restored = false;
        var restore = coordinator.RestoreAsync(owner, () => { }, () => { }, () => { },
            saved => { Assert.Null(saved); restored = true; }, CancellationToken.None);
        saveFinished.TrySetResult();
        await clearEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(0, store.LoadCalls);
        Assert.False(restored);
        Assert.False(cleanupToken.CanBeCanceled);
        clearFinished.TrySetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => persist);
        await restore.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(restored);
        Assert.Equal(1, store.LoadCalls);
    }

    [Fact]
    public async Task FailedPendingSave_DoesNotRememberFingerprintAndRequiresRecovery()
    {
        var store = new PendingStore();
        var coordinator = new FDriverPendingOperationCoordinator(store);
        var snapshot = Pending("owner");
        var attempts = 0; var unready = 0;
        store.Save = (_, _) => ++attempts == 1
            ? Task.FromException(new InvalidOperationException("storage unavailable")) : Task.CompletedTask;
        Task Persist() => coordinator.PersistAsync("owner", () => { }, () => snapshot,
            () => "owner", () => unready++, CancellationToken.None);

        await Assert.ThrowsAsync<FDriverPendingStorageException>(Persist);
        await Persist();
        await Persist();
        Assert.Equal(2, attempts);
        Assert.Equal(1, unready);
    }

    [Fact]
    public async Task DrivingRestart_RejectsBothInFlightAndQueuedOldGenerationUploads()
    {
        var session = new FDriverLocationTransmissionSession(TimeSpan.FromSeconds(10));
        using var workspace = new CancellationTokenSource();
        var response = Signal();
        var sends = 0; var confirmations = 0;
        var oldGeneration = session.Generation;
        Task<bool> Transmit(long generation, Func<CancellationToken, Task> send)
            => session.TransmitAsync(generation, workspace, () => true, () => true, send,
                _ => confirmations++, CancellationToken.None);
        var first = Transmit(oldGeneration, _ => { sends++; return response.Task; });
        var queued = Transmit(oldGeneration, _ => { sends++; return Task.CompletedTask; });
        session.Cancel();
        response.TrySetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.False(await queued.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(1, sends);
        Assert.Equal(0, confirmations);
        Assert.Null(session.LastSentAtUtc);
        Assert.True(await Transmit(session.Generation, _ => { sends++; return Task.CompletedTask; }));
        Assert.Equal(2, sends);
        Assert.Equal(1, confirmations);
        Assert.NotNull(session.LastSentAtUtc);
        Assert.False(session.HeartbeatDue);
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static FDriverPendingOperationsSnapshot Pending(string owner)
        => new(owner, null, new 운영배차수신의사변경요청
        { 클라이언트요청Id = Guid.NewGuid(), 수신의사Code = 운영배차수신의사Code.Off }, null);

    private sealed class PendingStore : IFDriverPendingOperationStore
    {
        internal FDriverPendingOperationsSnapshot? Snapshot;
        internal int LoadCalls;
        internal Func<FDriverPendingOperationsSnapshot, CancellationToken, Task>? Save;
        internal Func<string, CancellationToken, Task>? Clear;
        public Task<FDriverPendingOperationsSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
        {
            LoadCalls++;
            return Task.FromResult(Snapshot);
        }
        public Task SaveAsync(FDriverPendingOperationsSnapshot snapshot, CancellationToken cancellationToken = default)
            => Save?.Invoke(snapshot, cancellationToken) ?? Task.CompletedTask;
        public Task ClearAsync(string ownerId, CancellationToken cancellationToken = default)
            => Clear?.Invoke(ownerId, cancellationToken) ?? Task.CompletedTask;
    }
}
