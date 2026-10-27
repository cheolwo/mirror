using System.Text.Json;

namespace Ssalddel.Client.Infrastructure.Security;

/// <summary>보류 요청 한 묶음의 JSON 입출력과 저장소 인스턴스별 직렬 실행만 공유합니다.</summary>
public sealed class ProtectedPendingSnapshotStore<TSnapshot>(
    string storageKey,
    Func<string, Task<string?>> readAsync,
    Func<string, string, Task> writeAsync,
    Func<string, bool> remove,
    Action<TSnapshot> validate,
    Func<TSnapshot, string?> ownerId,
    string invalidDataMessage,
    string removeFailureMessage) where TSnapshot : class
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<TSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try { var result = await ReadAsync(); cancellationToken.ThrowIfCancellationRequested(); return result; }
        finally { _gate.Release(); }
    }

    public async Task SaveAsync(TSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            validate(snapshot);
            await writeAsync(storageKey, JsonSerializer.Serialize(snapshot, JsonOptions));
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally { _gate.Release(); }
    }

    public async Task ClearAsync(string owner, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var snapshot = await ReadAsync();
            if ((snapshot is null ? null : ownerId(snapshot)) == owner)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!remove(storageKey))
                    throw new IOException(removeFailureMessage);
            }
        }
        finally { _gate.Release(); }
    }

    private async Task<TSnapshot?> ReadAsync()
    {
        var json = await readAsync(storageKey);
        if (string.IsNullOrWhiteSpace(json)) return null;
        var result = JsonSerializer.Deserialize<TSnapshot>(json, JsonOptions)
            ?? throw new InvalidDataException(invalidDataMessage);
        validate(result);
        return result;
    }
}
