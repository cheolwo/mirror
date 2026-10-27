using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace SellerApp.Services;

public sealed class SellerAuthSession : ISsalddelAccessTokenProvider, ISsalddel현재사용자변경알림
{
    private readonly IClientSecureTokenStore tokenStore;
    private readonly IClientSessionGuard sessionGuard;
    private ClientAuthTokenSnapshot? snapshot;
    private bool restored;

    public SellerAuthSession(
        IClientSecureTokenStore tokenStore,
        IClientSessionGuard sessionGuard)
    {
        this.tokenStore = tokenStore;
        this.sessionGuard = sessionGuard;
    }

    public event Action? Changed;

    public string? AccessToken => snapshot?.AccessToken;
    public string? RefreshToken => snapshot?.RefreshToken;
    public string UserId => snapshot?.UserId ?? string.Empty;
    public string UserName => snapshot?.UserName ?? string.Empty;
    public IReadOnlyList<string> Roles => snapshot?.Roles ?? [];

    public ClientAuthSessionRestoreState CurrentState
        => sessionGuard.IsAccessTokenUsable(snapshot, DateTime.UtcNow)
            ? ClientAuthSessionRestoreState.Authenticated
            : sessionGuard.IsRefreshTokenUsable(snapshot, DateTime.UtcNow)
                ? ClientAuthSessionRestoreState.RefreshRequired
                : ClientAuthSessionRestoreState.Anonymous;

    public bool IsLoggedIn => CurrentState == ClientAuthSessionRestoreState.Authenticated;
    public bool IsSellerOperator
        => IsLoggedIn && Roles.Any(SellerAuthService.IsSellerRole);

    public async Task<ClientAuthSessionRestoreState> RestoreAsync(
        CancellationToken cancellationToken = default)
    {
        if (!restored)
        {
            restored = true;
            snapshot = await tokenStore.LoadAsync(cancellationToken);
            if (CurrentState == ClientAuthSessionRestoreState.Anonymous)
            {
                await ClearAsync(cancellationToken);
            }
        }

        return CurrentState;
    }

    public async Task ApplyAsync(
        ClientAuthTokenSnapshot value,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        snapshot = value;
        restored = true;
        await tokenStore.SaveAsync(value, cancellationToken);
        Changed?.Invoke();
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        snapshot = null;
        restored = true;
        await tokenStore.ClearAsync(cancellationToken);
        Changed?.Invoke();
    }
}
