using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Food;

namespace RestaurantDeskApp.Services.Security;

public sealed class RestaurantSecureProgressPendingStore : IRestaurantProgressPendingStore
{
    private const string Key = "ssalddel.restaurant.progress-pending.v1";
    private readonly ProtectedPendingSnapshotStore<RestaurantProgressPendingSnapshot> _store = new(
        Key,
        key => Microsoft.Maui.Storage.SecureStorage.Default.GetAsync(key),
        (key, json) => Microsoft.Maui.Storage.SecureStorage.Default.SetAsync(key, json),
        key => Microsoft.Maui.Storage.SecureStorage.Default.Remove(key),
        Validate,
        value => value.OwnerId,
        "진행 요청 기록을 읽을 수 없습니다.",
        "진행 요청 기록을 지우지 못했습니다.");

    public Task<RestaurantProgressPendingSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
        => _store.LoadAsync(cancellationToken);

    public Task SaveAsync(RestaurantProgressPendingSnapshot snapshot, CancellationToken cancellationToken = default)
        => _store.SaveAsync(snapshot, cancellationToken);

    public Task ClearAsync(string ownerId, CancellationToken cancellationToken = default)
        => _store.ClearAsync(ownerId, cancellationToken);

    private static void Validate(RestaurantProgressPendingSnapshot value)
    {
        if (string.IsNullOrWhiteSpace(value.OwnerId) || value.Requests is null || value.Requests.Count > 500
            || value.Requests.Any(x => x is null || x.RequestId == Guid.Empty || string.IsNullOrWhiteSpace(x.OrderNo)
                || !음식점주문진행작업코드.전체.Contains(x.Operation)))
            throw new InvalidDataException("진행 요청 기록을 확인해 주세요.");
    }
}
