using Hongdal.Client.Infrastructure.Security;

namespace HongdalApp.Services;

public sealed class AuthSession : IAuthSession
{
    private readonly IClientSecureTokenStore _tokenStore;
    private readonly IClientSessionGuard _sessionGuard;
    private bool _restored;

    public AuthSession(IClientSecureTokenStore tokenStore, IClientSessionGuard sessionGuard)
    {
        _tokenStore = tokenStore;
        _sessionGuard = sessionGuard;
    }

    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public string? UserId { get; private set; }
    public string? UserName { get; private set; }
    public IReadOnlyList<string> Roles { get; private set; } = Array.Empty<string>();
    public bool IsLoggedIn => !string.IsNullOrWhiteSpace(AccessToken);

    public async Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        if (_restored)
        {
            return;
        }

        _restored = true;
        var snapshot = await _tokenStore.LoadAsync(cancellationToken);
        if (!_sessionGuard.IsAccessTokenUsable(snapshot, DateTime.UtcNow)
            && !_sessionGuard.IsRefreshTokenUsable(snapshot, DateTime.UtcNow))
        {
            await ClearAsync(cancellationToken);
            return;
        }

        ApplySnapshot(snapshot!);
    }

    public async Task ApplyAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        ApplySnapshot(snapshot);
        await _tokenStore.SaveAsync(snapshot, cancellationToken);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        AccessToken = null;
        RefreshToken = null;
        UserId = null;
        UserName = null;
        Roles = Array.Empty<string>();
        await _tokenStore.ClearAsync(cancellationToken);
    }

    private void ApplySnapshot(ClientAuthTokenSnapshot snapshot)
    {
        AccessToken = snapshot.AccessToken;
        RefreshToken = snapshot.RefreshToken;
        UserId = snapshot.UserId;
        UserName = snapshot.UserName;
        Roles = snapshot.Roles;
    }
}
