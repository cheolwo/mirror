using System.Globalization;
using System.Text.Json;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace SsalddelAdminApp.Services;

public sealed class AdminAuthSession : ISsalddelAccessTokenProvider
{
    private const string AccessTokenKey = "ssalddel.admin.access_token";
    private const string RefreshTokenKey = "ssalddel.admin.refresh_token";
    private const string ExpiresAtKey = "ssalddel.admin.access_token_expires_at";
    private const string RefreshExpiresAtKey = "ssalddel.admin.refresh_token_expires_at";
    private const string UserIdKey = "ssalddel.admin.user_id";
    private const string UserNameKey = "ssalddel.admin.user_name";
    private const string RolesKey = "ssalddel.admin.roles";
    private readonly IClientSessionGuard sessionGuard;
    private bool restored;

    public AdminAuthSession(IClientSessionGuard sessionGuard)
    {
        this.sessionGuard = sessionGuard;
    }

    public event Action? Changed;

    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTime AccessTokenExpiresAtUtc { get; private set; }
    public DateTime RefreshTokenExpiresAtUtc { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string? AuthenticationOwnerId => string.IsNullOrWhiteSpace(UserId) ? null : UserId;
    public string UserName { get; private set; } = string.Empty;
    public IReadOnlyList<string> Roles { get; private set; } = [];

    public ClientAuthSessionRestoreState CurrentState
    {
        get
        {
            var snapshot = CreateSnapshot();
            if (sessionGuard.IsAccessTokenUsable(snapshot, DateTime.UtcNow))
            {
                return ClientAuthSessionRestoreState.Authenticated;
            }

            return sessionGuard.IsRefreshTokenUsable(snapshot, DateTime.UtcNow)
                ? ClientAuthSessionRestoreState.RefreshRequired
                : ClientAuthSessionRestoreState.Anonymous;
        }
    }

    public bool IsLoggedIn => CurrentState == ClientAuthSessionRestoreState.Authenticated;

    public bool IsServerAdmin
        => IsLoggedIn && Roles.Contains("서버관리자", StringComparer.Ordinal);

    public async Task<ClientAuthSessionRestoreState> RestoreAsync(
        CancellationToken cancellationToken = default)
    {
        if (restored)
        {
            return CurrentState;
        }

        cancellationToken.ThrowIfCancellationRequested();
        restored = true;
        try
        {
            AccessToken = await SecureStorage.Default.GetAsync(AccessTokenKey);
            RefreshToken = await SecureStorage.Default.GetAsync(RefreshTokenKey);
            UserId = await SecureStorage.Default.GetAsync(UserIdKey) ?? string.Empty;
            UserName = await SecureStorage.Default.GetAsync(UserNameKey) ?? string.Empty;
            var expiresAt = await SecureStorage.Default.GetAsync(ExpiresAtKey);
            var refreshExpiresAt = await SecureStorage.Default.GetAsync(RefreshExpiresAtKey);
            var roles = await SecureStorage.Default.GetAsync(RolesKey);

            AccessTokenExpiresAtUtc = DateTime.TryParse(
                expiresAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsedExpiresAt)
                ? parsedExpiresAt.ToUniversalTime()
                : default;
            RefreshTokenExpiresAtUtc = DateTime.TryParse(
                refreshExpiresAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsedRefreshExpiresAt)
                ? parsedRefreshExpiresAt.ToUniversalTime()
                : default;
            Roles = string.IsNullOrWhiteSpace(roles)
                ? []
                : JsonSerializer.Deserialize<string[]>(roles) ?? [];

            if (CurrentState == ClientAuthSessionRestoreState.Anonymous)
            {
                ClearState();
            }
        }
        catch
        {
            ClearState();
        }

        Changed?.Invoke();
        return CurrentState;
    }

    public async Task ApplyAsync(토큰응답 response)
    {
        ArgumentNullException.ThrowIfNull(response);

        AccessToken = response.AccessToken;
        RefreshToken = response.RefreshToken;
        AccessTokenExpiresAtUtc = response.AccessTokenExpiresAtUtc.ToUniversalTime();
        RefreshTokenExpiresAtUtc = response.RefreshTokenExpiresAtUtc.ToUniversalTime();
        UserId = response.UserId;
        UserName = response.UserName;
        Roles = response.Roles ?? [];
        restored = true;

        await SecureStorage.Default.SetAsync(AccessTokenKey, AccessToken);
        await SecureStorage.Default.SetAsync(RefreshTokenKey, RefreshToken ?? string.Empty);
        await SecureStorage.Default.SetAsync(ExpiresAtKey, AccessTokenExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture));
        await SecureStorage.Default.SetAsync(RefreshExpiresAtKey, RefreshTokenExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture));
        await SecureStorage.Default.SetAsync(UserIdKey, UserId);
        await SecureStorage.Default.SetAsync(UserNameKey, UserName);
        await SecureStorage.Default.SetAsync(RolesKey, JsonSerializer.Serialize(Roles));
        Changed?.Invoke();
    }

    public Task LogoutAsync()
    {
        SecureStorage.Default.Remove(AccessTokenKey);
        SecureStorage.Default.Remove(RefreshTokenKey);
        SecureStorage.Default.Remove(ExpiresAtKey);
        SecureStorage.Default.Remove(RefreshExpiresAtKey);
        SecureStorage.Default.Remove(UserIdKey);
        SecureStorage.Default.Remove(UserNameKey);
        SecureStorage.Default.Remove(RolesKey);
        ClearState();
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    private void ClearState()
    {
        AccessToken = null;
        RefreshToken = null;
        AccessTokenExpiresAtUtc = default;
        RefreshTokenExpiresAtUtc = default;
        UserId = string.Empty;
        UserName = string.Empty;
        Roles = [];
    }

    private ClientAuthTokenSnapshot? CreateSnapshot()
        => string.IsNullOrWhiteSpace(UserId)
            ? null
            : new ClientAuthTokenSnapshot(
                AccessToken ?? string.Empty,
                AccessTokenExpiresAtUtc,
                RefreshToken ?? string.Empty,
                RefreshTokenExpiresAtUtc,
                UserId,
                UserName,
                Roles);
}
