using Ssalddel.Client.Infrastructure.Security;

namespace FDriverApp.Services;

public sealed class FDriverSecurePendingOperationStore : IFDriverPendingOperationStore
{
    private const string Key = "ssalddel.fdriver.food-pending.v1";
    private readonly ProtectedPendingSnapshotStore<FDriverPendingOperationsSnapshot> _store = new(
        Key,
        key => Microsoft.Maui.Storage.SecureStorage.Default.GetAsync(key),
        (key, json) => Microsoft.Maui.Storage.SecureStorage.Default.SetAsync(key, json),
        key => Microsoft.Maui.Storage.SecureStorage.Default.Remove(key),
        Validate,
        value => value.OwnerId,
        "이전 요청 기록을 읽을 수 없습니다.",
        "이전 요청 기록을 지우지 못했습니다.");

    public Task<FDriverPendingOperationsSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
        => _store.LoadAsync(cancellationToken);

    public Task SaveAsync(FDriverPendingOperationsSnapshot snapshot, CancellationToken cancellationToken = default)
        => _store.SaveAsync(snapshot, cancellationToken);

    public Task ClearAsync(string ownerId, CancellationToken cancellationToken = default)
        => _store.ClearAsync(ownerId, cancellationToken);

    private static void Validate(FDriverPendingOperationsSnapshot value)
    {
        if (string.IsNullOrWhiteSpace(value.OwnerId)
            || value.Arrival is { } arrival && (string.IsNullOrWhiteSpace(arrival.OfferId)
                || string.IsNullOrWhiteSpace(arrival.AttemptId) || arrival.Request is null || arrival.Request.클라이언트요청Id == Guid.Empty)
            || value.DispatchIntent is { } intent && intent.클라이언트요청Id == Guid.Empty
            || value.StartReceptionRequestId is { } startId && (startId == Guid.Empty
                || value.DispatchIntent?.클라이언트요청Id != startId
                || value.DispatchIntent.수신의사Code != Ssalddel.Contracts.Common.Dispatch.운영배차수신의사Code.On)
            || value.StartRequestId is { } workStartId && (workStartId == Guid.Empty
                || value.StartReceptionRequestId is null || value.StartReceptionRequestId == workStartId)
            || value.Interruption is { } interruption && (string.IsNullOrWhiteSpace(interruption.OfferId)
                || string.IsNullOrWhiteSpace(interruption.AttemptId) || interruption.Request is null
                || interruption.Request.클라이언트요청Id == Guid.Empty || interruption.Request.메모?.Length is > 500))
            throw new InvalidDataException("이전 요청 기록을 확인해 주세요.");
    }
}
