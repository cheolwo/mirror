using System.Text.Json;

namespace Ssalddel.Client.Infrastructure.Security;

/// <summary>역할별 보안 저장 연결에서 인증 토큰의 JSON 입출력만 공유합니다.</summary>
public sealed class ClientAuthTokenJsonStore(
    string storageKey,
    Func<string, Task<string?>> readAsync,
    Func<string, string, Task> writeAsync,
    Func<string, bool> remove) : IClientSecureTokenStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var json = await readAsync(storageKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ClientAuthTokenSnapshot>(json, JsonOptions);
        }
        catch (JsonException)
        {
            remove(storageKey);
            return null;
        }
    }

    public async Task SaveAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        await writeAsync(storageKey, json);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        remove(storageKey);
        return Task.CompletedTask;
    }
}
