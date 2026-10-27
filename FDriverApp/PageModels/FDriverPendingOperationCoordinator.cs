using System.Text.Json;
using FDriverApp.Services;

namespace FDriverApp.PageModels;

// Serializes durable recovery. Request creation, canonical reconciliation and POSTs
// remain with the workspace commands; this coordinator only loads/saves/clears.
internal sealed class FDriverPendingOperationCoordinator(IFDriverPendingOperationStore store)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _loadedOwner;
    private string? _memoryOwner;
    private string? _persistedFingerprint;

    internal async Task RestoreAsync(string owner, Action ensureActor, Action resetMemory,
        Action markRecoveryUnready, Action<FDriverPendingOperationsSnapshot?> applySnapshot,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            ensureActor();
            if (_loadedOwner == owner) return;
            if (_memoryOwner != owner)
            {
                resetMemory();
                _memoryOwner = owner;
            }
            markRecoveryUnready();
            var saved = await store.LoadAsync(cancellationToken);
            ensureActor();
            if (saved is not null && saved.OwnerId != owner)
            {
                await store.ClearAsync(saved.OwnerId, cancellationToken);
                ensureActor();
                saved = null;
            }
            applySnapshot(saved);
            _loadedOwner = owner;
            _persistedFingerprint = Fingerprint(saved ?? new(owner, null, null, null));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { throw new FDriverPendingStorageException(ex); }
        finally { _gate.Release(); }
    }

    internal async Task PersistAsync(string owner, Action ensureActor,
        Func<FDriverPendingOperationsSnapshot> captureSnapshot, Func<string?> currentOwner,
        Action markRecoveryUnready, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            ensureActor();
            var snapshot = captureSnapshot();
            var fingerprint = Fingerprint(snapshot);
            if (fingerprint == _persistedFingerprint) return;
            if (snapshot.Arrival is null && snapshot.DispatchIntent is null && snapshot.Interruption is null)
                await store.ClearAsync(owner, cancellationToken);
            else
                await store.SaveAsync(snapshot, cancellationToken);
            ensureActor();
            _persistedFingerprint = fingerprint;
        }
        catch (OperationCanceledException)
        {
            _loadedOwner = null;
            if (currentOwner() is { } nextOwner && nextOwner != owner)
                await store.ClearAsync(owner, CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            _loadedOwner = null;
            markRecoveryUnready();
            throw new FDriverPendingStorageException(ex);
        }
        finally { _gate.Release(); }
    }

    internal async Task ClearAccountAsync(string? owner, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(owner)) return;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await store.ClearAsync(owner, cancellationToken);
            _persistedFingerprint = null;
            _loadedOwner = null;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { throw new FDriverPendingStorageException(ex); }
        finally { _gate.Release(); }
    }

    internal void ResetMemoryBoundary()
    {
        _loadedOwner = null;
        _memoryOwner = null;
        _persistedFingerprint = null;
    }

    private static string Fingerprint(FDriverPendingOperationsSnapshot value) => JsonSerializer.Serialize(value);
}
