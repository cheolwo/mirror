using System.Text.Json;

namespace FDriverApp.Services;

public sealed class FDriverSecurePendingOperationStore : IFDriverPendingOperationStore
{
    private const string Key = "ssalddel.fdriver.food-pending.v1";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<FDriverPendingOperationsSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try { var result = await ReadAsync(); cancellationToken.ThrowIfCancellationRequested(); return result; }
        finally { _gate.Release(); }
    }

    public async Task SaveAsync(FDriverPendingOperationsSnapshot snapshot, CancellationToken cancellationToken = default)
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
                    throw new IOException("이전 요청 기록을 지우지 못했습니다.");
            }
        }
        finally { _gate.Release(); }
    }

    private static async Task<FDriverPendingOperationsSnapshot?> ReadAsync()
    {
        var json = await Microsoft.Maui.Storage.SecureStorage.Default.GetAsync(Key);
        if (string.IsNullOrWhiteSpace(json)) return null;
        var result = JsonSerializer.Deserialize<FDriverPendingOperationsSnapshot>(json, JsonOptions)
            ?? throw new InvalidDataException("이전 요청 기록을 읽을 수 없습니다.");
        Validate(result);
        return result;
    }

    private static void Validate(FDriverPendingOperationsSnapshot value)
    {
        if (string.IsNullOrWhiteSpace(value.OwnerId)
            || value.Arrival is { } arrival && (string.IsNullOrWhiteSpace(arrival.OfferId)
                || string.IsNullOrWhiteSpace(arrival.AttemptId) || arrival.Request is null || arrival.Request.클라이언트요청Id == Guid.Empty)
            || value.DispatchIntent is { } intent && intent.클라이언트요청Id == Guid.Empty
            || value.Interruption is { } interruption && (string.IsNullOrWhiteSpace(interruption.OfferId)
                || string.IsNullOrWhiteSpace(interruption.AttemptId) || interruption.Request is null
                || interruption.Request.클라이언트요청Id == Guid.Empty || interruption.Request.메모?.Length is > 500))
            throw new InvalidDataException("이전 요청 기록을 확인해 주세요.");
    }
}
