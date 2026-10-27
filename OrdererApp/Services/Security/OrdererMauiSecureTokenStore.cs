using Ssalddel.Client.Infrastructure.Security;

namespace OrdererApp.Services.Security;

/// <summary>주문자 앱 인증 토큰을 다른 역할 앱과 분리된 MAUI 보안 저장소에 보관합니다.</summary>
public sealed class OrdererMauiSecureTokenStore : IClientSecureTokenStore
{
    private const string StorageKey = "ssalddel.orderer.authToken.v1";
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
