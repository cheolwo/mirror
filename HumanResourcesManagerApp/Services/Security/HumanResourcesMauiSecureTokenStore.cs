using Ssalddel.Client.Infrastructure.Security;

namespace HumanResourcesManagerApp.Services.Security;

public sealed class HumanResourcesMauiSecureTokenStore : IClientSecureTokenStore
{
    private const string StorageKey = "ssalddel.human-resources.authToken.v1";
    private readonly ClientAuthTokenJsonStore _store = new(
        StorageKey,
        key => SecureStorage.Default.GetAsync(key),
        (key, json) => SecureStorage.Default.SetAsync(key, json),
        key => SecureStorage.Default.Remove(key));

    public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
        => _store.LoadAsync(cancellationToken);

    public async Task SaveAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        await _store.SaveAsync(snapshot, cancellationToken);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
        => _store.ClearAsync(cancellationToken);
}
