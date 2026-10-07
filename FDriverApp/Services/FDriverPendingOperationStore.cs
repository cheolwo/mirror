using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Food;

namespace FDriverApp.Services;

public sealed record FDriverPendingArrival(string OfferId, string AttemptId, 음식배달가게도착요청 Request);
public sealed record FDriverPendingInterruption(string OfferId, string AttemptId, 음식배달중단요청 Request);
public sealed record FDriverPendingOperationsSnapshot(string OwnerId, FDriverPendingArrival? Arrival,
    운영배차수신의사변경요청? DispatchIntent, FDriverPendingInterruption? Interruption);

public interface IFDriverPendingOperationStore
{
    Task<FDriverPendingOperationsSnapshot?> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(FDriverPendingOperationsSnapshot snapshot, CancellationToken cancellationToken = default);
    Task ClearAsync(string ownerId, CancellationToken cancellationToken = default);
}

internal sealed class FDriverMemoryPendingOperationStore : IFDriverPendingOperationStore
{
    private FDriverPendingOperationsSnapshot? _snapshot;
    public Task<FDriverPendingOperationsSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(_snapshot); }
    public Task SaveAsync(FDriverPendingOperationsSnapshot snapshot, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); _snapshot = snapshot; return Task.CompletedTask; }
    public Task ClearAsync(string ownerId, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); if (_snapshot?.OwnerId == ownerId) _snapshot = null; return Task.CompletedTask; }
}

public sealed class FDriverPendingStorageException(Exception inner)
    : InvalidOperationException("이전 요청을 안전하게 저장하거나 확인하지 못했습니다. 새로고침한 뒤 다시 시도해 주세요.", inner);
