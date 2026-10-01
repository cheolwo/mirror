using System.Globalization;
using System.Text.Json;
using Hongdal.Contracts.Common;
using Hongdal.Ui.Common.Areas.App.Services;

namespace HongdalAdminApp.Services;

public sealed class AdminAuthSession : IHongdalAccessTokenProvider
{
    private const string AccessTokenKey = "hongdal.admin.access_token";
    private const string RefreshTokenKey = "hongdal.admin.refresh_token";
    private const string ExpiresAtKey = "hongdal.admin.access_token_expires_at";
    private const string UserIdKey = "hongdal.admin.user_id";
    private const string UserNameKey = "hongdal.admin.user_name";
    private const string RolesKey = "hongdal.admin.roles";
    private bool restored;

    public event Action? Changed;

    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTime AccessTokenExpiresAtUtc { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string UserName { get; private set; } = string.Empty;
    public IReadOnlyList<string> Roles { get; private set; } = [];

    public bool IsLoggedIn
        => !string.IsNullOrWhiteSpace(AccessToken)
           && AccessTokenExpiresAtUtc > DateTime.UtcNow;

    public bool IsServerAdmin
        => IsLoggedIn && Roles.Contains("서버관리자", StringComparer.Ordinal);

    public async Task RestoreAsync()
    {
        if (restored)
        {
            return;
        }

        restored = true;
        try
        {
            AccessToken = await SecureStorage.Default.GetAsync(AccessTokenKey);
            RefreshToken = await SecureStorage.Default.GetAsync(RefreshTokenKey);
            UserId = await SecureStorage.Default.GetAsync(UserIdKey) ?? string.Empty;
            UserName = await SecureStorage.Default.GetAsync(UserNameKey) ?? string.Empty;
            var expiresAt = await SecureStorage.Default.GetAsync(ExpiresAtKey);
            var roles = await SecureStorage.Default.GetAsync(RolesKey);

            AccessTokenExpiresAtUtc = DateTime.TryParse(
                expiresAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsedExpiresAt)
                ? parsedExpiresAt.ToUniversalTime()
                : default;
            Roles = string.IsNullOrWhiteSpace(roles)
                ? []
                : JsonSerializer.Deserialize<string[]>(roles) ?? [];

            if (!IsLoggedIn)
            {
                ClearState();
            }
        }
        catch
        {
            ClearState();
        }

        Changed?.Invoke();
    }

    public async Task ApplyAsync(토큰응답 response)
    {
        ArgumentNullException.ThrowIfNull(response);

        AccessToken = response.AccessToken;
        RefreshToken = response.RefreshToken;
        AccessTokenExpiresAtUtc = response.AccessTokenExpiresAtUtc.ToUniversalTime();
        UserId = response.UserId;
        UserName = response.UserName;
        Roles = response.Roles ?? [];
        restored = true;

        await SecureStorage.Default.SetAsync(AccessTokenKey, AccessToken);
        await SecureStorage.Default.SetAsync(RefreshTokenKey, RefreshToken ?? string.Empty);
        await SecureStorage.Default.SetAsync(ExpiresAtKey, AccessTokenExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture));
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
        UserId = string.Empty;
        UserName = string.Empty;
        Roles = [];
    }
}
