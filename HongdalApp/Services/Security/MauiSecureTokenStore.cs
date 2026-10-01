using System.Text.Json;
using Hongdal.Client.Infrastructure.Security;

namespace HongdalApp.Services.Security;

public sealed class MauiSecureTokenStore : IClientSecureTokenStore
{
    private const string StorageKey = "hongdal.shipper.authToken.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var json = await SecureStorage.Default.GetAsync(StorageKey);
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
            SecureStorage.Default.Remove(StorageKey);
            return null;
        }
    }

    public async Task SaveAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        await SecureStorage.Default.SetAsync(StorageKey, json);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SecureStorage.Default.Remove(StorageKey);
        return Task.CompletedTask;
    }
}
