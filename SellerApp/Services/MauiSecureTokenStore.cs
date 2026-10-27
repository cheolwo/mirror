using Ssalddel.Client.Infrastructure.Security;

namespace SellerApp.Services;

public sealed class MauiSecureTokenStore : IClientSecureTokenStore
{
    private const string StorageKey = "ssalddel.seller.auth_token.v1";
    private readonly ClientAuthTokenJsonStore _store = new(
        StorageKey,
        key => SecureStorage.Default.GetAsync(key),
        (key, json) => SecureStorage.Default.SetAsync(key, json),
        key => SecureStorage.Default.Remove(key));

    public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
        => _store.LoadAsync(cancellationToken);

    public Task SaveAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default)
        => _store.SaveAsync(snapshot, cancellationToken);

    public Task ClearAsync(CancellationToken cancellationToken = default)
        => _store.ClearAsync(cancellationToken);
}
