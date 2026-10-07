using Ssalddel.Contracts.Food;

namespace RestaurantDeskApp.Services;

// 고객·주소·메뉴 사본 없이 요청의 멱등성과 원래 입력만 보존합니다.
public sealed record RestaurantProgressPendingRequest(string OrderNo, Guid RequestId, long? ExpectedRevision,
    string Operation, int? PreparationMinutes, string Reason, string? ReasonCode)
{
    public 음식점주문진행변경요청 ToRequest() => new()
    {
        클라이언트요청Id = RequestId, 예상Revision = ExpectedRevision, 작업 = Operation,
        조리예상분 = PreparationMinutes, 사유 = Reason, 사유Code = ReasonCode
    };
}

public sealed record RestaurantProgressPendingSnapshot(string OwnerId, IReadOnlyList<RestaurantProgressPendingRequest> Requests);

public interface IRestaurantProgressPendingStore
{
    Task<RestaurantProgressPendingSnapshot?> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(RestaurantProgressPendingSnapshot snapshot, CancellationToken cancellationToken = default);
    Task ClearAsync(string ownerId, CancellationToken cancellationToken = default);
}

// 직접 생성하는 기존 서비스/시험의 호환 경로. MAUI 구성은 보안 저장소를 명시 주입합니다.
internal sealed class RestaurantMemoryProgressPendingStore : IRestaurantProgressPendingStore
{
    private RestaurantProgressPendingSnapshot? _snapshot;
    public Task<RestaurantProgressPendingSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(_snapshot); }
    public Task SaveAsync(RestaurantProgressPendingSnapshot snapshot, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); _snapshot = snapshot; return Task.CompletedTask; }
    public Task ClearAsync(string ownerId, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); if (_snapshot?.OwnerId == ownerId) _snapshot = null; return Task.CompletedTask; }
}

public sealed class RestaurantPendingStorageException(Exception inner)
    : InvalidOperationException("이전 요청을 안전하게 저장하거나 확인하지 못했습니다. 다시 확인한 뒤 진행해 주세요.", inner);
