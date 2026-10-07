using System.Text.Json;
using Ssalddel.Contracts.Food;

namespace RestaurantDeskApp.Services.Security;

public sealed class RestaurantSecureProgressPendingStore : IRestaurantProgressPendingStore
{
    private const string Key = "ssalddel.restaurant.progress-pending.v1";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<RestaurantProgressPendingSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try { var result = await ReadAsync(); cancellationToken.ThrowIfCancellationRequested(); return result; }
        finally { _gate.Release(); }
    }

    public async Task SaveAsync(RestaurantProgressPendingSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Validate(snapshot);
            await Microsoft.Maui.Storage.SecureStorage.Default.SetAsync(Key, JsonSerializer.Serialize(snapshot, JsonOptions));
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally { _gate.Release(); }
    }

    public async Task ClearAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if ((await ReadAsync())?.OwnerId == ownerId)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!Microsoft.Maui.Storage.SecureStorage.Default.Remove(Key))
                    throw new IOException("진행 요청 기록을 지우지 못했습니다.");
            }
        }
        finally { _gate.Release(); }
    }

    private static async Task<RestaurantProgressPendingSnapshot?> ReadAsync()
    {
        var json = await Microsoft.Maui.Storage.SecureStorage.Default.GetAsync(Key);
        if (string.IsNullOrWhiteSpace(json)) return null;
        var result = JsonSerializer.Deserialize<RestaurantProgressPendingSnapshot>(json, JsonOptions)
            ?? throw new InvalidDataException("진행 요청 기록을 읽을 수 없습니다.");
        Validate(result);
        return result;
    }

    private static void Validate(RestaurantProgressPendingSnapshot value)
    {
        if (string.IsNullOrWhiteSpace(value.OwnerId) || value.Requests is null || value.Requests.Count > 500
            || value.Requests.Any(x => x is null || x.RequestId == Guid.Empty || string.IsNullOrWhiteSpace(x.OrderNo)
                || !음식점주문진행작업코드.전체.Contains(x.Operation)))
            throw new InvalidDataException("진행 요청 기록을 확인해 주세요.");
    }
}
